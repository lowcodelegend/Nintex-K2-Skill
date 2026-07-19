using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace K2WorkflowCli
{
    internal static class WorkflowJsonBuilder
    {
        public static string BuildStartEnd(string name)
        {
            const string reference = "root.links[{\"internalId\":1}]";
            var link = Obj(
                "fromInternalId", 1, "toInternalId", 2,
                "ui", Obj("fromPortId", "bottomPorts_1", "toPortId", "topPorts_1", "path", "0,84,0,104,0,168,0,168,0,260,0,280", "template", "DefaultLine"),
                "configuration", Component(40013), "systemName", "DefaultLine", "internalId", 1, "componentId", 50002);
            var root = Obj(
                "nodes", Arr(Activity("Start", 1, 56, "StartStep", null, true, reference, false), Activity("End", 2, 280, "EndStep", "endStep", false, reference, true)),
                "links", Arr(link), "configuration", ProcessConfiguration(), "ui", Component(50004),
                "externalReferenceDefinitions", Arr(), "trackedReferences", Arr(),
                "systemName", name, "title", name, "componentId", 50001);
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

        private static JObject Activity(string title, int id, int y, string template, string icon, bool start, string linkReference, bool incoming)
        {
            var ui = Obj(
                "y", y, "topPorts", Ports("topPorts", incoming ? linkReference : null, true),
                "leftPorts", Ports("leftPorts", null, true), "bottomPorts", Ports("bottomPorts", start ? linkReference : null, false),
                "rightPorts", Ports("rightPorts", null, false), "template", template, "componentId", 40009);
            if (icon != null) ui.AddFirst(new JProperty("icon", icon));
            var node = Obj("ui", ui, "configuration", ActivityConfiguration(incoming), "systemName", title,
                "title", title, "internalId", id, "componentId", 40000);
            if (start) node.AddFirst(new JProperty("isStartActivity", true));
            if (incoming) node["customTitle"] = true;
            return node;
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
            var email = Obj("from", Component(10008), "subject", Component(10008), "body", Component(10008),
                "exceptionSettings", Component(50012), "componentId", 30006);
            var emailAction = Obj("repeatDays", Component(10008), "repeatHours", Component(10008),
                "repeatMinutes", Component(10008), "repeatSeconds", Component(10008), "repeatAmount", Component(10008),
                "emailConfiguration", email);
            var smoAction = Obj("smartObjectReference", Component(10008), "smartObjectMethodReference", Component(10008),
                "smartObjectIdentifierReference", Component(10008), "filter", Component(80016), "listOptions", Component(50011));
            var repetition = Obj("repeatDays", Component(10008), "repeatHours", Component(10008),
                "repeatMinutes", Component(10008), "repeatSeconds", Component(10008), "repeatCount", Component(10008));
            var deadline = Obj("deadlineEmailAction", emailAction, "deadlineSmoAction", smoAction,
                "specificDate", Component(10008), "expressDays", Component(10008), "expressHours", Component(10008),
                "expressMinutes", Component(10008), "expressSeconds", Component(10008), "noDeadline", true,
                "dynamicWorkingHours", Component(10008), "repetition", repetition, "componentId", 30025);
            return Obj("deadline", deadline, "expectedDuration", Component(30026), "priority", 1,
                "exceptionSettings", logException ? Obj("logException", true, "componentId", 50012) : Component(50012),
                "componentId", 40001);
        }

        private static JObject ProcessConfiguration()
        {
            var expression = Obj("leftExpression", Endpoint(), "logicalOperator", "equals", "rightExpression", Endpoint(),
                "directive", "k2-simple-expression", "internalId", 1, "componentId", 80000);
            var group = Obj("expressions", Arr(expression), "directive", "k2-group-expression", "internalId", 1, "componentId", 80004);
            var statement = Obj("IfExpressions", Arr(group), "thenStatements", Arr(OutcomeStatement(1)),
                "elseStatements", Arr(OutcomeStatement(2)), "internalId", 1, "componentId", 80002);
            return Obj("processDefinitions", Arr(), "dataFields", Arr(), "itemReferences", Arr(), "processPriority", 1,
                "exceptionSettings", Obj("logException", true, "componentId", 50012),
                "startRule", Obj("statements", Arr(statement), "componentId", 80107),
                "outcomes", Arr(), "eventPlatformConfiguration", Component(90000));
        }

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
