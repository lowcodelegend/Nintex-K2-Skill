using System;
using System.IO;
using System.Linq;
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

        public static string BuildRequestApproval(WorkflowSettings workflow, SmartObjectDescriptor smartObject, SmartFormsIntegrationDescriptor smartForm)
        {
            var update = workflow.RequestStatusUpdate;
            var integrated = smartForm != null;
            var smartObjectExternalId = integrated ? 1 : 2;
            var environmentExternalId = integrated ? 3 : 0;
            var formExternalId = integrated ? 4 : 0;
            var nodes = Arr(
                Activity("Start", 1, 56, "StartStep", null, true, false, null, LinkReference(1), null, null),
                Activity(Default(update.Name, "Set Request Status"), 2, 280, "SmartWizardStep", "smartObjectWizardStep", false, false,
                    LinkReference(1), LinkReference(2), SmartObjectEvent(Default(update.Name, "Set Request Status"), update, smartObject, smartObjectExternalId, integrated), null),
                Activity(workflow.Email.Name, 3, 504, "DefaultStep", "emailStep", false, false,
                    LinkReference(2), LinkReference(3), EmailEvent(workflow.Email, environmentExternalId), null),
                Activity(workflow.UserTask.Name, 4, 728, "DefaultStep", "userTaskStep", false, false,
                    LinkReference(3), LinkReference(4), UserTaskEvent(workflow.UserTask, update.IdentifierDataField, 4, smartForm, formExternalId, integrated ? workflow.SmartForms.TaskState : null), UserTaskActivityConfiguration(workflow.UserTask, 4)),
                Activity("End", 5, 952, "EndStep", "endStep", false, true, LinkReference(4), null, null, null));
            var root = Obj(
                "nodes", nodes,
                "links", Arr(Link(1, 1, 56, 2, 280), Link(2, 2, 280, 3, 504), Link(3, 3, 504, 4, 728), Link(4, 4, 728, 5, 952)),
                "configuration", ProcessConfiguration(integrated ? Arr() : Arr(DataField(update.IdentifierDataField, 1, false, 0)), integrated ? Arr(ItemReference(smartObject, smartForm)) : Arr()),
                "ui", Component(50004),
                "externalReferenceDefinitions", integrated
                    ? Arr(ExternalSmartObject(smartObject, true, 1), SmartObjectServiceFunctions(2), EnvironmentField(EnvironmentFieldName(workflow.Email.From), 3), SmartFormReference(smartForm, 4))
                    : Arr(SmartObjectServiceFunctions(1), ExternalSmartObject(smartObject, false, 2)),
                "trackedReferences", Arr(), "systemName", workflow.Name, "title", workflow.Name, "componentId", 50001);
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
            AddControl(controls, "kdReturnOnlyKeys", LiteralExpression("true"));
            AddControl(controls, "SmartObject", LiteralExpression("radReference"));
            AddControl(controls, "kdDefaultLoad", LiteralExpression(smartObject.DefaultLoadMethod));
            AddControl(controls, "cbbMethods", ExternalExpression(methodReference));
            AddControl(controls, "radReference", LiteralExpression("radReference"));
            AddControl(controls, "kdReturnDefaultLoadProperties", LiteralExpression("true"));
            AddControl(controls, "spSmartObject", ExternalExpression(objectReference));
            AddControl(controls, "cbxExternalSystem", LiteralExpression("false"));
            AddControl(controls, "kdDelete", LiteralExpression("delete"));
            AddControl(controls, "kdList", LiteralExpression("list"));
            AddControl(controls, "kdMethodType", LiteralExpression("update"));
            AddControl(controls, "kdMethodPropertiesType", LiteralExpression("return"));
            AddControl(controls, "cbxContinueOnError", LiteralExpression("false"));
            AddControl(controls, "DefaultLoad", LiteralExpression(smartObject.DefaultLoadMethod));
            AddControl(controls, "kdExecute", LiteralExpression("execute"));
            AddControl(controls, "kdRead", LiteralExpression("read"));
            AddControl(controls, "kdTrue", LiteralExpression("true"));
            AddControl(controls, "cbxEnableRequiredFieldsValidation", LiteralExpression("true"));
            AddControl(controls, "kdCreate", LiteralExpression("create"));
            AddControl(controls, "kdDefaultMethodType", LiteralExpression("read"));
            controls["pmInputs"] = Obj("values", values, "value", Component(10008), "filterValue", Component(80016), "listOptions", Component(50011), "componentId", 60000);
            return Obj(
                "wizardId", 3176,
                "wizardDefinition", SmartObjectWizardDefinition(smartObject),
                "ui", Obj("icon", "smartObjectWizardStep", "template", "SmartWizardStep", "componentId", 30027),
                "configuration", Obj("controlValues", controls, "exceptionSettings", Component(50012), "componentId", 30012),
                "systemName", title, "title", title, "internalId", 1, "componentId", 30011);
        }

        private static JObject SmartObjectWizardDefinition(SmartObjectDescriptor smartObject)
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

        private static JObject UserTaskEvent(UserTaskSettings task, string identifierDataField, int nodeId, SmartFormsIntegrationDescriptor smartForm, int formExternalId, string taskState)
        {
            var actions = new JArray();
            for (var i = 0; i < task.Actions.Count; i++)
            {
                var reference = "root.nodes[{\"internalId\":" + nodeId + "}].children[{\"internalId\":1}].configuration.actions[{\"internalId\":" + (i + 1) + "}]";
                actions.Add(Obj("alwaysVisible", true, "continueWorkflow", true, "actionTitle", task.Actions[i],
                    "internalId", i + 1, "componentId", 30019, "oldReference", reference));
            }
            var destinationItems = new JArray();
            for (var i = 0; i < task.Assignees.Count; i++)
            {
                var expression = string.Equals(task.Assignees[i], "$originatorManager", StringComparison.OrdinalIgnoreCase)
                    ? OriginatorManagerExpression() : LiteralExpression(task.Assignees[i]);
                expression["type"] = "User"; expression["isDynamic"] = true; expression["internalId"] = i + 1;
                destinationItems.Add(expression);
            }
            var destinationTitle = task.Assignees.Count == 1 && string.Equals(task.Assignees[0], "$originatorManager", StringComparison.OrdinalIgnoreCase)
                ? "Originator's Manager" : string.Join(", ", task.Assignees.ToArray());
            var destinations = Arr(Obj("title", destinationTitle, "destinationItems", destinationItems,
                "isRecipient", true, "internalId", 1, "componentId", 80010));
            var parameters = smartForm == null ? Arr(
                    Obj("name", TaskParameterName("SN", true), "value", TaskSerialNumber(), "internalId", 1, "componentId", 30018),
                    Obj("name", TaskParameterName(task.RequestIdParameter, false), "value", DataFieldExpression(1), "internalId", 2, "componentId", 30018))
                : Arr(
                    Obj("name", TaskParameterName("SerialNo", true), "value", TaskSerialNumber(), "internalId", 1, "componentId", 30018),
                    Obj("name", TaskParameterName("_state", false, "State"), "value", LiteralExpression(taskState), "internalId", 2, "componentId", 30018));
            var notification = Obj("from", Component(10008), "cc", Arr(), "bcc", Arr(), "subject", Component(10008),
                "body", Component(10008), "exceptionSettings", Component(50012), "componentId", 30006);
            var configuration = Obj(
                "instruction", MultiLineLiteralExpression(task.Instructions), "actions", actions, "actionStatementRuleType", 1,
                "formConfiguration", smartForm == null
                    ? Obj("url", LiteralExpression(task.FormUrl), "parameters", parameters, "componentId", 30015)
                    : Obj("url", SmartFormExpression(formExternalId), "parameters", parameters, "componentId", 30016),
                "destinationSets", destinations, "allRecipients", true, "slots", NumberExpression(1),
                "votingResolveGroupsToIndividuals", true, "timeLine", Deadline(), "reminder", Obj("deadlines", Arr()),
                "emailConfiguration", notification, "sendNotification", false,
                "exceptionSettings", Component(50012), "componentId", 30010);
            return Obj("wizardId", 3000, "ui", Obj("icon", "userTaskStep", "template", "DefaultStep"),
                "configuration", configuration, "systemName", task.Name, "title", task.Name, "internalId", 1, "componentId", 30009);
        }

        private static JObject UserTaskActivityConfiguration(UserTaskSettings task, int nodeId)
        {
            var configuration = ActivityConfiguration(true);
            var outcomes = new JArray();
            for (var i = 0; i < task.Actions.Count; i++)
            {
                var reference = "root.nodes[{\"internalId\":" + nodeId + "}].children[{\"internalId\":1}].configuration.actions[{\"internalId\":" + (i + 1) + "}]";
                outcomes.Add(Obj("title", task.Actions[i], "originalTitle", task.Actions[i], "linkedActionReference", reference,
                    "actionRule", Component(80004), "internalId", i + 1, "componentId", 30020));
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

        private static JObject Link(int id, int from, int fromY, int to, int toY)
        {
            var middle = (fromY + toY) / 2;
            var path = "0," + (fromY + 28) + ",0," + (fromY + 48) + ",0," + middle + ",0," + middle + ",0," + (toY - 48) + ",0," + (toY - 28);
            return Obj("fromInternalId", from, "toInternalId", to,
                "ui", Obj("fromPortId", "bottomPorts_1", "toPortId", "topPorts_1", "path", path, "template", "DefaultLine"),
                "configuration", Component(40013), "systemName", id == 1 ? "DefaultLine" : "DefaultLine " + (id - 1), "internalId", id, "componentId", 50002);
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
            var result = Obj("type", 6, "title", title, "isEmptyValue", true);
            if (hidden) result["hidden"] = true;
            if (scope != 0) result["scope"] = scope;
            result["internalId"] = internalId; result["componentId"] = 50000;
            return result;
        }

        private static JObject Mapping(JObject value, string tokenReference) { return Obj("value", value, "tokenReference", tokenReference, "componentId", 60043); }
        private static void AddControl(JObject controls, string name, JObject value) { controls[name] = Obj("value", value, "filterValue", Component(80016), "listOptions", Component(50011), "componentId", 60000); }
        private static JObject LiteralExpression(string value) { return Obj("smartFields", Arr(Obj("text", value, "internalId", 1, "componentId", 10004)), "componentId", 10008); }
        private static JObject MultiLineLiteralExpression(string value) { var result = LiteralExpression(value); result["multiLine"] = true; return result; }
        private static JObject DataFieldExpression(int id) { return Obj("smartFields", Arr(Obj("dataFieldReference", "root.configuration.dataFields[{\"internalId\":" + id + "}]", "internalId", 1, "componentId", 10000)), "componentId", 10008); }
        private static JObject ItemReferencePropertyExpression(int itemId, string itemTitle, string propertyTitle, int propertyId)
        {
            return Obj("smartFields", Arr(Obj(
                "externalReferencePropertyReference", "root.externalReferenceDefinitions[{\"internalId\":1}].methods[{\"internalId\":1}].returns[{\"internalId\":" + propertyId + "}]",
                "itemReferenceDefinitionReference", "root.configuration.itemReferences[{\"internalId\":" + itemId + "}]",
                "smartObjectPropertyReference", "root.configuration.itemReferences[{\"internalId\":" + itemId + "}].propertyReferences[{\"internalId\":" + propertyId + "}]",
                "customItemReferenceTitle", itemTitle, "title", propertyTitle, "customTitle", propertyTitle,
                "internalId", 1, "componentId", 10011)), "componentId", 10008);
        }
        private static JObject EnvironmentExpression(string name, int id) { return Obj("smartFields", Arr(Obj("environmentFieldReference", "root.externalReferenceDefinitions[{\"internalId\":" + id + "}]", "title", name, "internalId", 1, "componentId", 10001)), "componentId", 10008); }
        private static JObject OriginatorExpression() { var value = Obj("smartFields", Arr(Obj("fieldName", "ProcessOriginatorEmail", "parentName", "Originator's", "title", "Email", "customTitle", "Originator", "internalId", 1, "componentId", 10009)), "type", "Originator", "componentId", 10008); return value; }
        private static JObject OriginatorManagerExpression() { return Obj("smartFields", Arr(Obj("fieldName", "ProcessOriginatorManager", "parentName", "Originator's", "title", "Manager", "customTitle", "Originator's Manager", "internalId", 1, "componentId", 10009)), "type", "User", "componentId", 10008); }
        private static JObject SmartFormExpression(int id) { return Obj("smartFields", Arr(Obj("smartFormFieldReference", "root.externalReferenceDefinitions[{\"internalId\":" + id + "}]", "internalId", 1, "componentId", 10006)), "componentId", 10008); }
        private static JObject ExternalExpression(string reference) { return Obj("smartFields", Arr(Obj("id", reference, "title", reference, "internalId", 1, "componentId", 10018)), "componentId", 10008); }
        private static JObject NumberExpression(int value) { return Obj("smartFields", Arr(Obj("value", value, "internalId", 1, "componentId", 10021)), "componentId", 10008); }
        private static JObject TaskParameterName(string name, bool serialNumber, string customTitle = null) { var result = LiteralExpression(name); if (serialNumber || customTitle != null) { ((JObject)((JArray)result["smartFields"])[0])["customTitle"] = serialNumber ? "Serial Number" : customTitle; result["type"] = "tworows"; } return result; }
        private static JObject TaskSerialNumber() { return Obj("smartFields", Arr(Obj("fieldName", "SerialNo", "parentName", "Task", "title", "Serial Number", "internalId", 1, "componentId", 10009)), "componentId", 10008); }
        private static int FindReturnId(SmartObjectDescriptor smartObject, string propertyName)
        {
            var property = smartObject.ReadReturns.FirstOrDefault(x => string.Equals(x.SystemName, propertyName, StringComparison.OrdinalIgnoreCase));
            if (property == null) throw new CliException("The request identifier is not returned by the SmartObject Read method: " + propertyName);
            return property.InternalId;
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
        private static JObject Endpoint() { return Obj("value", Component(10008), "directive", "k2-endpoint", "componentId", 80003); }
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
