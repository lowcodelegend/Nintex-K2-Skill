using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SourceCode.Forms.Management;
using SourceCode.Hosting.Client.BaseAPI;

namespace K2WorkflowCli
{
    internal sealed class SmartFormsIntegrationDescriptor
    {
        public string FormId { get; set; }
        public string SystemName { get; set; }
        public string DisplayName { get; set; }
        public JObject PrimaryItemReference { get; set; }
    }

    internal sealed class SmartFormsIntegrationManager
    {
        private readonly object _client;
        private readonly string _host;
        private readonly Type _dataType;
        private readonly Type _managementType;
        private readonly K2Settings _k2;

        public SmartFormsIntegrationManager(object client, string host, K2Settings k2)
        {
            _client = client;
            _host = host;
            _k2 = k2;
            var assembly = Assembly.LoadFrom(System.IO.Path.Combine(RuntimeAssemblyResolver.WorkflowDesignerBin, "SourceCode.K2Designer.dll"));
            _dataType = assembly.GetType("SourceCode.K2Designer.Providers.Legacy.SmartFormsDataProvider", true);
            _managementType = assembly.GetType("SourceCode.K2Designer.Providers.Legacy.SmartFormsManagementProvider", true);
        }

        public SmartFormsIntegrationDescriptor Load(WorkflowSettings workflow)
        {
            return LoadCore(workflow, true);
        }

        public SmartFormsIntegrationDescriptor LoadForCleanup(WorkflowSettings workflow)
        {
            if (workflow.SmartForms == null) return null;
            return WithFormsManager(delegate(FormsManager manager)
            {
                if (!manager.CheckFormExists(workflow.SmartForms.Form))
                {
                    Console.WriteLine("Integrated SmartForm is already absent: " + workflow.SmartForms.Form);
                    return null;
                }
                var info = manager.GetForm(workflow.SmartForms.Form);
                return new SmartFormsIntegrationDescriptor
                {
                    FormId = info.Guid.ToString(),
                    SystemName = info.Name,
                    DisplayName = info.Name
                };
            });
        }

        private SmartFormsIntegrationDescriptor LoadCore(WorkflowSettings workflow, bool requirePrimaryItemReference)
        {
            if (workflow.SmartForms == null) return null;
            var form = JObject.Parse(InvokeData("GetFormCategoryAndName", _host, workflow.SmartForms.Form));
            var id = Convert.ToString(form["id"]);
            if (string.IsNullOrWhiteSpace(id)) throw new CliException("SmartForm was not found: " + workflow.SmartForms.Form);
            JObject primary = null;
            if (requirePrimaryItemReference)
            {
                var content = GetContent(id, workflow.ProcessFullName, true);
                var expectedSmartObject = workflow.RequestStatusUpdate == null ? workflow.SmartForms.PrimarySmartObject : workflow.RequestStatusUpdate.SmartObject;
                primary = SelectPrimaryReference(content["itemReferences"] as JArray, expectedSmartObject);
                if (primary == null && Environment.GetEnvironmentVariable("K2WF_DEBUG") == "1")
                    Console.WriteLine("Primary reference diagnostics: " + new JArray((content["itemReferences"] as JArray ?? new JArray()).OfType<JObject>()
                        .Select(x => O("name", x["name"], "smartObjectName", x["smartObjectName"], "primary", x["primary"], "method", x["smartObjectMethodName"], "methodType", x["smartObjectMehtodType"], "primaryReference", x["primaryReference"]))).ToString(Formatting.None));
                if (primary == null)
                    throw new CliException("The SmartForm does not expose an unambiguous configured request SmartObject reference: " + expectedSmartObject);
            }
            return new SmartFormsIntegrationDescriptor
            {
                FormId = id,
                SystemName = Convert.ToString(form["systemName"]),
                DisplayName = Convert.ToString(form["displayName"]),
                PrimaryItemReference = primary == null ? null : (JObject)primary.DeepClone()
            };
        }

        internal static JObject SelectPrimaryReference(JArray references, string expectedSmartObject)
        {
            var matches = (references ?? new JArray()).OfType<JObject>().Where(x =>
                string.Equals(Convert.ToString(x["smartObjectName"]), expectedSmartObject, StringComparison.OrdinalIgnoreCase)).ToArray();
            var primaries = matches.Where(x => (bool?)x["primary"] == true).ToArray();
            if (primaries.Length > 1) throw new CliException("Ambiguous primary request reference: " + expectedSmartObject);
            var primary = primaries.SingleOrDefault();
            if (primary == null)
            {
                var creates = matches.Where(x => string.Equals(Convert.ToString(x["smartObjectMethodName"]), "Create", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Convert.ToString(x["smartObjectMehtodType"]), "create", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (creates.Length > 1) throw new CliException("Ambiguous primary request Create reference: " + expectedSmartObject);
                primary = creates.SingleOrDefault();
                // Once integrated, GetFormContent commonly reports default Load
                // references and marks the first (possibly navigation) View primary.
                if (primary == null && matches.Length == 1) primary = matches[0];
                if (primary == null && matches.Length > 1) throw new CliException("Ambiguous request reference: " + expectedSmartObject);
            }
            if (primary == null) return null;
            var selected = (JObject)primary.DeepClone();
            selected.Remove("primaryReference");
            selected["primary"] = true;
            selected["startMode"] = "smartForms";
            return selected;
        }

        public void Integrate(WorkflowSettings workflow, SmartFormsIntegrationDescriptor form)
        {
            if (form == null) return;
            IntegrateStart(workflow, form);
            if (!workflow.SmartForms.StartOnly) IntegrateTask(workflow, form);
        }

        public void Verify(WorkflowSettings workflow, SmartFormsIntegrationDescriptor form)
        {
            if (form == null) return;
            var start = GetContent(form.FormId, workflow.ProcessFullName, true);
            if (string.IsNullOrWhiteSpace(Nested(start, "workflowAction", "id")))
                throw new CliException("SmartForm Start integration was not found for " + workflow.ProcessFullName + ".");
            JObject task = null;
            if (!workflow.SmartForms.StartOnly)
            {
                task = GetContent(form.FormId, workflow.ProcessFullName + "\\Task", false);
                if (string.IsNullOrWhiteSpace(Nested(task, "workflowAction", "id"))) throw new CliException("SmartForm task integration was not found for " + workflow.ProcessFullName + "\\Task.");
            }

            var states = JArray.Parse(InvokeData("GetFormStates", _host, form.FormId)).OfType<JObject>().ToList();
            var defaultStateIds = GetDefaultStateIds(form.FormId);
            var startState = states.FirstOrDefault(x => string.Equals(Convert.ToString(x["name"]), workflow.SmartForms.StartState, StringComparison.Ordinal));
            if (startState == null) throw new CliException("SmartForm Start state was not found: " + workflow.SmartForms.StartState + ".");
            var taskState = workflow.SmartForms.StartOnly ? null : states.FirstOrDefault(x => string.Equals(Convert.ToString(x["name"]), workflow.SmartForms.TaskState, StringComparison.Ordinal));
            if (!workflow.SmartForms.StartOnly && taskState == null) throw new CliException("SmartForm task state was not found: " + workflow.SmartForms.TaskState + ".");

            var startIsDefault = defaultStateIds.Contains(Convert.ToString(startState["id"]));
            var taskIsDefault = taskState != null && defaultStateIds.Contains(Convert.ToString(taskState["id"]));
            if (taskIsDefault) throw new CliException("SmartForm task state must not be default: " + workflow.SmartForms.TaskState + ".");
            if (startIsDefault != workflow.SmartForms.MakeStartStateDefault)
                throw new CliException("SmartForm Start state default mismatch for '" + workflow.SmartForms.StartState + "': manifest=" +
                    workflow.SmartForms.MakeStartStateDefault.ToString().ToLowerInvariant() + ", runtime=" + startIsDefault.ToString().ToLowerInvariant() + ".");
            if (workflow.SmartForms.MakeStartStateDefault && defaultStateIds.Count != 1)
                throw new CliException("SmartForm must have exactly one default state when the workflow Start state is default: " + workflow.SmartForms.Form + ".");

            AssertCheckedIn(form);
            WithFormsManager(delegate(FormsManager manager)
            {
                VerifyWorkflowReferenceBindings(manager.GetFormDefinition(new Guid(form.FormId)), workflow);
                return 0;
            });

            Console.WriteLine("Verified SmartForm states: Start default=" + startIsDefault.ToString().ToLowerInvariant() + (workflow.SmartForms.StartOnly ? ", start-only=true" : ", Task default=false"));
        }

        public void CheckInAfterIntegration(SmartFormsIntegrationDescriptor form)
        {
            if (form == null) return;
            CheckInOwnedForm(form, "integration");
        }

        private bool CheckInOwnedForm(SmartFormsIntegrationDescriptor form, string operation)
        {
            if (form == null) return false;
            return WithFormsManager(delegate(FormsManager manager)
            {
                var info = manager.GetForm(new Guid(form.FormId));
                if (!info.IsCheckedOut)
                {
                    return false;
                }

                var owner = Convert.ToString(info.CheckedOutBy);
                if (!IsCurrentIdentity(owner))
                    throw new CliException("Integrated SmartForm is checked out by '" + owner + "' during workflow " + operation + ": " + form.DisplayName + ". The CLI will not publish another user's draft.");
                Console.WriteLine("SmartForm " + operation + " check-in: " + form.DisplayName + " (v" + info.Version + ", owner=" + owner + ")");
                manager.CheckInForm(info.Guid);
                var checkedIn = manager.GetForm(info.Guid);
                if (checkedIn.IsCheckedOut)
                    throw new CliException("Integrated SmartForm remains checked out after workflow " + operation + " check-in: " + form.DisplayName + " (owner=" + checkedIn.CheckedOutBy + ").");
                Console.WriteLine("SmartForm " + operation + " checked in: " + form.DisplayName + " (v" + checkedIn.Version + ")");
                return true;
            });
        }

        private void AssertCheckedIn(SmartFormsIntegrationDescriptor form)
        {
            WithFormsManager(delegate(FormsManager manager)
            {
                var info = manager.GetForm(new Guid(form.FormId));
                if (info.IsCheckedOut)
                    throw new CliException("Integrated SmartForm is checked out by '" + info.CheckedOutBy + "': " + form.DisplayName + ". Workflow verification requires the form to be checked in.");
                Console.WriteLine("Verified integrated SmartForm check-in state: checked in (" + form.DisplayName + ", v" + info.Version + ")");
                return 0;
            });
        }

        private bool IsCurrentIdentity(string owner)
        {
            if (string.IsNullOrWhiteSpace(owner)) return true;
            return K2Connection.SameIdentity(
                owner,
                K2Connection.DescribeIdentity(_k2));
        }

        private T WithFormsManager<T>(Func<FormsManager, T> action)
        {
            var manager = new FormsManager();
            try
            {
                manager.CreateConnection();
                manager.Connection.Open(
                    K2Connection.BuildManagementConnectionString(_k2));
                K2Connection.AssertAuthenticated(
                    manager.Connection,
                    _k2,
                    "K2 SmartForms workflow integration");
                return action(manager);
            }
            finally
            {
                if (manager.Connection != null)
                {
                    manager.Connection.Close();
                    manager.DeleteConnection();
                }
                manager.Dispose();
            }
        }

        public void Remove(WorkflowSettings workflow, SmartFormsIntegrationDescriptor form)
        {
            if (form == null) return;
            // A previous interrupted provider call may have left a tool-owned draft. Publish it once
            // before continuing so the next provider call starts from a stable Form version.
            CheckInOwnedForm(form, "cleanup recovery");
            if (!workflow.SmartForms.StartOnly) RemoveOne(form, workflow.ProcessFullName + "\\Task", false, workflow.SmartForms.TaskState);
            RemoveOne(form, workflow.ProcessFullName, true, workflow.SmartForms.StartState);
            AssertCheckedIn(form);
        }

        private void RemoveOne(SmartFormsIntegrationDescriptor form, string fqn, bool start, string expectedStateName)
        {
            const int maximumAttempts = 6;
            for (var attempt = 1; attempt <= maximumAttempts; attempt++)
            {
                var content = GetContent(form.FormId, fqn, start);
                var actionId = Nested(content, "workflowAction", "id");
                var stateId = Convert.ToString(content["workflowActionStateId"]);
                if (string.IsNullOrWhiteSpace(actionId))
                {
                    Console.WriteLine((attempt == 1 ? "SmartForm integration already absent: " : "Removed SmartForm integration: ") + fqn);
                    return;
                }

                var states = JArray.Parse(InvokeData("GetFormStates", _host, form.FormId));
                var state = states.OfType<JObject>().FirstOrDefault(x => string.Equals(Convert.ToString(x["id"]), stateId, StringComparison.OrdinalIgnoreCase));
                var deleteState = state != null && string.Equals(Convert.ToString(state["name"]), expectedStateName, StringComparison.Ordinal);
                CliException providerFailure = null;
                try
                {
                    InvokeManagement("RemoveWorkflowIntegration", _host, form.FormId, actionId, stateId, true, deleteState, true, fqn);
                }
                catch (CliException ex)
                {
                    providerFailure = ex;
                }

                // K2 can commit part of RemoveWorkflowIntegration and then report its own checkout as
                // foreign. Check in only this identity's draft, re-read the exact action, and continue
                // in-process instead of requiring a full agent/CLI retry for every Form version.
                var recoveredCheckout = CheckInOwnedForm(form, "cleanup recovery");
                var remaining = GetContent(form.FormId, fqn, start);
                if (string.IsNullOrWhiteSpace(Nested(remaining, "workflowAction", "id")))
                {
                    Console.WriteLine("Removed SmartForm integration: " + fqn);
                    return;
                }

                if (!recoveredCheckout)
                {
                    if (providerFailure != null) throw providerFailure;
                    throw new CliException("SmartForms provider reported success but the workflow integration remains: " + fqn + ".");
                }
                if (attempt == maximumAttempts)
                {
                    var detail = providerFailure == null ? string.Empty : " Last provider error: " + providerFailure.Message;
                    throw new CliException("SmartForm workflow integration did not converge after " + maximumAttempts + " bounded recovery attempts: " + fqn + "." + detail);
                }
                Console.WriteLine("Retrying SmartForm integration removal after owned-draft check-in (attempt " + (attempt + 1) + "/" + maximumAttempts + "): " + fqn);
            }
        }

        private void IntegrateStart(WorkflowSettings workflow, SmartFormsIntegrationDescriptor form)
        {
            var current = GetContent(form.FormId, workflow.ProcessFullName, true);
            if (!string.IsNullOrWhiteSpace(Nested(current, "workflowAction", "id")))
            {
                var currentStateId = Convert.ToString(current["workflowActionStateId"]);
                var currentStates = JArray.Parse(InvokeData("GetFormStates", _host, form.FormId));
                var currentState = currentStates.OfType<JObject>().FirstOrDefault(x =>
                    string.Equals(Convert.ToString(x["id"]), currentStateId, StringComparison.OrdinalIgnoreCase));
                var currentIsDefault = currentState != null && GetDefaultStateIds(form.FormId).Contains(currentStateId);
                var presentationRule = SelectRule(form.FormId, currentStateId, true, workflow.SmartForms.StartRuleContains);
                UpdateStartPresentation(workflow, form, presentationRule);
                if (currentIsDefault == workflow.SmartForms.MakeStartStateDefault) return;

                Console.WriteLine("Reconciling SmartForm Start default state: " + currentIsDefault.ToString().ToLowerInvariant() +
                    " -> " + workflow.SmartForms.MakeStartStateDefault.ToString().ToLowerInvariant());
                UpdateExistingStartDefault(workflow, form, current, currentStateId);
                return;
            }
            var state = BaseState(form.FormId);
            var rule = SelectRule(form.FormId, Convert.ToString(state["id"]), true, workflow.SmartForms.StartRuleContains);
            UpdateStartPresentation(workflow, form, rule);
            var tree = GetRuleTree(form.FormId, state, rule, workflow.ProcessFullName, true);
            var handler = tree.OfType<JObject>().FirstOrDefault();
            var actions = handler == null ? null : handler["actions"] as JArray;
            var preceding = actions == null ? null : actions.OfType<JObject>().LastOrDefault(x => x["id"] != null);
            var payload = O(
                "formSystemName", form.SystemName, "formName", form.DisplayName,
                "processName", workflow.ProcessFullName, "stateName", workflow.SmartForms.StartState,
                "stateId", string.Empty, "makeDefaultState", workflow.SmartForms.MakeStartStateDefault,
                "ruleId", rule["definitionGuid"], "ruleInstanceId", rule["instanceGuid"], "ruleSubFormId", rule["subFormGuid"],
                "precedingActionId", preceding == null ? null : preceding["id"], "handlerId", handler == null ? null : handler["id"],
                "itemReferences", new JArray(form.PrimaryItemReference.DeepClone()), "actionId", string.Empty,
                "folio", Expression(workflow.Name), "moveAction", false);
            InvokeManagement("UpdateForm", _host, payload.ToString(Formatting.None));
            Console.WriteLine("Integrated SmartForm Start state: " + workflow.SmartForms.StartState);
        }

        private void UpdateExistingStartDefault(WorkflowSettings workflow, SmartFormsIntegrationDescriptor form, JObject current, string stateId)
        {
            var action = current["workflowAction"] as JObject;
            var actionRule = current["workflowActionRule"] as JObject;
            var actionRuleId = actionRule == null ? string.Empty : Convert.ToString(actionRule["id"]);
            var rules = JArray.Parse(InvokeData("GetFormRules", _host, form.FormId, stateId, true));
            var rule = rules.OfType<JObject>().FirstOrDefault(x =>
                new[] { "id", "definitionGuid", "instanceGuid" }.Any(name =>
                    string.Equals(Convert.ToString(x[name]), actionRuleId, StringComparison.OrdinalIgnoreCase)))
                ?? rules.OfType<JObject>().FirstOrDefault(x => (bool?)x["isDefault"] == true)
                ?? rules.OfType<JObject>().FirstOrDefault();
            if (action == null || rule == null) throw new CliException("Existing SmartForm Start integration is missing its action or rule metadata.");

            var itemReferences = new JArray(form.PrimaryItemReference.DeepClone());
            var payload = O(
                "formSystemName", form.SystemName, "formName", form.DisplayName,
                "processName", workflow.ProcessFullName, "stateName", workflow.SmartForms.StartState,
                "stateId", stateId, "makeDefaultState", workflow.SmartForms.MakeStartStateDefault,
                "ruleId", rule["definitionGuid"], "ruleInstanceId", rule["instanceGuid"], "ruleSubFormId", rule["subFormGuid"],
                "precedingActionId", action["previousActionId"], "handlerId", action["handlerId"],
                "itemReferences", itemReferences.DeepClone(), "actionId", action["id"],
                "folio", Expression(workflow.Name), "moveAction", false);
            InvokeManagement("UpdateForm", _host, payload.ToString(Formatting.None));
            Console.WriteLine("Updated SmartForm Start default state in place: " + workflow.SmartForms.StartState);
        }

        private HashSet<string> GetDefaultStateIds(string formId)
        {
            return WithFormsManager(delegate(FormsManager manager)
            {
                var definition = XDocument.Parse(manager.GetFormDefinition(new Guid(formId)));
                return new HashSet<string>(definition.Descendants()
                    .Where(x => string.Equals(x.Name.LocalName, "State", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals((string)x.Attribute("IsDefault"), "True", StringComparison.OrdinalIgnoreCase))
                    .Select(x => (string)x.Attribute("ID"))
                    .Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
            });
        }

        private void IntegrateTask(WorkflowSettings workflow, SmartFormsIntegrationDescriptor form)
        {
            var activity = workflow.ProcessFullName + "\\Task";
            var current = GetContent(form.FormId, activity, false);
            if (!string.IsNullOrWhiteSpace(Nested(current, "workflowAction", "id"))) return;
            var state = BaseState(form.FormId);
            var rule = SelectRule(form.FormId, Convert.ToString(state["id"]), false, "Initializing");
            var payload = O(
                "formSystemName", form.SystemName, "afterSubmitNavigateToForm", string.Empty, "afterSubmitNavigateToUrl", string.Empty,
                "formName", form.DisplayName, "activityName", activity, "activityDisplayName", workflow.UserTask.Name,
                "stateName", workflow.SmartForms.TaskState, "stateId", string.Empty, "rule", rule.DeepClone(),
                "itemReferences", new JArray(form.PrimaryItemReference.DeepClone()), "actionType", "workflowview",
                "viewAt", string.IsNullOrWhiteSpace(workflow.SmartForms.WorkflowStripLocation) ? "bottom" : workflow.SmartForms.WorkflowStripLocation,
                "actionCommand", "showmessage", "actionMessage", "Worklist item submitted successfully.", "allocateWorkflow", true,
                "actionId", null, "previousActionGuid", null, "handlerGuid", null, "previousHandlerGuid", null,
                "afterSubmitRuleId", string.Empty, "afterSubmitRuleInstanceId", string.Empty);
            InvokeManagement("PublishClientEvent", _host, payload.ToString(Formatting.None));
            Console.WriteLine("Integrated SmartForm task state: " + workflow.SmartForms.TaskState);
        }

        private JObject GetContent(string formId, string fqn, bool start)
        {
            return JObject.Parse(InvokeData("GetFormContent", _host, formId, fqn, start, string.Empty));
        }

        private JObject BaseState(string formId)
        {
            var states = JArray.Parse(InvokeData("GetFormStates", _host, formId));
            var state = states.OfType<JObject>().FirstOrDefault(x => (bool?)x["isBase"] == true) ?? states.OfType<JObject>().FirstOrDefault();
            if (state == null) throw new CliException("SmartForm has no state available for workflow integration.");
            return state;
        }

        private JObject SelectRule(string formId, string stateId, bool start, string contains)
        {
            var rules = JArray.Parse(InvokeData("GetFormRules", _host, formId, stateId, start));
            var rule = rules.OfType<JObject>().FirstOrDefault(x => !string.IsNullOrWhiteSpace(contains) &&
                Convert.ToString(x["displayName"]).IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0);
            if (rule == null && !string.IsNullOrWhiteSpace(contains))
                throw new CliException("No SmartForm rule matches '" + contains + "'; refusing to integrate a different rule.");
            rule = rule ?? rules.OfType<JObject>().FirstOrDefault(x => (bool?)x["isDefault"] == true)
                ?? rules.OfType<JObject>().FirstOrDefault();
            if (rule == null) throw new CliException("SmartForm state has no rule available for workflow integration.");
            return rule;
        }

        private void UpdateStartPresentation(WorkflowSettings workflow, SmartFormsIntegrationDescriptor form, JObject rule)
        {
            var settings = workflow.SmartForms;
            if (string.IsNullOrWhiteSpace(settings.StartButtonText)
                && string.IsNullOrWhiteSpace(settings.StartSuccessMessageTitle)
                && string.IsNullOrWhiteSpace(settings.StartSuccessMessageBody)) return;
            WithFormsManager(delegate(FormsManager manager)
            {
                var info = manager.GetForm(new Guid(form.FormId));
                if (info.IsCheckedOut && !IsCurrentIdentity(Convert.ToString(info.CheckedOutBy)))
                    throw new CliException("Cannot update submission text on another designer's Form draft.");
                var original = manager.GetFormDefinition(info.Guid);
                bool changed;
                var updated = UpdateStartPresentationDefinition(original,
                    Convert.ToString(rule["definitionGuid"]), settings, out changed);
                if (!changed) return 0;
                if (!info.IsCheckedOut) manager.CheckOutForm(info.Guid);
                manager.DeployForms(updated, info.CategoryPath, true);
                var saved = manager.GetForm(info.Guid);
                if (saved.Guid != info.Guid || saved.IsCheckedOut)
                    throw new CliException("Submission text update did not preserve a checked-in Form identity.");
                Console.WriteLine("Updated SmartForm submission text in place: " + form.DisplayName);
                return 0;
            });
        }

        internal static string UpdateStartPresentationDefinition(string xml, string ruleId, SmartFormsSettings settings, out bool changed)
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var events = document.Descendants().Where(x => x.Name.LocalName == "Event").ToArray();
            var selected = events.FirstOrDefault(x => string.Equals((string)x.Attribute("DefinitionID"), ruleId, StringComparison.OrdinalIgnoreCase)
                || string.Equals((string)x.Attribute("ID"), ruleId, StringComparison.OrdinalIgnoreCase));
            if (selected == null || (string)selected.Attribute("SourceType") != "Control")
                throw new CliException("Submission text requires a selected native Button rule.");
            var controlId = (string)selected.Attribute("SourceID");
            var control = document.Descendants().SingleOrDefault(x => x.Name.LocalName == "Control"
                && (string)x.Attribute("ID") == controlId && (string)x.Attribute("Type") == "Button");
            if (control == null) throw new CliException("Selected submission rule does not belong to a native Button.");
            changed = false;
            if (!string.IsNullOrWhiteSpace(settings.StartButtonText))
            {
                var text = control.Elements().Where(x => x.Name.LocalName == "Properties").Elements()
                    .Single(x => x.Name.LocalName == "Property" && x.Elements().Any(n => n.Name.LocalName == "Name" && n.Value == "Text"));
                foreach (var value in text.Elements().Where(x => x.Name.LocalName == "Value" || x.Name.LocalName == "DisplayValue" || x.Name.LocalName == "NameValue"))
                    if (value.Value != settings.StartButtonText) { value.Value = settings.StartButtonText; changed = true; }
            }
            foreach (var action in events.Where(x => (string)x.Attribute("SourceID") == controlId)
                .SelectMany(x => x.Descendants()).Where(x => x.Name.LocalName == "Action" && (string)x.Attribute("Type") == "ShowMessage"))
            {
                foreach (var parameter in action.Descendants().Where(x => x.Name.LocalName == "Parameter" && (string)x.Attribute("TargetType") == "MessageProperty"))
                {
                    var target = (string)parameter.Attribute("TargetID");
                    var value = target == "Title" ? settings.StartSuccessMessageTitle : target == "Body" ? settings.StartSuccessMessageBody : null;
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    var source = parameter.Descendants().Single(x => x.Name.LocalName == "Source" && (string)x.Attribute("SourceType") == "Value");
                    if (source.Value != value) { source.Value = value; changed = true; }
                }
            }
            return changed ? document.ToString(SaveOptions.DisableFormatting) : xml;
        }

        internal static void VerifyWorkflowReferenceBindings(string xml, WorkflowSettings workflow)
        {
            var expected = workflow.RequestStatusUpdate == null ? workflow.SmartForms.PrimarySmartObject : workflow.RequestStatusUpdate.SmartObject;
            if (string.IsNullOrWhiteSpace(expected)) throw new CliException("An integrated workflow must declare its request SmartObject.");
            var actions = XDocument.Parse(xml).Descendants().Where(x => x.Name.LocalName == "Action").ToArray();
            Func<XElement, string, string> property = (action, name) => action.Elements().Where(x => x.Name.LocalName == "Properties").Elements()
                .Where(x => x.Name.LocalName == "Property" && x.Elements().Any(n => n.Name.LocalName == "Name" && n.Value == name))
                .SelectMany(x => x.Elements()).Where(x => x.Name.LocalName == "Value").Select(x => x.Value).FirstOrDefault();
            var starts = actions.Where(x => property(x, "Method") == "StartProcess" && property(x, "ProcessName") == workflow.ProcessFullName).ToArray();
            if (starts.Length != 1 || !starts[0].Descendants().Any(x => x.Name.LocalName == "Parameter"
                && (string)x.Attribute("TargetType") == "ProcessItemReference" && (string)x.Attribute("TargetID") == expected))
                throw new CliException("Workflow StartProcess must bind exactly one start to the configured request reference: " + expected);
            if (workflow.SmartForms.StartOnly) return;
            var loads = actions.Where(x => property(x, "Method") == "LoadProcess" && property(x, "ActivityFullName") == workflow.ProcessFullName + "\\Task").ToArray();
            if (loads.Length != 1 || !loads[0].Descendants().Any(x => x.Name.LocalName == "Result"
                && (string)x.Attribute("SourceType") == "ProcessItemReference" && (string)x.Attribute("SourceID") == expected))
                throw new CliException("Workflow task LoadProcess must bind to the configured request reference: " + expected);
        }

        private JArray GetRuleTree(string formId, JObject state, JObject rule, string fqn, bool start)
        {
            return JArray.Parse(InvokeData("GetRuleHandlersConditionsAndActions", _host, formId, Convert.ToString(state["id"]),
                Convert.ToString(rule["id"]), Convert.ToString(rule["displayName"]), Convert.ToString(rule["instanceGuid"]), fqn, start));
        }

        private string InvokeData(string name, params object[] args) { return Convert.ToString(InvokeProvider(_dataType, name, args)); }
        private void InvokeManagement(string name, params object[] args) { InvokeProvider(_managementType, name, args); }

        private object InvokeProvider(Type type, string name, object[] args)
        {
            var provider = Activator.CreateInstance(type, _client);
            try { return type.GetMethods().Single(x => x.Name == name && x.GetParameters().Length == args.Length).Invoke(provider, args); }
            catch (TargetInvocationException ex) { throw new CliException("SmartForms integration failed: " + ex.GetBaseException().Message); }
        }

        private static JObject Expression(string text)
        {
            return O("smartFields", new JArray(O("text", text, "title", text, "internalId", 1, "componentId", 10004)), "componentId", 10008);
        }

        private static JObject O(params object[] pairs)
        {
            var value = new JObject();
            for (var i = 0; i < pairs.Length; i += 2)
                value.Add((string)pairs[i], pairs[i + 1] == null ? JValue.CreateNull() : JToken.FromObject(pairs[i + 1]));
            return value;
        }

        private static string Nested(JObject value, string objectName, string propertyName)
        {
            var nested = value[objectName] as JObject;
            return nested == null ? string.Empty : Convert.ToString(nested[propertyName]);
        }
    }
}
