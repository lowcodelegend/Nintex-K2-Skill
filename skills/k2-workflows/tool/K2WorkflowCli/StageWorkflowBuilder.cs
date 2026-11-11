using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace K2WorkflowCli
{
    internal static partial class WorkflowJsonBuilder
    {
        public static string BuildStageTask(WorkflowSettings workflow)
        {
            var settings = workflow.StageTask;
            var graph = new StageGraph(workflow.Name, Arr(DataField(settings.RequestIdField, 1, false, 0), DataField(settings.OutcomeField, 2, false, 0)));
            var start = graph.Add("Start", 0, 56, null, true, false);
            var taskId = graph.NextId;
            var task = graph.Add(workflow.UserTask.Name, 0, 224,
                UserTaskEvent(workflow.UserTask, settings.RequestIdField, taskId, null, null, 0, null, 0), false, false);
            task["configuration"] = UserTaskActivityConfiguration(workflow.UserTask, taskId, true);
            foreach (var outcome in task["configuration"]["outcomes"].Children<JObject>()) outcome["originalTitle"] = outcome["title"].DeepClone();
            graph.Connect(start, task);
            var end = graph.Add("End", -560, 224, null, false, true);
            JObject previous = task;
            var previousOutcome = 0;
            for (var index = 0; index < workflow.UserTask.Actions.Count; index++)
            {
                var action = workflow.UserTask.Actions[index];
                var decision = graph.Decision("Action is " + action, 0, 392 + index * 224, 2, action);
                var taskReference = NodeReference(taskId);
                var actionReference = taskReference + ".configuration.outcomes[{\"internalId\":" + (index + 1) + "}]";
                var expression = Obj("outcomeReference", actionReference, "activityReference", taskReference,
                    "directive", "k2-task-expression", "internalId", 1, "componentId", 80009);
                decision["configuration"]["outcomeRule"]["statements"][0]["IfExpressions"][0]["expressions"][0] = expression;
                decision["configuration"]["outcomesEventReference"] = taskReference + ".children[{\"internalId\":1}]";
                graph.Connect(previous, decision, previousOutcome);
                var result = graph.Add("Return " + action, -280, 392 + index * 224, SetStageFields(new Dictionary<int, string> { { 2, action } }), false, false);
                graph.Connect(decision, result, 1);
                graph.Connect(result, end);
                previous = decision; previousOutcome = 2;
            }
            // Defensive fallback: no matching task action must not manufacture a successful outcome.
            graph.Connect(previous, task, 2);
            return graph.Build();
        }

        public static string BuildStageRouter(WorkflowSettings workflow)
        {
            var settings = workflow.StageRouter;
            var fields = Arr(DataField(settings.RequestIdField, 1, false, 0), DataField(settings.OutcomeField, 2, false, 0), DataField("CurrentStage", 3, false, 0), DataField("RouterResult", 4, false, 0));
            var graph = new StageGraph(workflow.Name, fields);
            var start = graph.Add("Start", 0, 56, null, true, false);
            var entries = new Dictionary<string, JObject>(StringComparer.Ordinal);
            var calls = new Dictionary<string, JObject>(StringComparer.Ordinal);
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            var cursor = 280;
            foreach (var stage in settings.Stages)
            {
                positions.Add(stage.Key, cursor);
                entries.Add(stage.Key, graph.Add("Enter " + stage.Key, 0, cursor, SetStageFields(new Dictionary<int, string> { { 2, "" }, { 3, stage.Key }, { 4, "" } }), false, false));
                var call = new CallSubWorkflowSettings { Workflow = stage.Workflow, WorkflowId = stage.WorkflowId, WaitFor = "all", Account = "Originator",
                    Inputs = new Dictionary<string, string> { { stage.RequestIdInput, settings.RequestIdField } }, Outputs = new Dictionary<string, string> { { stage.OutcomeOutput, settings.OutcomeField } } };
                calls.Add(stage.Key, graph.Add("Execute " + stage.Key, 0, cursor + 168, CallSubWorkflowEvent(call, fields), false, false));
                graph.Connect(entries[stage.Key], calls[stage.Key]);
                cursor += 560 + stage.Transitions.Count * 168;
            }
            foreach (var result in settings.TerminalResults)
            {
                var setResult = graph.Add("Result " + result, -840, cursor, SetStageFields(new Dictionary<int, string> { { 4, result } }), false, false);
                var end = graph.Add("End " + result, -840, cursor + 168, null, false, true);
                entries.Add("$end:" + result, setResult); graph.Connect(setResult, end); cursor += 336;
            }
            graph.Connect(start, entries[settings.InitialStage]);
            foreach (var stage in settings.Stages)
            {
                JObject previous = calls[stage.Key]; var previousOutcome = 0; var index = 0;
                foreach (var transition in stage.Transitions)
                {
                    var decision = graph.Decision(stage.Key + " - " + transition.Key, 0, positions[stage.Key] + 336 + index * 168, 2, transition.Key);
                    graph.Connect(previous, decision, previousOutcome);
                    graph.Connect(decision, entries[transition.Value], 1);
                    previous = decision; previousOutcome = 2; index++;
                }
                var recoveryId = graph.NextId;
                var recovery = graph.Add("Investigate " + stage.Key, 280, positions[stage.Key] + 336,
                    UserTaskEvent(settings.ExceptionTask, settings.RequestIdField, recoveryId, null, null, 0, null, 0), false, false);
                recovery["configuration"] = UserTaskActivityConfiguration(settings.ExceptionTask, recoveryId, false);
                graph.Connect(previous, recovery, 2);
                graph.Connect(recovery, entries[stage.Key]);
            }
            return graph.Build();
        }

        private static string NodeReference(int id) { return "root.nodes[{\"internalId\":" + id + "}]"; }

        private static JObject SetStageFields(IDictionary<int, string> values)
        {
            var mappings = new JArray();
            foreach (var value in values) mappings.Add(Obj("dataFieldReference", "root.configuration.dataFields[{\"internalId\":" + value.Key + "}]", "value", LiteralExpression(value.Value), "internalId", mappings.Count + 1, "componentId", 30003));
            return Obj("wizardId", 3003, "ui", Obj("template", "DefaultStep", "icon", "setDataFieldsStep"), "configuration", Obj("mappings", mappings, "exceptionSettings", Obj("logException", true, "componentId", 50012), "componentId", 30001),
                "systemName", "Set Variables", "title", "Set Variables", "internalId", 1, "componentId", 30000);
        }

        // Saved native definitions must retain the entire declared contract, not just component counts.
        public static void VerifyStageWorkflow(JObject actual, WorkflowSettings workflow)
        {
            var expected = JObject.Parse(workflow.StageRouter != null ? BuildStageRouter(workflow) : BuildStageTask(workflow));
            foreach (var key in new[] { "nodes", "links", "configuration", "externalReferenceDefinitions", "trackedReferences" })
                if (!JToken.DeepEquals(actual[key], expected[key])) throw new CliException("Saved stage workflow differs from the generated manifest contract: " + key + ".");
            var nodes = actual["nodes"].Children<JObject>().ToDictionary(x => (int)x["internalId"]);
            var names = nodes.Values.Select(x => (string)x["systemName"]).ToArray();
            if (names.Distinct(StringComparer.Ordinal).Count() != names.Length) throw new CliException("Stage workflow has duplicate activity system names.");
            foreach (var link in actual["links"].Children<JObject>())
                if (!nodes.ContainsKey((int)link["fromInternalId"]) || !nodes.ContainsKey((int)link["toInternalId"])) throw new CliException("Stage connector has an invalid endpoint.");
        }

        private sealed class StageGraph
        {
            private readonly string _name;
            private readonly JArray _fields;
            private readonly JArray _nodes = new JArray();
            private readonly JArray _links = new JArray();
            public int NextId { get { return _nodes.Count + 1; } }
            public StageGraph(string name, JArray fields) { _name = name; _fields = fields; }

            public JObject Add(string title, int x, int y, JObject child, bool start, bool end)
            {
                var id = NextId;
                var node = Activity(title, id, y, start ? "StartStep" : end ? "EndStep" : "DefaultStep", start ? null : end ? "endStep" : child == null ? null : (string)child["ui"]["icon"], start, end, null, null, child, null);
                node["systemName"] = (start ? "Start" : end ? "End" : child == null ? "Step" : (int)child["componentId"] == 30009 ? "Task" : "Step") + " " + id;
                node["customTitle"] = true; node["ui"]["x"] = x;
                _nodes.Add(node); return node;
            }

            public JObject Decision(string title, int x, int y, int field, string expected)
            {
                var node = BinaryDataDecisionActivity(NextId, y, field, expected, null, null, null, "Match", "Otherwise");
                node["title"] = title; node["systemName"] = "Decision " + NextId;
                node["ui"]["x"] = x; _nodes.Add(node); return node;
            }

            public void Connect(JObject from, JObject to, int outcome = 0)
            {
                var id = _links.Count + 1;
                var fromSide = outcome == 1 ? "leftPorts" : "bottomPorts";
                var fromPort = (JObject)from["ui"][fromSide][1];
                var toPort = (JObject)to["ui"]["topPorts"][1];
                AddReference(fromPort, "outgoingLinkReferences", LinkReference(id));
                AddReference(toPort, "incomingLinkReferences", LinkReference(id));
                var config = Component(40013);
                if (outcome != 0)
                {
                    var reference = NodeReference((int)from["internalId"]) + ".configuration.outcomes[{\"internalId\":" + outcome + "}]";
                    config["associatedOutcomeReference"] = reference;
                    fromPort["outcomeReference"] = reference;
                }
                var x1 = (int)from["ui"]["x"]; var y1 = (int)from["ui"]["y"];
                var x2 = (int)to["ui"]["x"]; var y2 = (int)to["ui"]["y"];
                var startX = outcome == 1 ? x1 - 28 : x1;
                var startY = outcome == 1 ? y1 : y1 + 28;
                var bendX = Math.Min(x1, x2) - 112 - id * 7;
                var path = startX + "," + startY + "," + bendX + "," + startY + "," + bendX + "," + (y2 - 70) + "," + x2 + "," + (y2 - 70) + "," + x2 + "," + (y2 - 28);
                _links.Add(Obj("fromInternalId", (int)from["internalId"], "toInternalId", (int)to["internalId"],
                    "ui", Obj("fromPortId", fromSide + "_1", "toPortId", "topPorts_1", "path", path, "showLabel", outcome != 0),
                    "configuration", config, "title", outcome == 1 ? "Match" : outcome == 2 ? "Otherwise" : "", "systemName", "DefaultLine " + id, "internalId", id, "componentId", 50002));
            }

            private static void AddReference(JObject port, string property, string reference)
            {
                var list = port[property] as JArray;
                if (list == null) { list = new JArray(); port[property] = list; }
                list.Add(reference);
            }

            public string Build()
            {
                var root = Obj("nodes", _nodes, "links", _links, "configuration", ProcessConfiguration(_fields, Arr()), "ui", Component(50004),
                    "externalReferenceDefinitions", Arr(), "trackedReferences", Arr(), "systemName", _name, "title", _name, "componentId", 50001);
                // Track every generated reference so changing a task action or field in Designer preserves relationships.
                var references = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                CollectReferences(root, "root", references);
                var tracked = (JArray)root["trackedReferences"];
                foreach (var reference in references) tracked.Add(Tracked(tracked.Count + 1, reference.Key, reference.Value.ToArray()));
                return root.ToString(Formatting.None);
            }

            private static void CollectReferences(JToken value, string path, IDictionary<string, List<string>> references)
            {
                var obj = value as JObject;
                if (obj != null)
                {
                    foreach (var property in obj.Properties()) CollectReferences(property.Value, path + "." + property.Name, references);
                    return;
                }
                var array = value as JArray;
                if (array != null)
                {
                    for (var i = 0; i < array.Count; i++)
                    {
                        var id = array[i] is JObject ? (int?)array[i]["internalId"] : null;
                        CollectReferences(array[i], path + (id.HasValue ? "[{\"internalId\":" + id + "}]" : "[" + i + "]"), references);
                    }
                    return;
                }
                if (value.Type != JTokenType.String) return;
                var target = (string)value;
                if (!target.StartsWith("root.", StringComparison.Ordinal)) return;
                if (!references.ContainsKey(target)) references.Add(target, new List<string>());
                references[target].Add(path);
            }
        }
    }
}
