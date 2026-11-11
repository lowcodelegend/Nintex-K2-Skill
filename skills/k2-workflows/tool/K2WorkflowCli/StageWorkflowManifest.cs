using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace K2WorkflowCli
{
    internal sealed class StageTaskSettings
    {
        [JsonProperty("requestIdField")] public string RequestIdField { get; set; }
        [JsonProperty("outcomeField")] public string OutcomeField { get; set; }
    }

    internal sealed class StageRouterSettings
    {
        [JsonProperty("initialStage")] public string InitialStage { get; set; }
        [JsonProperty("requestIdField")] public string RequestIdField { get; set; }
        [JsonProperty("outcomeField")] public string OutcomeField { get; set; }
        [JsonProperty("stages")] public List<RouterStageSettings> Stages { get; set; }
        [JsonProperty("terminalResults")] public List<string> TerminalResults { get; set; }
        [JsonProperty("exceptionTask")] public UserTaskSettings ExceptionTask { get; set; }
    }

    internal sealed class RouterStageSettings
    {
        [JsonProperty("key")] public string Key { get; set; }
        [JsonProperty("workflow")] public string Workflow { get; set; }
        [JsonProperty("workflowId")] public int WorkflowId { get; set; }
        [JsonProperty("requestIdInput")] public string RequestIdInput { get; set; }
        [JsonProperty("outcomeOutput")] public string OutcomeOutput { get; set; }
        [JsonProperty("transitions")] public Dictionary<string, string> Transitions { get; set; }
    }

    internal sealed partial class WorkflowManifest
    {
        // These kinds deliberately do not assume SQL resolvers, financial approvals, or a fixed stage count.
        private void ValidateStageTask()
        {
            if (Workflow.StageTask == null) throw new CliException("workflow.stageTask is required for stage-task.");
            if (Workflow.StageRouter != null) throw new CliException("stage-task cannot also declare stageRouter.");
            RejectStageExtras();
            DefaultStageFields(Workflow.StageTask);
            ValidateScalarStageFields(Workflow.StageTask.RequestIdField, Workflow.StageTask.OutcomeField);
            ValidateStageUserTask(Workflow.UserTask, Workflow.StageTask.RequestIdField);
        }

        private void ValidateStageRouter()
        {
            var settings = Workflow.StageRouter;
            if (settings == null) throw new CliException("workflow.stageRouter is required for stage-router.");
            if (Workflow.StageTask != null) throw new CliException("stage-router cannot also declare stageTask.");
            RejectStageExtras();
            if (Workflow.UserTask != null) throw new CliException("stage-router uses stageRouter.exceptionTask, not workflow.userTask.");
            if (string.IsNullOrWhiteSpace(settings.RequestIdField)) settings.RequestIdField = "RequestId";
            if (string.IsNullOrWhiteSpace(settings.OutcomeField)) settings.OutcomeField = "StageOutcome";
            ValidateScalarStageFields(settings.RequestIdField, settings.OutcomeField);
            if (new[] { settings.RequestIdField, settings.OutcomeField }.Any(x => string.Equals(x, "CurrentStage", StringComparison.OrdinalIgnoreCase) || string.Equals(x, "RouterResult", StringComparison.OrdinalIgnoreCase)))
                throw new CliException("Stage request/outcome fields cannot use the reserved CurrentStage or RouterResult names.");
            if (settings.Stages == null || settings.Stages.Count == 0 || settings.Stages.Any(x => x == null))
                throw new CliException("stageRouter.stages must contain stage definitions.");
            var keys = settings.Stages.Select(x => x.Key).ToArray();
            if (keys.Any(string.IsNullOrWhiteSpace) || keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Length || keys.Any(x => x.StartsWith("$", StringComparison.Ordinal)))
                throw new CliException("stageRouter stage keys must be nonblank, unique and cannot start with $. ");
            if (keys.Concat(settings.TerminalResults ?? new List<string>()).Any(x => System.Text.RegularExpressions.Regex.IsMatch(x ?? "", "[\\\\/:.*?\"<>,|()]")))
                throw new CliException("Stage keys and terminal results cannot contain characters forbidden in native activity display names.");
            if (!keys.Contains(settings.InitialStage, StringComparer.Ordinal))
                throw new CliException("stageRouter.initialStage must name an exact stage key.");
            if (settings.TerminalResults == null || settings.TerminalResults.Count == 0 || settings.TerminalResults.Any(string.IsNullOrWhiteSpace) ||
                settings.TerminalResults.Distinct(StringComparer.OrdinalIgnoreCase).Count() != settings.TerminalResults.Count)
                throw new CliException("stageRouter.terminalResults must contain unique nonblank results.");
            foreach (var stage in settings.Stages)
            {
                Required(stage.Workflow, "stageRouter.stages.workflow");
                if (stage.Workflow.IndexOf('\\') < 1 || stage.WorkflowId <= 0)
                    throw new CliException("Each router stage requires an exact deployed workflow full name and positive Designer workflowId.");
                Required(stage.RequestIdInput, "stageRouter.stages.requestIdInput");
                Required(stage.OutcomeOutput, "stageRouter.stages.outcomeOutput");
                if (stage.Transitions == null || stage.Transitions.Count == 0 || stage.Transitions.Keys.Any(string.IsNullOrWhiteSpace) ||
                    stage.Transitions.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != stage.Transitions.Count)
                    throw new CliException("Every router stage requires unique nonblank outcome transitions.");
                if (stage.Transitions.Keys.Any(x => System.Text.RegularExpressions.Regex.IsMatch(x, "[\\\\/:.*?\"<>,|()]")))
                    throw new CliException("Stage transition outcomes cannot contain characters forbidden in native activity display names.");
                foreach (var target in stage.Transitions.Values)
                    if (!keys.Contains(target, StringComparer.Ordinal) && !settings.TerminalResults.Any(x => target == "$end:" + x))
                        throw new CliException("Unknown stage or terminal transition target: " + target);
            }
            var reached = new HashSet<string>(StringComparer.Ordinal) { settings.InitialStage };
            while (true)
            {
                var before = reached.Count;
                foreach (var stage in settings.Stages.Where(x => reached.Contains(x.Key)).ToArray())
                    foreach (var target in stage.Transitions.Values.Where(x => keys.Contains(x, StringComparer.Ordinal))) reached.Add(target);
                if (before == reached.Count) break;
            }
            if (reached.Count != keys.Length) throw new CliException("stageRouter has unreachable stages.");
            // Every stage must have some path to a declared terminal, while allowing deliberate rework loops.
            var exits = new HashSet<string>(settings.Stages.Where(x => x.Transitions.Values.Any(t => t.StartsWith("$end:", StringComparison.Ordinal))).Select(x => x.Key));
            while (true)
            {
                var before = exits.Count;
                foreach (var stage in settings.Stages.Where(x => x.Transitions.Values.Any(exits.Contains))) exits.Add(stage.Key);
                if (before == exits.Count) break;
            }
            if (exits.Count != keys.Length) throw new CliException("Every router stage must have a path to a terminal result.");
            ValidateStageUserTask(settings.ExceptionTask, settings.RequestIdField);
            if (settings.ExceptionTask.Actions.Count != 1 || settings.ExceptionTask.Actions[0] != "Retry")
                throw new CliException("stageRouter.exceptionTask must declare only the Retry action; unknown outcomes wait for explicit human recovery.");
        }

        private void RejectStageExtras()
        {
            if (Workflow.SmartForms != null || Workflow.RequestStatusUpdate != null || Workflow.Email != null || Workflow.ApprovalMatrix != null ||
                Workflow.CaseLifecycle != null || Workflow.CallSubWorkflow != null || Workflow.DataFields != null)
                throw new CliException("Stage kinds use their own scalar fields and explicit task URLs; SmartForms auto-integration, status, email, matrix, legacy lifecycle and additional data-field contracts are not supported in this spike.");
        }

        private static void DefaultStageFields(StageTaskSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.RequestIdField)) settings.RequestIdField = "RequestId";
            if (string.IsNullOrWhiteSpace(settings.OutcomeField)) settings.OutcomeField = "StageOutcome";
        }

        private static void ValidateScalarStageFields(string requestId, string outcome)
        {
            Required(requestId, "stage requestIdField"); Required(outcome, "stage outcomeField");
            if (string.Equals(requestId, outcome, StringComparison.OrdinalIgnoreCase))
                throw new CliException("Stage request and outcome fields must be distinct.");
        }

        private static void ValidateStageUserTask(UserTaskSettings task, string requestField)
        {
            if (task == null) throw new CliException("A stage task definition is required.");
            Required(task.Name, "stage task name"); Required(task.Instructions, "stage task instructions");
            if (task.Assignees == null || task.Assignees.Count == 0 || task.Assignees.Any(string.IsNullOrWhiteSpace) ||
                task.Assignees.Any(x => x.StartsWith("$", StringComparison.Ordinal) && x != "$originator"))
                throw new CliException("Stage tasks require explicit K2 identities or $originator assignees.");
            if (task.Actions == null || task.Actions.Count == 0 || task.Actions.Any(string.IsNullOrWhiteSpace) ||
                task.Actions.Distinct(StringComparer.OrdinalIgnoreCase).Count() != task.Actions.Count)
                throw new CliException("Stage task actions must be nonblank and unique.");
            if (task.Actions.Any(x => System.Text.RegularExpressions.Regex.IsMatch(x, "[\\\\/:.*?\"<>,|()]")))
                throw new CliException("Stage task actions cannot contain characters forbidden in native activity display names.");
            Uri url;
            if (!Uri.TryCreate(task.FormUrl, UriKind.Absolute, out url) || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
                throw new CliException("Stage task formUrl must be an absolute HTTP(S) URL.");
            if (task.Notification != null && task.Notification.Enabled)
                throw new CliException("Stage task notifications are outside the spike contract.");
            if (string.IsNullOrWhiteSpace(task.RequestIdParameter)) task.RequestIdParameter = requestField;
        }
    }
}
