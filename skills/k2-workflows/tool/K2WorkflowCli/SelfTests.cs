using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace K2WorkflowCli
{
    internal static class SelfTests
    {
        public static void Run()
        {
            var task = new UserTaskSettings { Assignees = new List<string> { "$originator", "K2:DOMAIN\\approvers" } };
            string title;
            var direct = WorkflowJsonBuilder.BuildTaskDestinationItems(task, null, out title);
            Assert(title == "Manifest Assignees" && direct.Count == 2, "direct destination count");
            Assert((string)direct[0]["smartFields"][0]["fieldName"] == "ProcessOriginatorFQN" && (bool)direct[0]["isDynamic"], "originator destination");
            Assert((string)direct[1]["smartFields"][0]["text"] == "K2:DOMAIN\\approvers" && !(bool)direct[1]["isDynamic"], "literal K2 destination");

            var matrix = WorkflowJsonBuilder.BuildTaskDestinationItems(new UserTaskSettings { Assignees = new List<string>() }, 3, out title);
            Assert(title == "Approval Matrix Approver" && matrix.Count == 1, "matrix destination count");
            Assert(Convert.ToString(matrix[0]["smartFields"][0]["dataFieldReference"]).EndsWith("[{\"internalId\":3}]", StringComparison.Ordinal), "matrix data-field destination");
            var workflow = new WorkflowSettings { Name = "Parent", DataFields = new List<WorkflowDataFieldSettings> { new WorkflowDataFieldSettings { Name = "CaseStageInstanceId", Type = "text" } }, CallSubWorkflow = new CallSubWorkflowSettings { Workflow = "App WFs\\Child", WorkflowId = 42, Account = "Originator", WaitFor = "all", Inputs = new Dictionary<string,string> { { "CaseStageInstanceId", "CaseStageInstanceId" } } } };
            var root = JObject.Parse(WorkflowJsonBuilder.BuildCallSubWorkflow(workflow));
            var call = (JObject)root["nodes"][1]["children"][0];
            Assert((int)call["componentId"] == 30021, "Call Sub Workflow component");
            Assert((int)call["configuration"]["waitMode"] == 1 && (bool)call["configuration"]["synchronous"], "Call Sub Workflow synchronous wait");
            Assert((string)call["configuration"]["selectedWorkflowFullName"] == "App WFs\\Child", "Call Sub Workflow target");
            Assert((int)call["configuration"]["selectedWorkflowId"] == 42, "Call Sub Workflow deployed target ID");
            var send = (JObject)call["configuration"]["processSendFields"]["CaseStageInstanceId"];
            Assert((string)send["value"]["smartFields"][0]["customTitle"] == "CaseStageInstanceId", "Call Sub Workflow scalar input mapping");
            Console.WriteLine("SELFTEST SUCCEEDED: destinations, matrix routing, and synchronous Call Sub Workflow generation and data mapping");
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new CliException("Self-test failed: " + name + ".");
        }
    }
}
