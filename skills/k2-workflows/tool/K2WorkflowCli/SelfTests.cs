using System;
using System.Collections.Generic;
using System.Linq;
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
            var stageNames = new List<string>();
            var stageIds = new Dictionary<string, int>();
            for (var index = 0; index < 8; index++)
            {
                var stageName = "APP.Application WFs\\APP Stage " + (index + 1);
                stageNames.Add(stageName);
                stageIds.Add(stageName, 100 + index);
            }
            var lifecycleSettings = new CaseLifecycleSettings
            {
                ResolverCaseIdInput = "ResolverCaseId",
                StateCaseIdInput = "StateCaseId",
                CaseIdDataField = "CaseId",
                StageInstanceProperty = "CaseStageInstanceId",
                StageWorkflowProperty = "StageWorkflowName",
                IsTerminalProperty = "IsTerminal",
                ChildStageInstanceInput = "CaseStageInstanceId",
                StageWorkflows = stageNames,
                WorkflowIds = stageIds
            };
            var lifecycleMethod = new SmartObjectMethodDescriptor
            {
                SystemName = "APP_State",
                DisplayName = "APP State",
                MethodSystemName = "Read",
                MethodDisplayName = "Read",
                MethodType = "read",
                Inputs = new List<SmartObjectInputDescriptor>
                {
                    new SmartObjectInputDescriptor { InternalId = 1, SystemName = "ResolverCaseId", DisplayName = "ResolverCaseId", Type = "Number", IsRequired = true },
                    new SmartObjectInputDescriptor { InternalId = 2, SystemName = "StateCaseId", DisplayName = "StateCaseId", Type = "Number", IsRequired = true }
                },
                Returns = new List<SmartObjectInputDescriptor>
                {
                    new SmartObjectInputDescriptor { InternalId = 1, SystemName = "CaseStageInstanceId", DisplayName = "CaseStageInstanceId", Type = "Number" },
                    new SmartObjectInputDescriptor { InternalId = 2, SystemName = "StageWorkflowName", DisplayName = "StageWorkflowName", Type = "Text" },
                    new SmartObjectInputDescriptor { InternalId = 3, SystemName = "IsTerminal", DisplayName = "IsTerminal", Type = "YesNo" }
                }
            };
            var lifecycleRoot = JObject.Parse(WorkflowJsonBuilder.BuildCaseLifecycle(
                new WorkflowSettings { Name = "APP Lifecycle", CaseLifecycle = lifecycleSettings },
                lifecycleMethod,
                lifecycleMethod));
            var lifecycleCalls = lifecycleRoot["nodes"]
                .Children<JObject>()
                .SelectMany(x => x["children"] == null ? Enumerable.Empty<JObject>() : x["children"].Children<JObject>())
                .Where(x => (int?)x["componentId"] == 30021)
                .ToArray();
            var lifecycleResolveEvents = lifecycleRoot["nodes"][1]["children"].Children<JObject>().ToArray();
            Assert(lifecycleResolveEvents[0]["configuration"]["controlValues"]["pmInputs"]["values"]["ResolverCaseId"] != null,
                "case lifecycle resolver-specific case input");
            Assert(lifecycleResolveEvents[1]["configuration"]["controlValues"]["pmInputs"]["values"]["StateCaseId"] != null,
                "case lifecycle state-specific case input");
            Assert(lifecycleCalls.Length == 8, "case lifecycle stage call count");
            for (var index = 0; index < lifecycleCalls.Length; index++)
            {
                Assert((string)lifecycleCalls[index]["configuration"]["selectedWorkflowFullName"] == stageNames[index], "case lifecycle ordered stage target");
                Assert((int)lifecycleCalls[index]["configuration"]["selectedWorkflowId"] == stageIds[stageNames[index]], "case lifecycle deployed target ID");
            }
            var terminalDecision = (JObject)lifecycleRoot["nodes"][2];
            Assert((string)terminalDecision["configuration"]["outcomeRule"]["statements"][0]["IfExpressions"][0]["expressions"][0]["rightExpression"]["value"]["smartFields"][0]["text"] == "true",
                "case lifecycle terminal flag decision");
            var application = new ApplicationSettings
            {
                RootCategoryPath = @"K2 Skills\APP.Application\\",
                WorkflowCategoryName = "APP.Application WFs"
            };
            var leafOnly = WorkflowManager.CleanupCategoryPaths(application, false);
            Assert(leafOnly.Count == 1 && leafOnly[0] == @"K2 Skills\APP.Application\APP.Application WFs",
                "standalone cleanup owns only the workflow category");
            var complete = WorkflowManager.CleanupCategoryPaths(application, true);
            Assert(complete.Count == 2 && complete[1] == @"K2 Skills\APP.Application",
                "builder cleanup deletes the application root last");
            var defaults = new K2Settings();
            Assert(defaults.Integrated
                && defaults.Port == 5555
                && defaults.WorkflowPort == 5252
                && defaults.SecurityLabel == "K2",
                "integrated connection defaults");
            var k2sql = new K2Settings
            {
                Integrated = false,
                SecurityLabel = "K2SQL",
                UserName = "K2Admin",
                PasswordEnvironmentVariable = "K2_DEPLOYMENT_PASSWORD"
            };
            Assert(
                K2Connection.DescribeIdentity(k2sql) == "K2SQL:K2Admin",
                "non-integrated author identity");
            Console.WriteLine("SELFTEST SUCCEEDED: destinations, matrix routing, synchronous Call Sub Workflow generation/data mapping, ordered manifest-driven case lifecycle routing, bounded category cleanup paths, and K2SQL author identity");
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new CliException("Self-test failed: " + name + ".");
        }
    }
}
