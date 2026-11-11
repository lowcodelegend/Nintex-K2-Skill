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
            TestStageWorkflows();
            TestDesignerIpcFixtures();
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
            Assert(call["configuration"]["waitMode"] == null && (bool)call["configuration"]["synchronous"], "single IPC uses native synchronous flag");
            Assert((int)call["wizardId"] == 3004 && (string)root["nodes"][1]["ui"]["template"] == "SubWorkflowStep", "native IPC wizard and activity template");
            Assert(call["configuration"]["processSendFields"]["09dfaa24-ac04-4827-8082-646bc8918f80"] != null, "native child folio mapping");
            Assert((string)call["configuration"]["selectedWorkflowFullName"] == "App WFs\\Child", "Call Sub Workflow target");
            Assert(call["configuration"]["selectedWorkflowId"] == null, "native static IPC selects the full workflow name, not a serialized Designer ID");
            var send = (JObject)call["configuration"]["processSendFields"]["CaseStageInstanceId"];
            Assert((string)send["value"]["smartFields"][0]["customTitle"] == "CaseStageInstanceId", "Call Sub Workflow scalar input mapping");
            workflow.CallSubWorkflow.Outputs = new Dictionary<string, string> { { "ChildResult", "CaseStageInstanceId" } };
            var output = JObject.Parse(WorkflowJsonBuilder.BuildCallSubWorkflow(workflow))["nodes"][1]["children"][0]["configuration"]["processReturnFields"]["ChildResult"];
            Assert((int?)output["componentId"] == 10008 && output["value"] == null, "primitive IPC return uses Expression directly");
            Assert(WorkflowManager.ParseCategoryResponse("{}") == null && WorkflowManager.ParseCategoryResponse("{\"id\":null}") == null, "missing category responses are absent");
            Assert((int)WorkflowManager.ParseCategoryResponse("{\"id\":4}")["id"] == 4, "existing category identity retained");
            Assert(WorkflowManager.CategoryMatchesCleanup(459, @"K2 Skills\APP.Loans\APP.Loans_WFs", 459, @"K2 Skills\APP.Loans\APP.Loans WFs"), "cleanup resolves sanitized workflow category by authoritative ID");
            Assert(!WorkflowManager.CategoryMatchesCleanup(460, @"K2 Skills\APP.Loans\APP.Loans WFs", 459, @"K2 Skills\APP.Loans\APP.Loans WFs"), "cleanup cannot substitute another category when ID is known");
            Assert(WorkflowManager.IsOwnedWorkflowCategoryLink("Workflow", @"APP.Loans WFs\APP.Test", "APP.Test", @"APP.Loans WFs\APP.Test", "APP.Test"), "exact stale workflow category link recognized");
            Assert(!WorkflowManager.IsOwnedWorkflowCategoryLink("Form", @"APP.Loans WFs\APP.Test", "APP.Test", @"APP.Loans WFs\APP.Test", "APP.Test"), "cleanup retains other artifact kinds");
            Assert(!WorkflowManager.IsOwnedWorkflowCategoryLink("Workflow", @"APP.Loans WFs\APP.Foreign", "APP.Foreign", @"APP.Loans WFs\APP.Test", "APP.Test"), "cleanup retains undeclared workflow links");
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
                Assert(lifecycleCalls[index]["configuration"]["selectedWorkflowId"] == null, "case lifecycle keeps Designer IDs out of the native static IPC payload");
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

        private static void TestStageWorkflows()
        {
            var taskWorkflow = new WorkflowSettings {
                Name = "APP.Stage", Kind = "stage-task", StageTask = new StageTaskSettings { RequestIdField = "LoanId", OutcomeField = "Result" },
                UserTask = new UserTaskSettings { Name = "Credit", Instructions = "Select a result", FormUrl = "https://example.invalid/task", RequestIdParameter = "LoanId", Assignees = new List<string> { "$originator" }, Actions = new List<string> { "Approve", "Reject", "Request Additional Info" } }
            };
            var manifest = new WorkflowManifest { SchemaVersion = 1, Application = new ApplicationSettings { RootCategoryPath = @"K2 Skills\APP.Application" }, Workflow = taskWorkflow };
            ValidateManifest(manifest);
            var root = JObject.Parse(WorkflowJsonBuilder.BuildStageTask(taskWorkflow));
            WorkflowJsonBuilder.VerifyStageWorkflow(root, taskWorkflow);
            Assert(root["nodes"].Children<JObject>().SelectMany(x => (x["children"] as JArray ?? new JArray()).Children<JObject>()).Count(x => (int?)x["componentId"] == 30000) == 3, "three explicit task-result assignments");
            var broken = (JObject)root.DeepClone();
            broken["nodes"][1]["children"][0]["configuration"]["actions"][2]["actionTitle"] = "Approve";
            AssertThrows(delegate { WorkflowJsonBuilder.VerifyStageWorkflow(broken, taskWorkflow); }, "saved action drift rejected");
            taskWorkflow.UserTask.Actions.Add("approve");
            AssertThrows(delegate { ValidateManifest(manifest); }, "duplicate action rejected"); taskWorkflow.UserTask.Actions.RemoveAt(3);
            taskWorkflow.UserTask.Actions = new List<string> { "Complete" }; ValidateManifest(manifest);
            WorkflowJsonBuilder.VerifyStageWorkflow(JObject.Parse(WorkflowJsonBuilder.BuildStageTask(taskWorkflow)), taskWorkflow);
            taskWorkflow.UserTask.Actions = new List<string>(); AssertThrows(delegate { ValidateManifest(manifest); }, "empty actions rejected");

            var settings = new StageRouterSettings { InitialStage = "Stage0", RequestIdField = "LoanId", OutcomeField = "Result", TerminalResults = new List<string> { "Completed", "Rejected" }, Stages = new List<RouterStageSettings>(),
                ExceptionTask = new UserTaskSettings { Name = "Investigate", Instructions = "Repair and Retry", FormUrl = "https://example.invalid/task", Assignees = new List<string> { "$originator" }, Actions = new List<string> { "Retry" } } };
            for (var index = 0; index < 6; index++) settings.Stages.Add(new RouterStageSettings { Key = "Stage" + index, Workflow = @"APP.Application WFs\APP.Child" + index, WorkflowId = 100 + index, RequestIdInput = "LoanId", OutcomeOutput = "Result",
                Transitions = new Dictionary<string, string> { { "Complete", index == 5 ? "$end:Completed" : "Stage" + (index + 1) }, { "Reject", "$end:Rejected" } } });
            settings.Stages[3].Transitions["Rework"] = "Stage2";
            var parent = new WorkflowSettings { Name = "APP.Router", Kind = "stage-router", StageRouter = settings };
            manifest.Workflow = parent; ValidateManifest(manifest);
            var router = JObject.Parse(WorkflowJsonBuilder.BuildStageRouter(parent));
            WorkflowJsonBuilder.VerifyStageWorkflow(router, parent);
            var calls = router["nodes"].Children<JObject>().SelectMany(x => (x["children"] as JArray ?? new JArray()).Children<JObject>()).Where(x => (int?)x["componentId"] == 30021).ToArray();
            Assert(calls.Length == 6, "six-stage router independent of canonical eight-stage lifecycle");
            Assert(calls.All(x => (bool?)x["configuration"]["synchronous"] == true && (int?)x["wizardId"] == 3004), "all children use native synchronous IPC");
            Assert(calls.All(x => (int?)x["configuration"]["processReturnFields"]["Result"]["componentId"] == 10008), "IPC output is a native Expression, not a SmartObject property-mapper row");
            Assert(calls.All(x => (string)x["configuration"]["processReturnFields"]["Result"]["smartFields"][0]["designTimeValue"] == "root.configuration.dataFields[{\"internalId\":2}]"), "IPC output targets parent scalar field");
            var bad = (JObject)router.DeepClone(); bad["links"][0]["toInternalId"] = 99999;
            AssertThrows(delegate { WorkflowJsonBuilder.VerifyStageWorkflow(bad, parent); }, "saved connector drift rejected");
            settings.Stages[0].Transitions["Complete"] = "Missing";
            AssertThrows(delegate { ValidateManifest(manifest); }, "unknown transition rejected"); settings.Stages[0].Transitions["Complete"] = "Stage1";
            settings.Stages[5].Transitions["Complete"] = "Stage5"; settings.Stages[5].Transitions.Remove("Reject");
            AssertThrows(delegate { ValidateManifest(manifest); }, "closed cycle without terminal rejected");
            Console.WriteLine("SELFTEST stage workflows: six-stage rework routing, one/three-action tasks, native IPC returns, contract drift and invalid topology guards passed.");
        }

        private static void ValidateManifest(WorkflowManifest manifest)
        {
            var path = System.IO.Path.GetTempFileName();
            try { System.IO.File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(manifest)); WorkflowManifest.Load(path); }
            finally { System.IO.File.Delete(path); }
        }

        private static void TestDesignerIpcFixtures()
        {
            JObject native;
            using (var stream = typeof(SelfTests).Assembly.GetManifestResourceStream("K2WorkflowCli.DesignerIpcFixtures.json"))
            using (var reader = new System.IO.StreamReader(stream)) native = JObject.Parse(reader.ReadToEnd());
            var settings = new CallSubWorkflowSettings { Workflow = "Example\\Child", WaitFor = "all", Account = "Originator", CallType = "single", Folio = "Child folio", Inputs = new Dictionary<string,string> { { "Dummy", "Return Var" } }, Outputs = new Dictionary<string,string> { { "Dummy", "Return Var" } } };
            var workflow = new WorkflowSettings { Name = "APP.IPC", CallSubWorkflow = settings, DataFields = new List<WorkflowDataFieldSettings> { new WorkflowDataFieldSettings { Name = "Return Var", Type = "text" } } };
            var generated = JObject.Parse(WorkflowJsonBuilder.BuildCallSubWorkflow(workflow));
            var owner = generated["nodes"][1]; var child = owner["children"][0]; var config = child["configuration"];
            var expected = native["single"]["children"][0];
            Assert(JToken.DeepEquals(child["wizardId"], expected["wizardId"]) && JToken.DeepEquals(child["ui"], expected["ui"]), "IPC wizard/UI matches user-authored Designer example");
            Assert(JToken.DeepEquals(owner["configuration"], native["single"]["configuration"]), "IPC parent activity configuration matches native example");
            Assert((string)owner["ui"]["template"] == (string)native["single"]["ui"]["template"], "IPC activity is SubWorkflowStep");
            Assert(JToken.DeepEquals(config["selectedWorkflow"], expected["configuration"]["selectedWorkflow"]), "native selected workflow full name and display title");
            Assert(JToken.DeepEquals(config["processReturnFields"], expected["configuration"]["processReturnFields"]), "native IPC result mapper matches Designer output expression");
            Assert((bool)config["synchronous"] && config["waitMode"] == null, "single wait uses synchronous flag");
            settings.CallType = "multiple"; settings.InstanceData = "A;B;C"; settings.Outputs = null;
            config = JObject.Parse(WorkflowJsonBuilder.BuildCallSubWorkflow(workflow))["nodes"][1]["children"][0]["configuration"];
            Assert((int)config["subWorkflowCallType"] == (int)native["multiple"]["children"][0]["configuration"]["subWorkflowCallType"], "native multiple IPC mode");
            Assert((bool)config["synchronous"] && (int)config["waitMode"] == 1, "Multiple/All must persist both synchronous=true and waitMode=1");
            Assert((string)config["slotInstanceData"]["smartFields"][0]["text"] == "A;B;C", "multiple IPC list is a native delimited expression");
            settings.WaitFor = "none";
            config = JObject.Parse(WorkflowJsonBuilder.BuildCallSubWorkflow(workflow))["nodes"][1]["children"][0]["configuration"];
            Assert(!(bool)config["synchronous"] && (int)config["waitMode"] == 0, "Multiple/None native defaults");
            var manifest = new WorkflowManifest { SchemaVersion = 1, Application = new ApplicationSettings { RootCategoryPath = @"K2 Skills\APP.Application" }, Workflow = workflow };
            workflow.Kind = "call-subworkflow"; settings.WaitFor = "all"; settings.Outputs = new Dictionary<string,string> { { "Dummy", "Return Var" } };
            AssertThrows(delegate { ValidateManifest(manifest); }, "Multiple/All must not expose scalar result mapper hidden by native Designer");
            Console.WriteLine("SELFTEST native Designer fixtures: IPC wizard, activity, folio/result mapping and coupled multiple wait flags passed.");
        }

        private static void AssertThrows(Action action, string name)
        {
            try { action(); } catch (CliException) { return; }
            throw new CliException("Self-test failed: " + name + " did not reject the invalid contract.");
        }
    }
}
