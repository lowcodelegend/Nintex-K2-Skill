using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace K2WorkflowCli
{
    internal static class WorkflowJsonBuilder
    {
        public static string BuildStartEnd(string name)
        {
            var root = Obj(
                "nodes", Arr(
                    Activity("Start", 1, 56, "StartStep", null, true, false, null, LinkReference(1), null, null),
                    Activity("End", 2, 280, "EndStep", "endStep", false, true, LinkReference(1), null, null, null)),
                "links", Arr(Link(1, 1, 56, 2, 280)), "configuration", ProcessConfiguration(Arr(), Arr()), "ui", Component(50004),
                "externalReferenceDefinitions", Arr(), "trackedReferences", Arr(),
                "systemName", name, "title", name, "componentId", 50001);
            return root.ToString(Formatting.None);
        }

        public static string BuildRequestApproval(WorkflowSettings workflow, SmartObjectDescriptor smartObject, SmartFormsIntegrationDescriptor smartForm, SmartObjectMethodDescriptor approvalMatrix)
        {
            var update = workflow.RequestStatusUpdate;
            var integrated = smartForm != null;
            if (workflow.ApprovalMatrix != null) return BuildSmartFormsMatrixApproval(workflow, smartObject, smartForm, approvalMatrix);
            if (integrated) return BuildSmartFormsApproval(workflow, smartObject, smartForm);
            var smartObjectExternalId = integrated ? 1 : 2;
            var environmentExternalId = 3;
            var formExternalId = integrated ? 4 : 0;
            var nodes = Arr(
                Activity("Start", 1, 56, "StartStep", null, true, false, null, LinkReference(1), null, null),
                Activity(Default(update.Name, "Set Request Status"), 2, 280, "SmartWizardStep", "smartObjectWizardStep", false, false,
                    LinkReference(1), LinkReference(2), SmartObjectEvent(Default(update.Name, "Set Request Status"), update, smartObject, smartObjectExternalId, integrated), null),
                Activity(workflow.Email.Name, 3, 504, "DefaultStep", "emailStep", false, false,
                    LinkReference(2), LinkReference(3), EmailEvent(workflow.Email, environmentExternalId), null),
                Activity(workflow.UserTask.Name, 4, 728, "DefaultStep", "userTaskStep", false, false,
                    LinkReference(3), LinkReference(4), UserTaskEvent(workflow.UserTask, update.IdentifierDataField, 4, smartObject, smartForm, formExternalId, integrated ? workflow.SmartForms.TaskState : null, environmentExternalId), UserTaskActivityConfiguration(workflow.UserTask, 4, integrated)),
                Activity("End", 5, 952, "EndStep", "endStep", false, true, LinkReference(4), null, null, null));
            var root = Obj(
                "nodes", nodes,
                "links", Arr(Link(1, 1, 56, 2, 280), Link(2, 2, 280, 3, 504), Link(3, 3, 504, 4, 728), Link(4, 4, 728, 5, 952)),
                "configuration", ProcessConfiguration(integrated ? Arr() : Arr(DataField(update.IdentifierDataField, 1, false, 0)), integrated ? Arr(ItemReference(smartObject, smartForm)) : Arr()),
                "ui", Component(50004),
                "externalReferenceDefinitions", integrated
                    ? Arr(ExternalSmartObject(smartObject, true, 1), SmartObjectServiceFunctions(2), EnvironmentField(EnvironmentFieldName(workflow.Email.From), 3), SmartFormReference(smartForm, 4))
                    : Arr(SmartObjectServiceFunctions(1), ExternalSmartObject(smartObject, false, 2), EnvironmentField(EnvironmentFieldName(workflow.Email.From), 3)),
                "trackedReferences", Arr(), "systemName", workflow.Name, "title", workflow.Name, "componentId", 50001);
            return root.ToString(Formatting.None);
        }

        private static string BuildSmartFormsMatrixApproval(WorkflowSettings workflow, SmartObjectDescriptor smartObject, SmartFormsIntegrationDescriptor smartForm, SmartObjectMethodDescriptor matrixMethod)
        {
            if (smartForm == null || matrixMethod == null) throw new CliException("Approval-matrix workflows require SmartForms and resolver SmartObject metadata.");
            var update = workflow.RequestStatusUpdate;
            var approved = CopyStatus(update, Default(update.ApprovedStatusValue, "Approved"), "Status Update - Approved");
            var rejected = CopyStatus(update, Default(update.RejectedStatusValue, "Rejected"), "Status Update - Rejected");
            var resolver = Activity(workflow.ApprovalMatrix.Name, 3, 336, "SmartWizardStep", "smartObjectWizardStep", false, false,
                LinkReference(2), LinkReference(3), ApprovalMatrixEvent(workflow.ApprovalMatrix, smartObject, matrixMethod, 5), null);
            ((JObject)((JArray)resolver["ui"]["leftPorts"])[1])["incomingLinkReferences"] = Arr(LinkReference(7));
            var nodes = Arr(
                Activity("Start", 1, 56, "StartStep", null, true, false, null, LinkReference(1), null, null),
                MultiStepActivity("Status Update - Pending", 2, null, 168, LinkReference(1), LinkReference(2),
                    SmartObjectEvent(Default(update.Name, "Status Update - Pending"), update, smartObject, 1, true),
                    EmailEvent(workflow.Email, 3)),
                resolver,
                MatrixDecisionActivity(4, 504),
                Activity(workflow.UserTask.Name, 5, 672, "DefaultStep", "userTaskStep", false, false,
                    LinkReference(4), LinkReference(6), UserTaskEvent(workflow.UserTask, update.IdentifierDataField, 5, smartObject, smartForm, 4, workflow.SmartForms.TaskState, 3, 3), UserTaskActivityConfiguration(workflow.UserTask, 5, true)),
                DecisionActivity(5, 6, 840, 7, 8),
                MatrixBranchActivity("Status Update - Approved", 7, 252, 504, "leftPorts", LinkReference(5),
                    SmartObjectEvent(approved.Name, approved, smartObject, 1, true), EmailEvent(OutcomeEmail(workflow.Email, "Approved"), 3)),
                MatrixBranchActivity("Status Update - Rejected", 8, 252, 840, "leftPorts", LinkReference(8),
                    SmartObjectEvent(rejected.Name, rejected, smartObject, 1, true), EmailEvent(OutcomeEmail(workflow.Email, "Rejected"), 3)));
            var root = Obj(
                "nodes", nodes,
                "links", Arr(
                    Link(1, 1, 56, 2, 168), Link(2, 2, 168, 3, 336), Link(3, 3, 336, 4, 504), Link(4, 4, 504, 5, 672),
                    HorizontalOutcomeLink(5, 4, 7, 504, "No More Approvers", 2), Link(6, 5, 672, 6, 840),
                    LoopOutcomeLink(7, 6, 3, 840, 336, "Approved", 1), HorizontalOutcomeLink(8, 6, 8, 840, "Rejected", 2)),
                "configuration", ProcessConfiguration(Arr(
                    DataField("Approval Matrix Stage", 1, false, 0, 4),
                    DataField("Approval Matrix Has Approver", 2, true, 0, 0),
                    DataField("Approval Matrix Approver", 3, true, 0, 6),
                    DataField("Approval Matrix Approver Type", 4, true, 0, 6)), Arr(ItemReference(smartObject, smartForm))),
                "ui", Component(50004),
                "externalReferenceDefinitions", Arr(ExternalSmartObject(smartObject, true, 1), SmartObjectServiceFunctions(2), EnvironmentField(EnvironmentFieldName(workflow.Email.From), 3), SmartFormReference(smartForm, 4), ExternalApprovalMatrix(matrixMethod, 5)),
                "trackedReferences", DecisionTrackedReferences(5, 6, 7, 8),
                "systemName", workflow.Name, "title", workflow.Name, "componentId", 50001);
            return root.ToString(Formatting.None);
        }

        private static string BuildSmartFormsApproval(WorkflowSettings workflow, SmartObjectDescriptor smartObject, SmartFormsIntegrationDescriptor smartForm)
        {
            var update = workflow.RequestStatusUpdate;
            var approved = CopyStatus(update, Default(update.ApprovedStatusValue, "Approved"), "Status Update - Approved");
            var rejected = CopyStatus(update, Default(update.RejectedStatusValue, "Rejected"), "Status Update - Rejected");
            var nodes = Arr(
                Activity("Start", 1, 56, "StartStep", null, true, false, null, LinkReference(1), null, null),
                MultiStepActivity("Status Update - Pending", 2, null, 168, LinkReference(1), LinkReference(2),
                    SmartObjectEvent(Default(update.Name, "Status Update - Pending"), update, smartObject, 1, true),
                    EmailEvent(workflow.Email, 3)),
                Activity(workflow.UserTask.Name, 3, 336, "DefaultStep", "userTaskStep", false, false,
                    LinkReference(2), LinkReference(3), UserTaskEvent(workflow.UserTask, update.IdentifierDataField, 3, smartObject, smartForm, 4, workflow.SmartForms.TaskState, 3), UserTaskActivityConfiguration(workflow.UserTask, 3, true)),
                DecisionActivity(4, 504),
                BranchActivity("Status Update - Rejected", 5, 252, 504, "leftPorts", LinkReference(5),
                    SmartObjectEvent(rejected.Name, rejected, smartObject, 1, true),
                    EmailEvent(OutcomeEmail(workflow.Email, "Rejected"), 3)),
                BranchActivity("Status Update - Approved", 6, -196, 504, "rightPorts", LinkReference(4),
                    SmartObjectEvent(approved.Name, approved, smartObject, 1, true),
                    EmailEvent(OutcomeEmail(workflow.Email, "Approved"), 3)));
            var root = Obj(
                "nodes", nodes,
                "links", Arr(
                    Link(1, 1, 56, 2, 168), Link(2, 2, 168, 3, 336), Link(3, 3, 336, 4, 504),
                    SideLink(4, 4, 6, true, "Approved", -196), SideLink(5, 4, 5, false, "Rejected", 252)),
                "configuration", ProcessConfiguration(Arr(), Arr(ItemReference(smartObject, smartForm))),
                "ui", Component(50004),
                "externalReferenceDefinitions", Arr(ExternalSmartObject(smartObject, true, 1), SmartObjectServiceFunctions(2), EnvironmentField(EnvironmentFieldName(workflow.Email.From), 3), SmartFormReference(smartForm, 4)),
                "trackedReferences", DecisionTrackedReferences(),
                "systemName", workflow.Name, "title", workflow.Name, "componentId", 50001);
            return root.ToString(Formatting.None);
        }

        public static JObject ParseAndValidate(string json, string expectedName)
        {
            JObject root;
            try { root = JObject.Parse(json); }
            catch (JsonException ex) { throw new CliException("Workflow definition is not valid JSON: " + ex.Message); }
            if ((int?)root["componentId"] != 50001) throw new CliException("Definition is not a K2 Five HTML5 Workflow Designer JSON process (root componentId 50001 is required).");
            if (!(root["nodes"] is JArray) || !(root["links"] is JArray) || !(root["configuration"] is JObject))
                throw new CliException("Definition must contain nodes, links, and configuration.");
            if (!((JArray)root["nodes"]).OfType<JObject>().Any(x => (bool?)x["isStartActivity"] == true))
                throw new CliException("Definition must contain an HTML5 designer start activity.");
            root["systemName"] = expectedName;
            root["title"] = expectedName;
            return root;
        }

        private static JObject SmartObjectEvent(string title, RequestStatusUpdateSettings settings, SmartObjectDescriptor smartObject, int externalId, bool itemReference)
        {
            var objectReference = "root.externalReferenceDefinitions[{\"internalId\":" + externalId + "}]";
            var methodReference = objectReference + ".methods[{\"internalId\":" + (itemReference ? 2 : 1) + "}]";
            var values = new JObject();
            values[smartObject.Identifier.SystemName] = Mapping(itemReference
                    ? ItemReferencePropertyExpression(1, smartObject.DisplayName, smartObject.Identifier.DisplayName, FindReturnId(smartObject, smartObject.Identifier.SystemName))
                    : DataFieldExpression(1),
                methodReference + ".inputs[{\"internalId\":" + smartObject.Identifier.InternalId + "}]");
            values[smartObject.Status.SystemName] = Mapping(LiteralExpression(settings.StatusValue), methodReference + ".inputs[{\"internalId\":" + smartObject.Status.InternalId + "}]");
            var controls = new JObject();
            AddControl(controls, "spSmartObject", ExternalExpression(objectReference, smartObject.DisplayName));
            AddControl(controls, "cbbMethods", ExternalExpression(methodReference, smartObject.MethodDisplayName));
            controls["pmInputs"] = Obj("values", values, "componentId", 60000);
            AddControl(controls, "radOutputs", LiteralExpression("radOutputs"));
            controls["pmOutputs"] = Component(60000);
            AddControl(controls, "radNoOutputs", LiteralExpression("radNoOutputs"));
            controls["loOptions"] = Obj("listOptions", Obj("filterOption", "all", "direction", "ascending", "componentId", 50011), "componentId", 60000);
            AddControl(controls, "cbxExternalSystem", LiteralExpression("false"));
            AddControl(controls, "cbxContinueOnError", LiteralExpression("false"));
            AddControl(controls, "cbxEnableRequiredFieldsValidation", LiteralExpression("true"));
            AddControl(controls, "SmartObject", LiteralExpression("radNoOutputs"));
            AddControl(controls, "btnLearnMore", Component(10008));
            AddControl(controls, "DefaultLoad", LiteralExpression(smartObject.DefaultLoadMethod));
            AddControl(controls, "kdDefaultLoad", LiteralExpression(smartObject.DefaultLoadMethod));
            AddControl(controls, "Empty", Component(10008));
            AddControl(controls, "kdMethodPropertiesType", LiteralExpression("return"));
            AddControl(controls, "kdReturnDefaultLoadProperties", LiteralExpression("true"));
            AddControl(controls, "kdReturnOnlyKeys", LiteralExpression("true"));
            AddControl(controls, "kdMethodType", LiteralExpression("update"));
            AddControl(controls, "kdCreate", LiteralExpression("create"));
            AddControl(controls, "kdRead", LiteralExpression("read"));
            AddControl(controls, "kdList", LiteralExpression("list"));
            AddControl(controls, "kdDelete", LiteralExpression("delete"));
            AddControl(controls, "kdExecute", LiteralExpression("execute"));
            AddControl(controls, "kdDefaultMethodType", LiteralExpression("read"));
            AddControl(controls, "kdEmpty", Component(10008));
            AddControl(controls, "kdTrue", LiteralExpression("true"));
            AddControl(controls, "radReference", LiteralExpression("radReference"));
            return Obj(
                "wizardId", 3176,
                "wizardDefinition", SmartObjectWizardDefinition(smartObject, itemReference ? 2 : 1),
                "ui", Obj("icon", "smartObjectWizardStep", "template", "SmartWizardStep", "componentId", 30027),
                "configuration", Obj("controlValues", controls, "exceptionSettings", Obj("logException", true, "componentId", 50012), "componentId", 30012),
                "systemName", title, "title", title, "internalId", 1, "componentId", 30011);
        }

        private static JObject ApprovalMatrixEvent(ApprovalMatrixSettings settings, SmartObjectDescriptor request, SmartObjectMethodDescriptor resolver, int externalId)
        {
            var objectReference = "root.externalReferenceDefinitions[{\"internalId\":" + externalId + "}]";
            var methodReference = objectReference + ".methods[{\"internalId\":1}]";
            var values = new JObject();
            AddInput(values, resolver, settings.MatrixCodeInput, LiteralExpression(settings.MatrixCode), methodReference);
            AddInput(values, resolver, settings.AmountInput, RequestPropertyExpression(request, settings.AmountProperty), methodReference);
            AddInput(values, resolver, settings.CurrentStageInput, DataFieldExpression(1), methodReference);
            foreach (var dimension in settings.Dimensions)
                AddInput(values, resolver, dimension.InputProperty, RequestPropertyExpression(request, dimension.RequestProperty), methodReference);

            var outputs = new JObject();
            AddOutput(outputs, resolver, settings.StageProperty, DataFieldExpression(1), methodReference);
            AddOutput(outputs, resolver, settings.HasApproverProperty, DataFieldExpression(2), methodReference);
            AddOutput(outputs, resolver, settings.ApproverProperty, DataFieldExpression(3), methodReference);
            AddOutput(outputs, resolver, settings.ApproverTypeProperty, DataFieldExpression(4), methodReference);

            var wizardObject = MatrixWizardDescriptor(resolver);
            var controls = new JObject();
            AddControl(controls, "spSmartObject", ExternalExpression(objectReference, resolver.DisplayName));
            AddControl(controls, "cbbMethods", ExternalExpression(methodReference, resolver.MethodDisplayName));
            if (resolver.Returns.Count > 0) AddControl(controls, "cbbMethodProperties", ExternalExpression(methodReference + ".returns[{\"internalId\":" + resolver.Returns[0].InternalId + "}]", resolver.Returns[0].DisplayName));
            controls["pmFilterInputs"] = Obj("filterValue", Obj("expressions", Arr(), "componentId", 80016), "componentId", 60000);
            controls["pmInputs"] = Obj("values", values, "componentId", 60000);
            controls["pmOutputs"] = Obj("values", outputs, "componentId", 60000);
            AddControl(controls, "radReference", Component(10008));
            AddControl(controls, "radOutputs", LiteralExpression("radOutputs"));
            AddControl(controls, "radNoOutputs", Component(10008));
            controls["loOptions"] = Obj("listOptions", Obj("filterOption", "all", "direction", "ascending", "componentId", 50011), "componentId", 60000);
            AddControl(controls, "cbxExternalSystem", LiteralExpression("false"));
            AddControl(controls, "cbxContinueOnError", LiteralExpression("false"));
            AddControl(controls, "cbxEnableRequiredFieldsValidation", LiteralExpression("true"));
            AddControl(controls, "SmartObject", LiteralExpression("radOutputs"));
            AddControl(controls, "btnLearnMore", Component(10008));
            AddControl(controls, "DefaultLoad", LiteralExpression(string.Empty));
            AddControl(controls, "kdDefaultLoad", LiteralExpression(string.Empty));
            AddControl(controls, "Empty", Component(10008));
            AddControl(controls, "kdMethodPropertiesType", LiteralExpression("return"));
            AddControl(controls, "kdReturnDefaultLoadProperties", LiteralExpression("true"));
            AddControl(controls, "kdReturnOnlyKeys", LiteralExpression("true"));
            AddControl(controls, "kdMethodType", LiteralExpression(resolver.MethodType));
            AddControl(controls, "kdCreate", LiteralExpression("create"));
            AddControl(controls, "kdRead", LiteralExpression("read"));
            AddControl(controls, "kdList", LiteralExpression("list"));
            AddControl(controls, "kdDelete", LiteralExpression("delete"));
            AddControl(controls, "kdExecute", LiteralExpression("execute"));
            AddControl(controls, "kdDefaultMethodType", LiteralExpression("read"));
            AddControl(controls, "kdEmpty", Component(10008));
            AddControl(controls, "kdTrue", LiteralExpression("true"));
            return Obj(
                "wizardId", 3176,
                "wizardDefinition", SmartObjectWizardDefinition(wizardObject, 2),
                "ui", Obj("icon", "smartObjectWizardStep", "template", "SmartWizardStep", "componentId", 30027),
                "configuration", Obj("controlValues", controls, "exceptionSettings", Obj("logException", true, "componentId", 50012), "componentId", 30012),
                "systemName", settings.Name, "title", settings.Name, "internalId", 1, "componentId", 30011);
        }

        private static void AddInput(JObject values, SmartObjectMethodDescriptor resolver, string propertyName, JObject value, string methodReference)
        {
            var property = FindProperty(resolver.Inputs, propertyName, "input");
            values[property.SystemName] = Mapping(value, methodReference + ".inputs[{\"internalId\":" + property.InternalId + "}]");
        }

        private static void AddOutput(JObject values, SmartObjectMethodDescriptor resolver, string propertyName, JObject value, string methodReference)
        {
            var property = FindProperty(resolver.Returns, propertyName, "return");
            values[property.SystemName] = Mapping(value, methodReference + ".returns[{\"internalId\":" + property.InternalId + "}]");
        }

        private static SmartObjectInputDescriptor FindProperty(System.Collections.Generic.IEnumerable<SmartObjectInputDescriptor> properties, string name, string direction)
        {
            var property = properties.FirstOrDefault(x => string.Equals(x.SystemName, name, StringComparison.OrdinalIgnoreCase));
            if (property == null) throw new CliException("Approval matrix resolver " + direction + " property was not found: " + name + ". Available: " + string.Join(", ", properties.Select(x => x.SystemName).ToArray()));
            return property;
        }

        private static JObject RequestPropertyExpression(SmartObjectDescriptor request, string propertyName)
        {
            var property = request.ReadReturns.FirstOrDefault(x => string.Equals(x.SystemName, propertyName, StringComparison.OrdinalIgnoreCase));
            if (property == null) throw new CliException("Approval matrix request property is not returned by the request SmartObject Read method: " + propertyName);
            return ItemReferencePropertyExpression(1, request.DisplayName, property.DisplayName, property.InternalId);
        }

        private static SmartObjectDescriptor MatrixWizardDescriptor(SmartObjectMethodDescriptor resolver)
        {
            return new SmartObjectDescriptor
            {
                SystemName = resolver.SystemName,
                DisplayName = resolver.DisplayName,
                MethodSystemName = resolver.MethodSystemName,
                MethodDisplayName = resolver.MethodDisplayName,
                MethodType = resolver.MethodType,
                DefaultLoadMethod = string.Empty,
                Inputs = resolver.Inputs,
                ReadReturns = resolver.Returns
            };
        }

        private static JObject SmartObjectWizardDefinition(SmartObjectDescriptor smartObject, int serviceFunctionsExternalId)
        {
            JObject definition;
            try
            {
                using (var stream = typeof(WorkflowJsonBuilder).Assembly.GetManifestResourceStream("K2WorkflowCli.SmartObjectWizardDefinition.json"))
                {
                    if (stream == null) throw new CliException("Embedded SmartObject workflow wizard definition is missing.");
                    using (var reader = new StreamReader(stream)) definition = JObject.Parse(reader.ReadToEnd());
                }
            }
            catch (CliException) { throw; }
            catch (Exception ex) { throw new CliException("Unable to load the SmartObject workflow wizard definition: " + ex.Message); }
            definition["id"] = 3176;
            definition["type"] = "smartWizard";
            definition["eventComponentId"] = 30011;
            definition["eventConfigurationComponentId"] = 30012;
            definition["componentId"] = 60001;
            definition["name"] = "SmartObject Method";
            definition["nameToken"] = "wizardName_smartObject";
            definition["descriptionToken"] = "wizarddescription_wizardname_smartobject";
            definition["tooltipTitleToken"] = "configpanel_tabs_task_tooltips_title_smartobject";
            definition["tooltipDescriptionToken"] = "configpanel_tabs_task_tooltips_description_smartobject";
            definition["smartObjectName"] = smartObject.SystemName;
            definition["smartObjectMethod"] = smartObject.MethodSystemName;
            definition["smartObjectDefaultLoadMethod"] = smartObject.DefaultLoadMethod;
            definition["smartObjectMethodType"] = smartObject.MethodType;
            foreach (var mapping in (definition["smartObjectMappings"] as JArray ?? new JArray()).OfType<JObject>())
            {
                var methods = mapping["methods"] as JArray;
                var methodName = methods == null || methods.Count == 0 ? string.Empty : Convert.ToString(methods[0]["name"]);
                if (methodName.StartsWith("GetSmartObject", StringComparison.Ordinal) || string.Equals(methodName, "GetDefaultLoadMethod", StringComparison.Ordinal))
                    mapping["smartObjectName"] = "root.externalReferenceDefinitions[{\"internalId\":" + serviceFunctionsExternalId + "}]";
            }
            foreach (var value in definition.DescendantsAndSelf().OfType<JValue>().Where(x => x.Type == JTokenType.String && Convert.ToString(x.Value) == "__SMARTOBJECT_DISPLAY_NAME__"))
                value.Value = smartObject.DisplayName;
            foreach (var control in definition.DescendantsAndSelf().OfType<JObject>().Where(x => x["name"] != null))
            {
                var name = Convert.ToString(control["name"]);
                if (name == "spSmartObject") { control["text"] = smartObject.DisplayName; control["smartObjectDefaultLoadMethod"] = smartObject.DefaultLoadMethod; }
                else if (name == "cbbMethods") control["text"] = smartObject.MethodDisplayName;
                else if (name == "ccReference") control["text"] = smartObject.DisplayName;
                else if (name == "DefaultLoad" || name == "kdDefaultLoad") control["text"] = smartObject.DefaultLoadMethod;
                else if (name == "kdMethodType") control["text"] = smartObject.MethodType;
            }
            return definition;
        }

        private static JObject EmailEvent(EmailSettings email, int environmentExternalId)
        {
            var recipients = new JArray();
            for (var i = 0; i < email.To.Count; i++)
            {
                var expression = string.Equals(email.To[i], "$originator", StringComparison.OrdinalIgnoreCase)
                    ? OriginatorExpression() : LiteralExpression(email.To[i]);
                expression["isDynamic"] = true; expression["internalId"] = i + 1;
                recipients.Add(expression);
            }
            var body = LiteralExpression(email.Body); if (email.Html) body["html"] = true;
            var configuration = Obj(
                "from", email.From.StartsWith("$environment:", StringComparison.OrdinalIgnoreCase) && environmentExternalId > 0
                    ? EnvironmentExpression(email.From.Substring(13), environmentExternalId) : LiteralExpression(email.From),
                "to", recipients, "cc", Arr(Component(10008)), "bcc", Arr(Component(10008)),
                "subject", LiteralExpression(email.Subject), "body", body,
                "exceptionSettings", Component(50012), "componentId", 30006);
            return Obj("wizardId", 3001, "ui", Obj("icon", "emailStep", "template", "DefaultStep"),
                "configuration", configuration, "systemName", email.Name, "title", email.Name, "internalId", 1, "componentId", 30004);
        }

        private static JObject TaskNotification(TaskNotificationSettings notification, SmartObjectDescriptor smartObject, int environmentExternalId)
        {
            var from = notification.From.StartsWith("$environment:", StringComparison.OrdinalIgnoreCase)
                ? EnvironmentExpression(notification.From.Substring(13), environmentExternalId)
                : LiteralExpression(notification.From);
            var body = TaskNotificationExpression(notification.Body, smartObject);
            if (notification.RichText) body["richText"] = true;
            return Obj(
                "from", from, "cc", Arr(Component(10008)), "bcc", Arr(Component(10008)),
                "subject", TaskNotificationExpression(notification.Subject, smartObject), "body", body,
                "exceptionSettings", Obj("logException", true, "componentId", 50012), "componentId", 30006);
        }

        private static JObject TaskNotificationExpression(string template, SmartObjectDescriptor smartObject)
        {
            var fields = new JArray();
            var tokenPattern = new Regex(@"{{\s*(request\.([A-Za-z_][A-Za-z0-9_]*)|task\.participantName|task\.worklistLink)\s*}}", RegexOptions.IgnoreCase);
            var position = 0;
            foreach (Match match in tokenPattern.Matches(template))
            {
                if (match.Index > position) fields.Add(LiteralField(template.Substring(position, match.Index - position), fields.Count + 1));
                var token = match.Groups[1].Value;
                if (token.StartsWith("request.", StringComparison.OrdinalIgnoreCase))
                {
                    var propertyName = match.Groups[2].Value;
                    var property = smartObject.ReadReturns.FirstOrDefault(x => string.Equals(x.SystemName, propertyName, StringComparison.OrdinalIgnoreCase));
                    if (property == null) throw new CliException("Task notification references a property that is not returned by the request SmartObject Read method: " + propertyName);
                    fields.Add(ItemReferencePropertyField(1, smartObject.DisplayName, property.DisplayName, property.InternalId, fields.Count + 1));
                }
                else if (string.Equals(token, "task.participantName", StringComparison.OrdinalIgnoreCase))
                    fields.Add(Obj("fieldName", "ActivityInstanceDestUserDisplayName", "parentName", "Task", "title", "Participant Name", "internalId", fields.Count + 1, "componentId", 10009));
                else
                    fields.Add(WorklistLinkField(fields.Count + 1));
                position = match.Index + match.Length;
            }
            if (position < template.Length) fields.Add(LiteralField(template.Substring(position), fields.Count + 1));
            var unsupported = Regex.Match(tokenPattern.Replace(template, string.Empty), @"{{.*?}}", RegexOptions.Singleline);
            if (unsupported.Success) throw new CliException("Unsupported task notification token: " + unsupported.Value);
            return Obj("smartFields", fields, "componentId", 10008);
        }

        private static JObject LiteralField(string value, int internalId)
        {
            return Obj("text", value, "internalId", internalId, "componentId", 10004);
        }

        private static JObject WorklistLinkField(int internalId)
        {
            return Obj(
                "parameters", Arr(
                    Obj("parameterType", "System.String", "title", "DisplayName", "value", LiteralExpression("Click to open worklist item"), "internalId", 1, "componentId", 10017),
                    Obj("parameterType", "System.String", "title", "URL", "value", Obj("smartFields", Arr(Obj("fieldName", "WorkFlowItemContextData", "parentName", "Task", "title", "Worklist Item Link", "internalId", 1, "componentId", 10009)), "componentId", 10008), "internalId", 2, "componentId", 10017)),
                "functionName", "CreateHyperlink", "assemblyId", 2, "className", "SourceCode.Workflow.Functions.HTMLHelper",
                "methodReturnType", "System.String", "title", "Worklist Item Link", "returnType", 1,
                "internalId", internalId, "componentId", 10002);
        }

        private static JObject UserTaskEvent(UserTaskSettings task, string identifierDataField, int nodeId, SmartObjectDescriptor smartObject, SmartFormsIntegrationDescriptor smartForm, int formExternalId, string taskState, int environmentExternalId, int? approvalMatrixAssigneeDataFieldId = null)
        {
            var actions = new JArray();
            for (var i = 0; i < task.Actions.Count; i++)
            {
                var reference = "root.nodes[{\"internalId\":" + nodeId + "}].children[{\"internalId\":1}].configuration.actions[{\"internalId\":" + (i + 1) + "}]";
                actions.Add(Obj("alwaysVisible", true, "continueWorkflow", true, "actionTitle", task.Actions[i],
                    "originalTitle", task.Actions[i], "internalId", i + 1, "componentId", 30019, "oldReference", reference));
            }
            var effectiveAssignee = approvalMatrixAssigneeDataFieldId.HasValue ? DataFieldExpression(approvalMatrixAssigneeDataFieldId.Value) : OriginatorUserExpression();
            effectiveAssignee["type"] = "User";
            effectiveAssignee["isDynamic"] = true;
            effectiveAssignee["internalId"] = 1;
            var destinationTitle = approvalMatrixAssigneeDataFieldId.HasValue ? "Approval Matrix Approver" : "Originator";
            var destinations = Arr(Obj("title", destinationTitle, "destinationItems", Arr(effectiveAssignee),
                "isRecipient", true, "internalId", 1, "componentId", 80010));
            var parameters = smartForm == null ? Arr(
                    Obj("name", TaskParameterName("SN", true), "value", TaskSerialNumber(), "internalId", 1, "componentId", 30018),
                    Obj("name", TaskParameterName(task.RequestIdParameter, false), "value", DataFieldExpression(1), "internalId", 2, "componentId", 30018))
                : Arr(
                    Obj("name", TaskParameterName("SerialNo", true), "value", TaskSerialNumber(), "internalId", 1, "componentId", 30018),
                    Obj("name", TaskParameterName("_state", false, "State"), "value", LiteralExpression(taskState), "internalId", 2, "componentId", 30018));
            var notificationSettings = task.Notification;
            var sendNotification = notificationSettings != null && notificationSettings.Enabled;
            var notification = sendNotification
                ? TaskNotification(notificationSettings, smartObject, environmentExternalId)
                : Obj("from", Component(10008), "cc", Arr(), "bcc", Arr(), "subject", Component(10008),
                    "body", Component(10008), "exceptionSettings", Component(50012), "componentId", 30006);
            var configuration = Obj(
                "instruction", MultiLineLiteralExpression(task.Instructions), "actions", actions, "actionStatementRuleType", 1,
                "formConfiguration", smartForm == null
                    ? Obj("url", LiteralExpression(task.FormUrl), "parameters", parameters, "componentId", 30015)
                    : Obj("url", SmartFormExpression(formExternalId), "parameters", parameters, "componentId", 30016),
                "destinationSets", destinations, "allRecipients", true, "slots", NumberExpression(1),
                "votingResolveGroupsToIndividuals", true, "timeLine", Deadline(), "reminder", Obj("deadlines", Arr()),
                "emailConfiguration", notification, "sendNotification", sendNotification,
                "exceptionSettings", Component(50012), "componentId", 30010);
            if (smartForm != null)
                configuration["votingRuleConsensusSelectedOutcomeReference"] = "root.nodes[{\"internalId\":" + nodeId + "}].configuration.outcomes[{\"internalId\":1}]";
            return Obj("wizardId", 3000, "ui", Obj("icon", "userTaskStep", "template", "DefaultStep"),
                "configuration", configuration, "systemName", task.Name, "title", task.Name, "internalId", 1, "componentId", 30009);
        }

        private static JObject UserTaskActivityConfiguration(UserTaskSettings task, int nodeId, bool decisionRouting)
        {
            var configuration = ActivityConfiguration(true);
            var outcomes = new JArray();
            for (var i = 0; i < task.Actions.Count; i++)
            {
                var reference = "root.nodes[{\"internalId\":" + nodeId + "}].children[{\"internalId\":1}].configuration.actions[{\"internalId\":" + (i + 1) + "}]";
                var outcome = Obj("title", task.Actions[i], "originalTitle", decisionRouting ? OutcomeTitle(task.Actions[i]) : task.Actions[i],
                    "linkedActionReference", reference, "internalId", i + 1, "componentId", 30020);
                if (!decisionRouting) outcome["actionRule"] = Component(80004);
                outcomes.Add(outcome);
            }
            configuration.AddFirst(new JProperty("datafields", Arr(DataField("Action Result", 1, true, 3))));
            configuration.AddFirst(new JProperty("outcomes", outcomes));
            return configuration;
        }

        private static JObject Activity(string title, int id, int y, string template, string icon, bool start, bool end,
            string incomingReference, string outgoingReference, JObject child, JObject configuration)
        {
            var ui = Obj(
                "y", y, "topPorts", Ports("topPorts", incomingReference, true),
                "leftPorts", Ports("leftPorts", null, true), "bottomPorts", Ports("bottomPorts", outgoingReference, false),
                "rightPorts", Ports("rightPorts", null, false), "template", template, "componentId", 40009);
            if (icon != null) ui.AddFirst(new JProperty("icon", icon));
            var node = Obj("ui", ui, "configuration", configuration ?? ActivityConfiguration(end || incomingReference != null),
                "systemName", ActivitySystemName(title, child, id), "title", title, "internalId", id, "componentId", 40000);
            if (child != null) node.AddFirst(new JProperty("children", Arr(child)));
            if (start) node.AddFirst(new JProperty("isStartActivity", true));
            if (end) node["customTitle"] = true;
            return node;
        }

        private static JObject MultiStepActivity(string title, int id, int? x, int y, string incomingReference, string outgoingReference, JObject first, JObject second)
        {
            var ui = Obj("y", y, "icon", "smartObjectWizardStep", "showLabel", true,
                "topPorts", Ports("topPorts", incomingReference, true), "leftPorts", Ports("leftPorts", null, true),
                "bottomPorts", Ports("bottomPorts", outgoingReference, false), "rightPorts", Ports("rightPorts", null, false),
                "template", "MultiStep", "componentId", 40009);
            if (x.HasValue) ui.AddFirst(new JProperty("x", x.Value));
            var configuration = ActivityConfiguration(true);
            configuration.AddFirst(new JProperty("decisionOptionType", 2));
            return Obj("children", Arr(first, second), "ui", ui, "configuration", configuration,
                "systemName", id == 2 ? "SmartWizardStep" : "SmartWizardStep " + (id - 4),
                "title", title, "customTitle", true, "internalId", id, "componentId", 40000);
        }

        private static JObject BranchActivity(string title, int id, int x, int y, string incomingPort, string incomingReference, JObject first, JObject second)
        {
            var top = Ports("topPorts", null, true);
            var left = Ports("leftPorts", incomingPort == "leftPorts" ? incomingReference : null, true);
            var right = Ports("rightPorts", incomingPort == "rightPorts" ? incomingReference : null, true);
            var ui = Obj("x", x, "y", y, "icon", "smartObjectWizardStep", "showLabel", true,
                "topPorts", top, "leftPorts", left, "bottomPorts", Ports("bottomPorts", null, false), "rightPorts", right,
                "template", "MultiStep", "componentId", 40009);
            var configuration = ActivityConfiguration(true);
            configuration.AddFirst(new JProperty("decisionOptionType", 2));
            return Obj("children", Arr(first, second), "ui", ui, "configuration", configuration,
                "systemName", "SmartWizardStep " + (id - 4), "title", title, "customTitle", true,
                "internalId", id, "componentId", 40000);
        }

        private static JObject MatrixBranchActivity(string title, int id, int x, int y, string incomingPort, string incomingReference, JObject first, JObject second)
        {
            return BranchActivity(title, id, x, y, incomingPort, incomingReference, first, second);
        }

        private static JObject DecisionActivity(int id, int y)
        {
            return DecisionActivity(3, id, y, 4, 5);
        }

        private static JObject DecisionActivity(int taskId, int id, int y, int approvedLinkId, int rejectedLinkId)
        {
            var left = Ports("leftPorts", LinkReference(approvedLinkId), false);
            var leftPort = (JObject)left[1];
            leftPort["labelX"] = -36; leftPort["labelRightAligned"] = true;
            leftPort["outcomeReference"] = "root.nodes[{\"internalId\":" + id + "}].configuration.outcomes[{\"internalId\":1}]";
            var right = Ports("rightPorts", LinkReference(rejectedLinkId), false);
            var rightPort = (JObject)right[1];
            rightPort["labelX"] = 36;
            rightPort["outcomeReference"] = "root.nodes[{\"internalId\":" + id + "}].configuration.outcomes[{\"internalId\":2}]";
            var ui = Obj("y", y, "icon", "decisionStep", "topPorts", Ports("topPorts", LinkReference(approvedLinkId - 1), true),
                "leftPorts", left, "bottomPorts", Ports("bottomPorts", null, false), "rightPorts", right,
                "template", "DecisionStep", "componentId", 40009);
            var taskOne = "root.nodes[{\"internalId\":" + taskId + "}].configuration.outcomes[{\"internalId\":1}]";
            var taskTwo = "root.nodes[{\"internalId\":" + taskId + "}].configuration.outcomes[{\"internalId\":2}]";
            var decisionOne = "root.nodes[{\"internalId\":" + id + "}].configuration.outcomes[{\"internalId\":1}]";
            var decisionTwo = "root.nodes[{\"internalId\":" + id + "}].configuration.outcomes[{\"internalId\":2}]";
            var firstExpression = Obj("outcomeReference", taskOne, "activityReference", "root.nodes[{\"internalId\":3}]",
                "directive", "k2-task-expression", "internalId", 1, "componentId", 80009);
            var secondExpression = Obj("outcomeReference", taskTwo, "activityReference", "root.nodes[{\"internalId\":3}]",
                "directive", "k2-task-expression", "internalId", 1, "componentId", 80009);
            var secondStatement = Obj("IfExpressions", Arr(Obj("expressions", Arr(secondExpression), "directive", "k2-group-expression", "internalId", 1, "componentId", 80004)),
                "thenStatements", Arr(Obj("linkedOutcomeReferences", Arr(decisionTwo), "directive", "k2-outcome-statement", "internalId", 1, "componentId", 80006)),
                "directive", "k2-if-then-else-statement", "internalId", 1, "componentId", 80002);
            var statement = Obj("IfExpressions", Arr(Obj("expressions", Arr(firstExpression), "directive", "k2-group-expression", "internalId", 1, "componentId", 80004)),
                "thenStatements", Arr(Obj("linkedOutcomeReferences", Arr(decisionOne), "directive", "k2-outcome-statement", "internalId", 1, "componentId", 80006)),
                "elseStatements", Arr(secondStatement), "directive", "k2-if-then-else-statement", "internalId", 1, "componentId", 80002);
            var configuration = Obj(
                "outcomes", Arr(
                    Obj("title", "Approved", "originalTitle", "Approved", "linkedOutcomeReference", taskOne, "internalId", 1, "componentId", 30020),
                    Obj("title", "Rejected", "originalTitle", "Rejected", "linkedOutcomeReference", taskTwo, "internalId", 2, "componentId", 30020)),
                "outcomeRule", Obj("statements", Arr(statement), "componentId", 80101),
                "outcomesEventReference", "root.nodes[{\"internalId\":" + taskId + "}].children[{\"internalId\":1}]",
                "deadline", Deadline(), "priority", 1, "decisionOptionType", 1, "isDecisionOutcomeCheckBoxChecked", true,
                "exceptionSettings", Obj("logException", true, "componentId", 50012), "componentId", 40014);
            return Obj("ui", ui, "configuration", configuration, "systemName", "Decision", "title", "Decision", "internalId", id, "componentId", 40000);
        }

        private static JObject MatrixDecisionActivity(int id, int y)
        {
            var bottom = Ports("bottomPorts", LinkReference(4), false);
            ((JObject)bottom[1])["labelY"] = 72;
            ((JObject)bottom[1])["outcomeReference"] = "root.nodes[{\"internalId\":" + id + "}].configuration.outcomes[{\"internalId\":1}]";
            var right = Ports("rightPorts", LinkReference(5), false);
            ((JObject)right[1])["labelX"] = 36;
            ((JObject)right[1])["outcomeReference"] = "root.nodes[{\"internalId\":" + id + "}].configuration.outcomes[{\"internalId\":2}]";
            var first = "root.nodes[{\"internalId\":" + id + "}].configuration.outcomes[{\"internalId\":1}]";
            var second = "root.nodes[{\"internalId\":" + id + "}].configuration.outcomes[{\"internalId\":2}]";
            var expression = Obj(
                "leftExpression", Endpoint(DataFieldExpression(2)), "logicalOperator", "equals", "rightExpression", Endpoint(LiteralExpression("true")),
                "directive", "k2-simple-expression", "internalId", 1, "componentId", 80000);
            var statement = Obj(
                "IfExpressions", Arr(Obj("expressions", Arr(expression), "directive", "k2-group-expression", "internalId", 1, "componentId", 80004)),
                "thenStatements", Arr(Obj("linkedOutcomeReferences", Arr(first), "directive", "k2-outcome-statement", "internalId", 1, "componentId", 80006)),
                "elseStatements", Arr(Obj("linkedOutcomeReferences", Arr(second), "directive", "k2-outcome-statement", "internalId", 1, "componentId", 80006)),
                "directive", "k2-if-then-else-statement", "internalId", 1, "componentId", 80002);
            var configuration = Obj(
                "outcomes", Arr(
                    Obj("title", "Approver Found", "originalTitle", "Approver Found", "internalId", 1, "componentId", 30020),
                    Obj("title", "No More Approvers", "originalTitle", "No More Approvers", "internalId", 2, "componentId", 30020)),
                "outcomeRule", Obj("statements", Arr(statement), "componentId", 80101),
                "deadline", Deadline(), "priority", 1, "decisionOptionType", 1, "isDecisionOutcomeCheckBoxChecked", true,
                "exceptionSettings", Obj("logException", true, "componentId", 50012), "componentId", 40014);
            return Obj("ui", Obj("y", y, "icon", "decisionStep", "topPorts", Ports("topPorts", LinkReference(3), true),
                    "leftPorts", Ports("leftPorts", null, true), "bottomPorts", bottom, "rightPorts", right, "template", "DecisionStep", "componentId", 40009),
                "configuration", configuration, "systemName", "Approval Matrix Decision", "title", "Approval Matrix Decision", "customTitle", true, "internalId", id, "componentId", 40000);
        }

        private static JObject Link(int id, int from, int fromY, int to, int toY)
        {
            var middle = (fromY + toY) / 2;
            var path = "0," + (fromY + 28) + ",0," + (fromY + 48) + ",0," + middle + ",0," + middle + ",0," + (toY - 48) + ",0," + (toY - 28);
            return Obj("fromInternalId", from, "toInternalId", to,
                "ui", Obj("fromPortId", "bottomPorts_1", "toPortId", "topPorts_1", "path", path, "template", "DefaultLine"),
                "configuration", Component(40013), "systemName", id == 1 ? "DefaultLine" : "DefaultLine " + (id - 1), "internalId", id, "componentId", 50002);
        }

        private static JObject SideLink(int id, int from, int to, bool left, string title, int toX)
        {
            var middle = toX / 2;
            var path = left
                ? "-28,504,-48,504," + middle + ",504," + middle + ",504," + (toX + 48) + ",504," + (toX + 28) + ",504"
                : "28,504,48,504," + middle + ",504," + middle + ",504," + (toX - 48) + ",504," + (toX - 28) + ",504";
            var outcomeId = left ? 1 : 2;
            return Obj("fromInternalId", from, "toInternalId", to,
                "ui", Obj("fromPortId", left ? "leftPorts_1" : "rightPorts_1", "toPortId", left ? "rightPorts_1" : "leftPorts_1", "path", path, "showLabel", true),
                "configuration", Obj("associatedOutcomeReference", "root.nodes[{\"internalId\":4}].configuration.outcomes[{\"internalId\":" + outcomeId + "}]", "componentId", 40013),
                "systemName", "DefaultLine " + (id - 1), "title", title, "customTitle", true, "internalId", id, "componentId", 50002);
        }

        private static JObject HorizontalOutcomeLink(int id, int from, int to, int y, string title, int outcomeId)
        {
            var path = "28," + y + ",48," + y + ",126," + y + ",126," + y + ",204," + y + ",224," + y;
            return Obj("fromInternalId", from, "toInternalId", to,
                "ui", Obj("fromPortId", "rightPorts_1", "toPortId", "leftPorts_1", "path", path, "showLabel", true),
                "configuration", Obj("associatedOutcomeReference", "root.nodes[{\"internalId\":" + from + "}].configuration.outcomes[{\"internalId\":" + outcomeId + "}]", "componentId", 40013),
                "systemName", "DefaultLine " + (id - 1), "title", title, "customTitle", true, "internalId", id, "componentId", 50002);
        }

        private static JObject LoopOutcomeLink(int id, int from, int to, int fromY, int toY, string title, int outcomeId)
        {
            var path = "-28," + fromY + ",-48," + fromY + ",-140," + fromY + ",-140," + toY + ",-48," + toY + ",-28," + toY;
            return Obj("fromInternalId", from, "toInternalId", to,
                "ui", Obj("fromPortId", "leftPorts_1", "toPortId", "leftPorts_1", "path", path, "showLabel", true),
                "configuration", Obj("associatedOutcomeReference", "root.nodes[{\"internalId\":" + from + "}].configuration.outcomes[{\"internalId\":" + outcomeId + "}]", "componentId", 40013),
                "systemName", "DefaultLine " + (id - 1), "title", title, "customTitle", true, "internalId", id, "componentId", 50002);
        }

        private static JArray Ports(string prefix, string linkReference, bool incoming)
        {
            var result = Arr();
            for (var i = 0; i < 3; i++)
            {
                var port = Obj("portId", prefix + "_" + i, "internalId", i + 1, "componentId", 40012);
                if (i == 1 && linkReference != null) port[incoming ? "incomingLinkReferences" : "outgoingLinkReferences"] = Arr(linkReference);
                result.Add(port);
            }
            return result;
        }

        private static JObject ActivityConfiguration(bool logException)
        {
            return Obj("deadline", Deadline(), "expectedDuration", Component(30026), "priority", 1,
                "exceptionSettings", logException ? Obj("logException", true, "componentId", 50012) : Component(50012), "componentId", 40001);
        }

        private static JObject Deadline()
        {
            var email = Obj("from", Component(10008), "subject", Component(10008), "body", Component(10008),
                "exceptionSettings", Component(50012), "componentId", 30006);
            var emailAction = Obj("repeatDays", Component(10008), "repeatHours", Component(10008),
                "repeatMinutes", Component(10008), "repeatSeconds", Component(10008), "repeatAmount", Component(10008), "emailConfiguration", email);
            var smoAction = Obj("smartObjectReference", Component(10008), "smartObjectMethodReference", Component(10008),
                "smartObjectIdentifierReference", Component(10008), "filter", Component(80016), "listOptions", Component(50011));
            var repetition = Obj("repeatDays", Component(10008), "repeatHours", Component(10008),
                "repeatMinutes", Component(10008), "repeatSeconds", Component(10008), "repeatCount", Component(10008));
            return Obj("deadlineEmailAction", emailAction, "deadlineSmoAction", smoAction,
                "specificDate", Component(10008), "expressDays", Component(10008), "expressHours", Component(10008),
                "expressMinutes", Component(10008), "expressSeconds", Component(10008), "noDeadline", true,
                "dynamicWorkingHours", Component(10008), "repetition", repetition, "componentId", 30025);
        }

        private static JObject ProcessConfiguration(JArray dataFields, JArray itemReferences)
        {
            var expression = Obj("leftExpression", Endpoint(), "logicalOperator", "equals", "rightExpression", Endpoint(),
                "directive", "k2-simple-expression", "internalId", 1, "componentId", 80000);
            var group = Obj("expressions", Arr(expression), "directive", "k2-group-expression", "internalId", 1, "componentId", 80004);
            var statement = Obj("IfExpressions", Arr(group), "thenStatements", Arr(OutcomeStatement(1)),
                "elseStatements", Arr(OutcomeStatement(2)), "internalId", 1, "componentId", 80002);
            return Obj("processDefinitions", itemReferences.Count == 0 ? Arr() : Arr(Component(20000)), "dataFields", dataFields, "itemReferences", itemReferences, "processPriority", 1,
                "exceptionSettings", Obj("logException", true, "componentId", 50012),
                "startRule", Obj("statements", Arr(statement), "componentId", 80107),
                "outcomes", Arr(), "eventPlatformConfiguration", Component(90000));
        }

        private static JObject ExternalSmartObject(SmartObjectDescriptor smartObject, bool includeRead, int externalId)
        {
            var inputs = new JArray();
            foreach (var input in smartObject.Inputs)
            {
                var value = Obj("systemName", input.SystemName, "type", input.Type, "displayName", input.DisplayName);
                if (!string.IsNullOrWhiteSpace(input.Description)) value["description"] = input.Description;
                value["state"] = 1; value["internalId"] = input.InternalId; value["componentId"] = 70006;
                if (input.IsRequired) value.AddFirst(new JProperty("isRequired", true));
                inputs.Add(value);
            }
            var method = Obj("inputs", inputs, "returns", Arr(), "systemName", smartObject.MethodSystemName,
                "type", smartObject.MethodType, "displayName", smartObject.MethodDisplayName,
                "state", 1, "internalId", includeRead ? 2 : 1, "componentId", 70005);
            var methods = includeRead ? Arr(ReadMethod(smartObject), method) : Arr(method);
            return Obj("methods", methods, "systemName", smartObject.SystemName, "displayName", smartObject.DisplayName,
                "state", 1, "internalId", externalId, "componentId", 70002);
        }

        private static JObject ExternalApprovalMatrix(SmartObjectMethodDescriptor resolver, int externalId)
        {
            var inputs = new JArray();
            foreach (var input in resolver.Inputs) inputs.Add(Property(input));
            var returns = new JArray();
            foreach (var property in resolver.Returns) returns.Add(Property(property));
            var method = Obj("inputs", inputs, "returns", returns, "systemName", resolver.MethodSystemName,
                "type", resolver.MethodType, "displayName", resolver.MethodDisplayName,
                "state", 1, "internalId", 1, "componentId", 70005);
            return Obj("methods", Arr(method), "systemName", resolver.SystemName, "displayName", resolver.DisplayName,
                "state", 1, "internalId", externalId, "componentId", 70002);
        }

        private static JObject ReadMethod(SmartObjectDescriptor smartObject)
        {
            var returns = new JArray();
            foreach (var property in smartObject.ReadReturns)
                returns.Add(Property(property));
            return Obj("returns", returns, "systemName", smartObject.ReadMethodSystemName, "type", "read",
                "displayName", smartObject.ReadMethodDisplayName, "state", 1, "internalId", 1, "componentId", 70005);
        }

        private static JObject Property(SmartObjectInputDescriptor property)
        {
            var value = Obj("systemName", property.SystemName, "type", property.Type, "displayName", property.DisplayName);
            if (!string.IsNullOrWhiteSpace(property.Description)) value["description"] = property.Description;
            value["state"] = 1; value["internalId"] = property.InternalId; value["componentId"] = 70006;
            return value;
        }

        private static JObject ItemReference(SmartObjectDescriptor smartObject, SmartFormsIntegrationDescriptor form)
        {
            var properties = new JArray();
            foreach (var property in smartObject.ReadReturns)
                properties.Add(Obj("propertyReference", "root.externalReferenceDefinitions[{\"internalId\":1}].methods[{\"internalId\":1}].returns[{\"internalId\":" + property.InternalId + "}]",
                    "propertySystemName", property.SystemName, "internalId", property.InternalId, "componentId", 50013));
            var source = form.PrimaryItemReference;
            return Obj("systemName", smartObject.SystemName, "title", smartObject.DisplayName, "type", 1,
                "objectTypes", source["objectTypes"] ?? Arr(), "objectNames", source["objectNames"] ?? Arr(), "serviceTypes", source["serviceTypes"] ?? Arr(),
                "propertyReferences", properties,
                "methodReference", "root.externalReferenceDefinitions[{\"internalId\":1}].methods[{\"internalId\":1}]",
                "smartObjectReference", "root.externalReferenceDefinitions[{\"internalId\":1}]",
                "primary", true, "startMode", "smartForms", "internalId", 1, "componentId", 50008);
        }

        private static JObject EnvironmentField(string name, int id)
        {
            return Obj("name", name, "internalId", id, "componentId", 70001);
        }

        private static string EnvironmentFieldName(string value)
        {
            return value != null && value.StartsWith("$environment:", StringComparison.OrdinalIgnoreCase) ? value.Substring(13) : "From Address";
        }

        private static JObject SmartFormReference(SmartFormsIntegrationDescriptor form, int id)
        {
            return Obj("id", form.FormId, "name", form.SystemName, "title", form.DisplayName, "internalId", id, "componentId", 70004);
        }

        private static JObject SmartObjectServiceFunctions(int externalId)
        {
            return Obj("methods", Arr(
                    ExternalMethod("GetSmartObjectMethods", "list", "Get SmartObject Methods", 1),
                    ExternalMethod("GetSmartObjectMethodType", "read", "Get SmartObject Method Type", 2),
                    ExternalMethod("GetSmartObjectMethodProperties", "list", "Get SmartObject Method Properties", 3),
                    ExternalMethod("GetDefaultLoadMethod", "read", "Get Default Load Method", 4)),
                "systemName", "SmartObject_Service_Functions", "displayName", "SmartObject Service Functions",
                "description", "Allows the user to retrieve Service Instances and SmartObjects in K2",
                "state", 1, "internalId", externalId, "componentId", 70002);
        }

        private static JObject ExternalMethod(string systemName, string type, string displayName, int internalId)
        {
            return Obj("systemName", systemName, "type", type, "displayName", displayName,
                "state", 1, "internalId", internalId, "componentId", 70005);
        }

        private static JObject DataField(string title, int internalId, bool hidden, int scope)
        {
            return DataField(title, internalId, hidden, scope, 6);
        }

        private static JObject DataField(string title, int internalId, bool hidden, int scope, int type)
        {
            var result = Obj("type", type, "title", title, "isEmptyValue", true);
            if (hidden) result["hidden"] = true;
            if (scope != 0) result["scope"] = scope;
            result["internalId"] = internalId; result["componentId"] = 50000;
            return result;
        }

        private static JObject Mapping(JObject value, string tokenReference) { return Obj("value", value, "tokenReference", tokenReference, "componentId", 60043); }
        private static void AddControl(JObject controls, string name, JObject value) { controls[name] = Obj("value", value, "componentId", 60000); }
        private static JObject LiteralExpression(string value) { return Obj("smartFields", Arr(Obj("text", value, "internalId", 1, "componentId", 10004)), "componentId", 10008); }
        private static JObject MultiLineLiteralExpression(string value) { var result = LiteralExpression(value); result["multiLine"] = true; return result; }
        private static JObject DataFieldExpression(int id) { return Obj("smartFields", Arr(Obj("dataFieldReference", "root.configuration.dataFields[{\"internalId\":" + id + "}]", "internalId", 1, "componentId", 10000)), "componentId", 10008); }
        private static JObject ItemReferencePropertyExpression(int itemId, string itemTitle, string propertyTitle, int propertyId)
        {
            return Obj("smartFields", Arr(ItemReferencePropertyField(itemId, itemTitle, propertyTitle, propertyId, 1)), "componentId", 10008);
        }
        private static JObject ItemReferencePropertyField(int itemId, string itemTitle, string propertyTitle, int propertyId, int internalId)
        {
            return Obj(
                "externalReferencePropertyReference", "root.externalReferenceDefinitions[{\"internalId\":1}].methods[{\"internalId\":1}].returns[{\"internalId\":" + propertyId + "}]",
                "itemReferenceDefinitionReference", "root.configuration.itemReferences[{\"internalId\":" + itemId + "}]",
                "smartObjectPropertyReference", "root.configuration.itemReferences[{\"internalId\":" + itemId + "}].propertyReferences[{\"internalId\":" + propertyId + "}]",
                "customItemReferenceTitle", itemTitle, "title", propertyTitle, "customTitle", propertyTitle,
                "internalId", internalId, "componentId", 10011);
        }
        private static JObject EnvironmentExpression(string name, int id) { return Obj("smartFields", Arr(Obj("environmentFieldReference", "root.externalReferenceDefinitions[{\"internalId\":" + id + "}]", "title", name, "internalId", 1, "componentId", 10001)), "componentId", 10008); }
        private static JObject OriginatorExpression() { var value = Obj("smartFields", Arr(Obj("fieldName", "ProcessOriginatorEmail", "parentName", "Originator's", "title", "Email", "customTitle", "Originator", "internalId", 1, "componentId", 10009)), "type", "Originator", "componentId", 10008); return value; }
        private static JObject OriginatorUserExpression() { return Obj("smartFields", Arr(Obj("fieldName", "ProcessOriginatorFQN", "parentName", "Originator's", "title", "FQN", "customTitle", "Originator", "internalId", 1, "componentId", 10009)), "type", "User", "componentId", 10008); }
        private static JObject SmartFormExpression(int id) { return Obj("smartFields", Arr(Obj("smartFormFieldReference", "root.externalReferenceDefinitions[{\"internalId\":" + id + "}]", "internalId", 1, "componentId", 10006)), "componentId", 10008); }
        private static JObject ExternalExpression(string reference, string title) { return Obj("smartFields", Arr(Obj("id", reference, "title", title, "internalId", 1, "componentId", 10018)), "componentId", 10008); }
        private static JObject NumberExpression(int value) { return Obj("smartFields", Arr(Obj("value", value, "internalId", 1, "componentId", 10021)), "componentId", 10008); }
        private static JObject TaskParameterName(string name, bool serialNumber, string customTitle = null) { var result = LiteralExpression(name); if (serialNumber || customTitle != null) { ((JObject)((JArray)result["smartFields"])[0])["customTitle"] = serialNumber ? "Serial Number" : customTitle; result["type"] = "tworows"; } return result; }
        private static JObject TaskSerialNumber() { return Obj("smartFields", Arr(Obj("fieldName", "SerialNo", "parentName", "Task", "title", "Serial Number", "internalId", 1, "componentId", 10009)), "componentId", 10008); }
        private static int FindReturnId(SmartObjectDescriptor smartObject, string propertyName)
        {
            var property = smartObject.ReadReturns.FirstOrDefault(x => string.Equals(x.SystemName, propertyName, StringComparison.OrdinalIgnoreCase));
            if (property == null) throw new CliException("The request identifier is not returned by the SmartObject Read method: " + propertyName);
            return property.InternalId;
        }
        private static RequestStatusUpdateSettings CopyStatus(RequestStatusUpdateSettings source, string value, string name)
        {
            return new RequestStatusUpdateSettings
            {
                Name = name, SmartObject = source.SmartObject, Method = source.Method,
                IdentifierProperty = source.IdentifierProperty, IdentifierDataField = source.IdentifierDataField,
                StatusProperty = source.StatusProperty, StatusValue = value
            };
        }
        private static EmailSettings OutcomeEmail(EmailSettings source, string outcome)
        {
            return new EmailSettings
            {
                Name = outcome, From = source.From, To = source.To,
                Subject = "Corporate request " + outcome.ToLowerInvariant(),
                Body = "Your corporate workflow request has been " + outcome.ToLowerInvariant() + ".",
                Html = source.Html
            };
        }
        private static string OutcomeTitle(string action)
        {
            if (string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase)) return "Approved";
            if (string.Equals(action, "Reject", StringComparison.OrdinalIgnoreCase)) return "Rejected";
            return action;
        }
        private static JArray DecisionTrackedReferences()
        {
            return DecisionTrackedReferences(3, 4, 4, 5);
        }

        private static JArray DecisionTrackedReferences(int taskId, int decisionId, int approvedLinkId, int rejectedLinkId)
        {
            var task = "root.nodes[{\"internalId\":" + taskId + "}]";
            var decision = "root.nodes[{\"internalId\":" + decisionId + "}]";
            return Arr(
                Tracked(1, task + ".children[{\"internalId\":1}].configuration.actions[{\"internalId\":1}]", task + ".configuration.outcomes[{\"internalId\":1}].linkedActionReference"),
                Tracked(2, task + ".children[{\"internalId\":1}].configuration.actions[{\"internalId\":2}]", task + ".configuration.outcomes[{\"internalId\":2}].linkedActionReference"),
                Tracked(3, task + ".configuration.outcomes[{\"internalId\":1}]",
                    task + ".children[{\"internalId\":1}].configuration.votingRuleConsensusSelectedOutcomeReference",
                    decision + ".configuration.outcomes[{\"internalId\":1}].linkedOutcomeReference",
                    decision + ".configuration.outcomeRule.statements[{\"internalId\":1}].IfExpressions[{\"internalId\":1}].expressions[{\"internalId\":1}].outcomeReference"),
                Tracked(4, task + ".children[{\"internalId\":1}]", decision + ".configuration.outcomesEventReference"),
                Tracked(5, decision + ".configuration.outcomes[{\"internalId\":1}]",
                    "root.links[{\"internalId\":" + approvedLinkId + "}].configuration.associatedOutcomeReference", decision + ".ui.leftPorts[{\"internalId\":2}].outcomeReference"),
                Tracked(6, task + ".configuration.outcomes[{\"internalId\":2}]",
                    decision + ".configuration.outcomes[{\"internalId\":2}].linkedOutcomeReference",
                    decision + ".configuration.outcomeRule.statements[{\"internalId\":1}].elseStatements[{\"internalId\":1}].IfExpressions[{\"internalId\":1}].expressions[{\"internalId\":1}].outcomeReference"),
                Tracked(7, decision + ".configuration.outcomes[{\"internalId\":2}]",
                    "root.links[{\"internalId\":" + rejectedLinkId + "}].configuration.associatedOutcomeReference", decision + ".ui.rightPorts[{\"internalId\":2}].outcomeReference"),
                Tracked(8, task,
                    decision + ".configuration.outcomeRule.statements[{\"internalId\":1}].IfExpressions[{\"internalId\":1}].expressions[{\"internalId\":1}].activityReference",
                    decision + ".configuration.outcomeRule.statements[{\"internalId\":1}].elseStatements[{\"internalId\":1}].IfExpressions[{\"internalId\":1}].expressions[{\"internalId\":1}].activityReference"));
        }
        private static JObject Tracked(int id, string definitionPath, params string[] references)
        {
            return Obj("trackedObjectDefinitionPath", definitionPath, "references", new JArray(references), "internalId", id, "componentId", 70000);
        }
        private static string LinkReference(int id) { return "root.links[{\"internalId\":" + id + "}]"; }
        private static string ActivitySystemName(string title, JObject child, int id)
        {
            if (child == null) return title;
            switch ((int)child["componentId"])
            {
                case 30011: return "SmartObject Method";
                case 30004: return "Send Email";
                case 30009: return "Task";
                default: return title;
            }
        }
        private static string Default(string value, string fallback) { return string.IsNullOrWhiteSpace(value) ? fallback : value; }
        private static JObject Endpoint() { return Endpoint(Component(10008)); }
        private static JObject Endpoint(JObject value) { return Obj("value", value, "directive", "k2-endpoint", "componentId", 80003); }
        private static JObject OutcomeStatement(int id) { return Obj("linkedOutcomeReferences", Arr("root.configuration.outcomes[{\"internalId\":" + id + "}]"), "directive", "k2-outcome-statement", "internalId", 1, "componentId", 80006); }
        private static JObject Component(int id) { return Obj("componentId", id); }

        private static JObject Obj(params object[] pairs)
        {
            var result = new JObject();
            for (var i = 0; i < pairs.Length; i += 2) result.Add((string)pairs[i], pairs[i + 1] == null ? JValue.CreateNull() : JToken.FromObject(pairs[i + 1]));
            return result;
        }

        private static JArray Arr(params object[] values)
        {
            var result = new JArray();
            foreach (var value in values) result.Add(value == null ? JValue.CreateNull() : JToken.FromObject(value));
            return result;
        }
    }
}
