using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace K2WorkflowCli
{
    internal sealed class WorkflowManifest
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("k2")] public K2Settings K2 { get; set; }
        [JsonProperty("application")] public ApplicationSettings Application { get; set; }
        [JsonProperty("workflow")] public WorkflowSettings Workflow { get; set; }
        [JsonIgnore] public string ManifestDirectory { get; private set; }

        public static WorkflowManifest Load(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new CliException("Manifest not found: " + fullPath);
            WorkflowManifest manifest;
            try { manifest = JsonConvert.DeserializeObject<WorkflowManifest>(File.ReadAllText(fullPath)); }
            catch (Exception ex) { throw new CliException("Invalid manifest JSON: " + ex.Message); }
            if (manifest == null) throw new CliException("Manifest is empty.");
            manifest.ManifestDirectory = Path.GetDirectoryName(fullPath);
            manifest.Validate();
            return manifest;
        }

        private void Validate()
        {
            if (SchemaVersion != 1) throw new CliException("schemaVersion must be 1.");
            if (K2 == null) K2 = new K2Settings();
            if (string.IsNullOrWhiteSpace(K2.DesignerHost)) K2.DesignerHost = "smartforms";
            if (string.IsNullOrWhiteSpace(K2.Host)) K2.Host = "localhost";
            if (K2.Port == 0) K2.Port = 5555;
            if (!K2.Integrated) K2.Integrated = true;
            if (string.IsNullOrWhiteSpace(K2.SecurityLabel)) K2.SecurityLabel = "K2";
            if (!string.Equals(K2.DesignerHost, "smartforms", StringComparison.OrdinalIgnoreCase))
                throw new CliException("k2.designerHost must be 'smartforms' for the installed K2 Five HTML5 Workflow Designer environment.");
            if (Application == null || string.IsNullOrWhiteSpace(Application.RootCategoryPath))
                throw new CliException("application.rootCategoryPath is required.");
            ValidateNoVersion(Application.RootCategoryPath, "application.rootCategoryPath");
            if (Application.RootCategoryPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(x => string.Equals(x, "Workflow", StringComparison.OrdinalIgnoreCase) || string.Equals(x, "Workflows", StringComparison.OrdinalIgnoreCase)))
                throw new CliException("application.rootCategoryPath must not contain a category named Workflow or Workflows.");
            if (string.IsNullOrWhiteSpace(Application.WorkflowCategoryName))
                Application.WorkflowCategoryName = Application.RootLeafName + " WFs";
            ValidateNoVersion(Application.WorkflowCategoryName, "application.workflowCategoryName");
            if (Application.WorkflowCategoryName.IndexOfAny(new[] { '\\', '/' }) >= 0)
                throw new CliException("application.workflowCategoryName must be a leaf name.");
            if (string.Equals(Application.WorkflowCategoryName, "Workflow", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Application.WorkflowCategoryName, "Workflows", StringComparison.OrdinalIgnoreCase))
                throw new CliException("application.workflowCategoryName must be solution-specific and end in ' WFs'; do not use Workflow or Workflows.");
            var expectedWorkflowCategory = Application.RootLeafName + " WFs";
            if (!string.Equals(Application.WorkflowCategoryName, expectedWorkflowCategory, StringComparison.Ordinal))
                throw new CliException("application.workflowCategoryName must be '<application root leaf> WFs': " + expectedWorkflowCategory);
            if (Workflow == null || string.IsNullOrWhiteSpace(Workflow.Name)) throw new CliException("workflow.name is required.");
            Workflow.ProcessFolderName = Application.WorkflowCategoryName;
            ValidateNoVersion(Workflow.Name, "workflow.name");
            if (Workflow.Name.IndexOfAny(new[] { '\\', '/' }) >= 0) throw new CliException("workflow.name must be a leaf name.");
            if (string.IsNullOrWhiteSpace(Workflow.Kind)) Workflow.Kind = "start-end";
            if (!string.Equals(Workflow.Kind, "start-end", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Workflow.Kind, "request-approval", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Workflow.Kind, "json-file", StringComparison.OrdinalIgnoreCase))
                throw new CliException("workflow.kind must be 'start-end', 'request-approval', or 'json-file'.");
            if (string.Equals(Workflow.Kind, "json-file", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Workflow.DefinitionFile))
                throw new CliException("workflow.definitionFile is required when workflow.kind is 'json-file'.");
            if (string.Equals(Workflow.Kind, "request-approval", StringComparison.OrdinalIgnoreCase)) ValidateRequestApproval();
        }

        private void ValidateRequestApproval()
        {
            var update = Workflow.RequestStatusUpdate;
            if (update == null) throw new CliException("workflow.requestStatusUpdate is required for request-approval.");
            Required(update.SmartObject, "workflow.requestStatusUpdate.smartObject");
            Required(update.Method, "workflow.requestStatusUpdate.method");
            Required(update.IdentifierProperty, "workflow.requestStatusUpdate.identifierProperty");
            Required(update.StatusProperty, "workflow.requestStatusUpdate.statusProperty");
            Required(update.StatusValue, "workflow.requestStatusUpdate.statusValue");
            if (string.IsNullOrWhiteSpace(update.ApprovedStatusValue)) update.ApprovedStatusValue = "Approved";
            if (string.IsNullOrWhiteSpace(update.RejectedStatusValue)) update.RejectedStatusValue = "Rejected";
            if (string.IsNullOrWhiteSpace(update.IdentifierDataField)) update.IdentifierDataField = update.IdentifierProperty;

            var email = Workflow.Email;
            if (email == null) throw new CliException("workflow.email is required for request-approval.");
            Required(email.Name, "workflow.email.name");
            if (string.IsNullOrWhiteSpace(email.From)) email.From = "$environment:From Address";
            Required(email.Subject, "workflow.email.subject"); Required(email.Body, "workflow.email.body");
            if (email.To == null || email.To.Count == 0) email.To = new List<string> { "$originator" };
            if (email.To.Any(string.IsNullOrWhiteSpace))
                throw new CliException("workflow.email.to must contain at least one recipient.");

            var task = Workflow.UserTask;
            if (task == null) throw new CliException("workflow.userTask is required for request-approval.");
            Required(task.Name, "workflow.userTask.name"); Required(task.Instructions, "workflow.userTask.instructions");
            if (task.Assignees == null) task.Assignees = new List<string>();
            if (Workflow.ApprovalMatrix == null)
            {
                if (task.Assignees.Count == 0 || task.Assignees.Any(string.IsNullOrWhiteSpace))
                    throw new CliException("workflow.userTask.assignees must explicitly contain at least one K2 user/group identity for direct routing.");
                var unsupportedAssignee = task.Assignees.FirstOrDefault(x => x.StartsWith("$", StringComparison.Ordinal) && !string.Equals(x, "$originator", StringComparison.OrdinalIgnoreCase));
                if (unsupportedAssignee != null)
                    throw new CliException("Unsupported workflow.userTask.assignees token '" + unsupportedAssignee + "'. Direct routing supports $originator or explicit K2 identity strings.");
            }
            else if (task.Assignees.Count > 0)
                throw new CliException("workflow.userTask.assignees must be omitted when workflow.approvalMatrix is authoritative.");
            if (task.Actions == null || task.Actions.Count == 0 || task.Actions.Any(string.IsNullOrWhiteSpace))
                throw new CliException("workflow.userTask.actions must contain at least one action.");
            if (task.Actions.Distinct(StringComparer.OrdinalIgnoreCase).Count() != task.Actions.Count)
                throw new CliException("workflow.userTask.actions must be unique.");
            if (string.IsNullOrWhiteSpace(task.RequestIdParameter)) task.RequestIdParameter = update.IdentifierDataField;
            if (task.Notification != null && task.Notification.Enabled)
            {
                if (string.IsNullOrWhiteSpace(task.Notification.From)) task.Notification.From = email.From;
                Required(task.Notification.Subject, "workflow.userTask.notification.subject");
                Required(task.Notification.Body, "workflow.userTask.notification.body");
                if (Workflow.SmartForms == null &&
                    (task.Notification.Subject.IndexOf("{{request.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     task.Notification.Body.IndexOf("{{request.", StringComparison.OrdinalIgnoreCase) >= 0))
                    throw new CliException("Task notification request-property tokens require workflow.smartForms and its primary item reference.");
            }

            if (Workflow.SmartForms != null)
            {
                Required(Workflow.SmartForms.Form, "workflow.smartForms.form");
                if (string.IsNullOrWhiteSpace(Workflow.SmartForms.StartState)) Workflow.SmartForms.StartState = Workflow.Name + " Start";
                if (string.IsNullOrWhiteSpace(Workflow.SmartForms.TaskState)) Workflow.SmartForms.TaskState = Workflow.Name + " Task";
                if (string.IsNullOrWhiteSpace(Workflow.SmartForms.StartRuleContains)) Workflow.SmartForms.StartRuleContains = "Create Button";
                ValidateNoVersion(Workflow.SmartForms.StartState, "workflow.smartForms.startState");
                ValidateNoVersion(Workflow.SmartForms.TaskState, "workflow.smartForms.taskState");
                if (task.Actions.Count != 2)
                    throw new CliException("SmartForms request-approval requires exactly two actions for decision routing.");
            }
            else
            {
                Required(task.FormUrl, "workflow.userTask.formUrl");
                Uri uri;
                if (!Uri.TryCreate(task.FormUrl, UriKind.Absolute, out uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    throw new CliException("workflow.userTask.formUrl must be an absolute HTTP(S) URL when workflow.smartForms is omitted.");
            }

            if (Workflow.ApprovalMatrix != null) ValidateApprovalMatrix(Workflow.ApprovalMatrix);
        }

        private void ValidateApprovalMatrix(ApprovalMatrixSettings matrix)
        {
            if (Workflow.SmartForms == null) throw new CliException("workflow.approvalMatrix requires workflow.smartForms so request properties can be mapped into the resolver.");
            if (string.IsNullOrWhiteSpace(matrix.Name)) matrix.Name = Workflow.Name + " Resolve Approval Matrix";
            Required(matrix.SmartObject, "workflow.approvalMatrix.smartObject");
            if (string.IsNullOrWhiteSpace(matrix.Method)) matrix.Method = "List";
            Required(matrix.MatrixCode, "workflow.approvalMatrix.matrixCode");
            if (string.IsNullOrWhiteSpace(matrix.MatrixCodeInput)) matrix.MatrixCodeInput = "MatrixCodeInput";
            Required(matrix.AmountProperty, "workflow.approvalMatrix.amountProperty");
            if (string.IsNullOrWhiteSpace(matrix.AmountInput)) matrix.AmountInput = "AmountInput";
            if (string.IsNullOrWhiteSpace(matrix.CurrentStageInput)) matrix.CurrentStageInput = "CurrentStageInput";
            if (string.IsNullOrWhiteSpace(matrix.HasApproverProperty)) matrix.HasApproverProperty = "HasApprover";
            if (string.IsNullOrWhiteSpace(matrix.StageProperty)) matrix.StageProperty = "StageNumber";
            if (string.IsNullOrWhiteSpace(matrix.ApproverProperty)) matrix.ApproverProperty = "ApproverValue";
            if (string.IsNullOrWhiteSpace(matrix.ApproverTypeProperty)) matrix.ApproverTypeProperty = "ApproverType";
            if (matrix.Dimensions == null) matrix.Dimensions = new List<ApprovalMatrixInputMapping>();
            var inputNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dimension in matrix.Dimensions)
            {
                if (dimension == null) throw new CliException("workflow.approvalMatrix.dimensions entries cannot be null.");
                Required(dimension.RequestProperty, "workflow.approvalMatrix.dimensions.requestProperty");
                Required(dimension.InputProperty, "workflow.approvalMatrix.dimensions.inputProperty");
                if (!inputNames.Add(dimension.InputProperty)) throw new CliException("workflow.approvalMatrix dimension inputProperty values must be unique: " + dimension.InputProperty);
            }
        }

        private static void Required(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new CliException(field + " is required.");
        }

        private static void ValidateNoVersion(string value, string field)
        {
            if (Regex.IsMatch(value, @"(^|[\\/\s_-])v?\d+\.\d+(?:\.\d+)?($|[\\/\s_-])", RegexOptions.IgnoreCase))
                throw new CliException(field + " must not contain a version. K2 versions workflows internally.");
        }
    }

    internal sealed class K2Settings
    {
        [JsonProperty("designerHost")] public string DesignerHost { get; set; }
        [JsonProperty("host")] public string Host { get; set; }
        [JsonProperty("port")] public int Port { get; set; }
        [JsonProperty("integrated")] public bool Integrated { get; set; }
        [JsonProperty("securityLabel")] public string SecurityLabel { get; set; }
    }

    internal sealed class ApplicationSettings
    {
        [JsonProperty("rootCategoryPath")] public string RootCategoryPath { get; set; }
        [JsonProperty("workflowCategoryName")] public string WorkflowCategoryName { get; set; }
        [JsonIgnore] public string RootLeafName { get { return RootCategoryPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Last(); } }
        [JsonIgnore] public string WorkflowCategoryPath { get { return RootCategoryPath.TrimEnd('\\', '/') + "\\" + WorkflowCategoryName; } }
    }

    internal sealed class WorkflowSettings
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("definitionFile")] public string DefinitionFile { get; set; }
        [JsonProperty("publish")] public bool Publish { get; set; }
        [JsonProperty("replaceExisting")] public bool ReplaceExisting { get; set; }
        [JsonProperty("comment")] public string Comment { get; set; }
        [JsonProperty("requestStatusUpdate")] public RequestStatusUpdateSettings RequestStatusUpdate { get; set; }
        [JsonProperty("email")] public EmailSettings Email { get; set; }
        [JsonProperty("userTask")] public UserTaskSettings UserTask { get; set; }
        [JsonProperty("approvalMatrix")] public ApprovalMatrixSettings ApprovalMatrix { get; set; }
        [JsonProperty("smartForms")] public SmartFormsSettings SmartForms { get; set; }
        [JsonIgnore] public string ProcessFolderName { get; set; }
        [JsonIgnore] public string ProcessFullName { get { return ProcessFolderName + "\\" + Name; } }
    }

    internal sealed class RequestStatusUpdateSettings
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("smartObject")] public string SmartObject { get; set; }
        [JsonProperty("method")] public string Method { get; set; }
        [JsonProperty("identifierProperty")] public string IdentifierProperty { get; set; }
        [JsonProperty("identifierDataField")] public string IdentifierDataField { get; set; }
        [JsonProperty("statusProperty")] public string StatusProperty { get; set; }
        [JsonProperty("statusValue")] public string StatusValue { get; set; }
        [JsonProperty("approvedStatusValue")] public string ApprovedStatusValue { get; set; }
        [JsonProperty("rejectedStatusValue")] public string RejectedStatusValue { get; set; }
    }

    internal sealed class EmailSettings
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("from")] public string From { get; set; }
        [JsonProperty("to")] public List<string> To { get; set; }
        [JsonProperty("subject")] public string Subject { get; set; }
        [JsonProperty("body")] public string Body { get; set; }
        [JsonProperty("html")] public bool Html { get; set; }
    }

    internal sealed class UserTaskSettings
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("assignees")] public List<string> Assignees { get; set; }
        [JsonProperty("instructions")] public string Instructions { get; set; }
        [JsonProperty("actions")] public List<string> Actions { get; set; }
        [JsonProperty("formUrl")] public string FormUrl { get; set; }
        [JsonProperty("requestIdParameter")] public string RequestIdParameter { get; set; }
        [JsonProperty("notification")] public TaskNotificationSettings Notification { get; set; }
    }

    internal sealed class TaskNotificationSettings
    {
        [JsonProperty("enabled")] public bool Enabled { get; set; }
        [JsonProperty("from")] public string From { get; set; }
        [JsonProperty("subject")] public string Subject { get; set; }
        [JsonProperty("body")] public string Body { get; set; }
        [JsonProperty("richText")] public bool RichText { get; set; }
    }

    internal sealed class ApprovalMatrixSettings
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("smartObject")] public string SmartObject { get; set; }
        [JsonProperty("method")] public string Method { get; set; }
        [JsonProperty("matrixCode")] public string MatrixCode { get; set; }
        [JsonProperty("matrixCodeInput")] public string MatrixCodeInput { get; set; }
        [JsonProperty("amountProperty")] public string AmountProperty { get; set; }
        [JsonProperty("amountInput")] public string AmountInput { get; set; }
        [JsonProperty("currentStageInput")] public string CurrentStageInput { get; set; }
        [JsonProperty("hasApproverProperty")] public string HasApproverProperty { get; set; }
        [JsonProperty("stageProperty")] public string StageProperty { get; set; }
        [JsonProperty("approverProperty")] public string ApproverProperty { get; set; }
        [JsonProperty("approverTypeProperty")] public string ApproverTypeProperty { get; set; }
        [JsonProperty("dimensions")] public List<ApprovalMatrixInputMapping> Dimensions { get; set; }
    }

    internal sealed class ApprovalMatrixInputMapping
    {
        [JsonProperty("requestProperty")] public string RequestProperty { get; set; }
        [JsonProperty("inputProperty")] public string InputProperty { get; set; }
    }

    internal sealed class SmartFormsSettings
    {
        [JsonProperty("form")] public string Form { get; set; }
        [JsonProperty("startState")] public string StartState { get; set; }
        [JsonProperty("taskState")] public string TaskState { get; set; }
        [JsonProperty("startRuleContains")] public string StartRuleContains { get; set; }
        [JsonProperty("makeStartStateDefault")] public bool MakeStartStateDefault { get; set; }
        [JsonProperty("workflowStripLocation")] public string WorkflowStripLocation { get; set; }
    }
}
