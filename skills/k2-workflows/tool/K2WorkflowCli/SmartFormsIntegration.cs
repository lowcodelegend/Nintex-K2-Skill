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
            if (workflow.SmartForms == null) return null;
            var form = JObject.Parse(InvokeData("GetFormCategoryAndName", _host, workflow.SmartForms.Form));
            var id = Convert.ToString(form["id"]);
            if (string.IsNullOrWhiteSpace(id)) throw new CliException("SmartForm was not found: " + workflow.SmartForms.Form);
            var content = GetContent(id, workflow.ProcessFullName, true);
            var primary = (content["itemReferences"] as JArray ?? new JArray()).OfType<JObject>()
                .FirstOrDefault(x => (bool?)x["primary"] == true &&
                    string.Equals(Convert.ToString(x["smartObjectName"]), workflow.RequestStatusUpdate.SmartObject, StringComparison.OrdinalIgnoreCase));
            if (primary == null)
                throw new CliException("The SmartForm does not expose the configured request SmartObject as its primary Create reference: " + workflow.RequestStatusUpdate.SmartObject);
            return new SmartFormsIntegrationDescriptor
            {
                FormId = id,
                SystemName = Convert.ToString(form["systemName"]),
                DisplayName = Convert.ToString(form["displayName"]),
                PrimaryItemReference = (JObject)primary.DeepClone()
            };
        }

        public void Integrate(WorkflowSettings workflow, SmartFormsIntegrationDescriptor form)
        {
            if (form == null) return;
            IntegrateStart(workflow, form);
            IntegrateTask(workflow, form);
        }

        public void Verify(WorkflowSettings workflow, SmartFormsIntegrationDescriptor form)
        {
            if (form == null) return;
            var start = GetContent(form.FormId, workflow.ProcessFullName, true);
            if (string.IsNullOrWhiteSpace(Nested(start, "workflowAction", "id")))
                throw new CliException("SmartForm Start integration was not found for " + workflow.ProcessFullName + ".");
            var task = GetContent(form.FormId, workflow.ProcessFullName + "\\Task", false);
            if (string.IsNullOrWhiteSpace(Nested(task, "workflowAction", "id")))
                throw new CliException("SmartForm task integration was not found for " + workflow.ProcessFullName + "\\Task.");

            var states = JArray.Parse(InvokeData("GetFormStates", _host, form.FormId)).OfType<JObject>().ToList();
            var defaultStateIds = GetDefaultStateIds(form.FormId);
            var startState = states.FirstOrDefault(x => string.Equals(Convert.ToString(x["name"]), workflow.SmartForms.StartState, StringComparison.Ordinal));
            if (startState == null) throw new CliException("SmartForm Start state was not found: " + workflow.SmartForms.StartState + ".");
            var taskState = states.FirstOrDefault(x => string.Equals(Convert.ToString(x["name"]), workflow.SmartForms.TaskState, StringComparison.Ordinal));
            if (taskState == null) throw new CliException("SmartForm task state was not found: " + workflow.SmartForms.TaskState + ".");

            var startIsDefault = defaultStateIds.Contains(Convert.ToString(startState["id"]));
            var taskIsDefault = defaultStateIds.Contains(Convert.ToString(taskState["id"]));
            if (taskIsDefault) throw new CliException("SmartForm task state must not be default: " + workflow.SmartForms.TaskState + ".");
            if (startIsDefault != workflow.SmartForms.MakeStartStateDefault)
                throw new CliException("SmartForm Start state default mismatch for '" + workflow.SmartForms.StartState + "': manifest=" +
                    workflow.SmartForms.MakeStartStateDefault.ToString().ToLowerInvariant() + ", runtime=" + startIsDefault.ToString().ToLowerInvariant() + ".");
            if (workflow.SmartForms.MakeStartStateDefault && defaultStateIds.Count != 1)
                throw new CliException("SmartForm must have exactly one default state when the workflow Start state is default: " + workflow.SmartForms.Form + ".");

            Console.WriteLine("Verified SmartForm states: Start default=" + startIsDefault.ToString().ToLowerInvariant() + ", Task default=false");
        }

        public void Remove(WorkflowSettings workflow, SmartFormsIntegrationDescriptor form)
        {
            if (form == null) return;
            RemoveOne(form.FormId, workflow.ProcessFullName + "\\Task", false, workflow.SmartForms.TaskState);
            RemoveOne(form.FormId, workflow.ProcessFullName, true, workflow.SmartForms.StartState);
        }

        private void RemoveOne(string formId, string fqn, bool start, string expectedStateName)
        {
            var content = GetContent(formId, fqn, start);
            var actionId = Nested(content, "workflowAction", "id");
            var stateId = Convert.ToString(content["workflowActionStateId"]);
            if (string.IsNullOrWhiteSpace(actionId)) return;
            var states = JArray.Parse(InvokeData("GetFormStates", _host, formId));
            var state = states.OfType<JObject>().FirstOrDefault(x => string.Equals(Convert.ToString(x["id"]), stateId, StringComparison.OrdinalIgnoreCase));
            var deleteState = state != null && string.Equals(Convert.ToString(state["name"]), expectedStateName, StringComparison.Ordinal);
            InvokeManagement("RemoveWorkflowIntegration", _host, formId, actionId, stateId, true, deleteState, true, fqn);
            Console.WriteLine("Removed SmartForm integration: " + fqn);
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
                if (currentIsDefault == workflow.SmartForms.MakeStartStateDefault) return;

                Console.WriteLine("Reconciling SmartForm Start default state: " + currentIsDefault.ToString().ToLowerInvariant() +
                    " -> " + workflow.SmartForms.MakeStartStateDefault.ToString().ToLowerInvariant());
                UpdateExistingStartDefault(workflow, form, current, currentStateId);
                return;
            }
            var state = BaseState(form.FormId);
            var rule = SelectRule(form.FormId, Convert.ToString(state["id"]), true, workflow.SmartForms.StartRuleContains);
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

            var itemReferences = current["itemReferences"] as JArray ?? new JArray();
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
            var manager = new FormsManager();
            try
            {
                manager.CreateConnection();
                var connection = new SCConnectionStringBuilder
                {
                    Authenticate = true,
                    Host = _k2.Host,
                    Port = (uint)_k2.Port,
                    Integrated = _k2.Integrated,
                    IsPrimaryLogin = true,
                    SecurityLabelName = _k2.SecurityLabel
                };
                manager.Connection.Open(connection.ConnectionString);
                var definition = XDocument.Parse(manager.GetFormDefinition(new Guid(formId)));
                return new HashSet<string>(definition.Descendants()
                    .Where(x => string.Equals(x.Name.LocalName, "State", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals((string)x.Attribute("IsDefault"), "True", StringComparison.OrdinalIgnoreCase))
                    .Select(x => (string)x.Attribute("ID"))
                    .Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
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
                Convert.ToString(x["displayName"]).IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
                ?? rules.OfType<JObject>().FirstOrDefault(x => (bool?)x["isDefault"] == true)
                ?? rules.OfType<JObject>().FirstOrDefault();
            if (rule == null) throw new CliException("SmartForm state has no rule available for workflow integration.");
            return rule;
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
