using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace K2WorkflowCli
{
    internal sealed class WorkflowRenderer
    {
        private readonly WorkflowManifest _manifest;
        private readonly Func<SmartFormsIntegrationDescriptor> _loadSmartForm;
        private SmartObjectDescriptor _smartObject;
        private SmartObjectMethodDescriptor _approvalMatrix;
        private SmartObjectMethodDescriptor _caseLifecycleResolver;
        private SmartObjectMethodDescriptor _caseLifecycleState;

        public WorkflowRenderer(WorkflowManifest manifest, Func<SmartFormsIntegrationDescriptor> loadSmartForm = null)
        {
            _manifest = manifest;
            _loadSmartForm = loadSmartForm;
        }

        public SmartObjectDescriptor SmartObject
        {
            get
            {
                return _smartObject ?? (_smartObject = SmartObjectMetadata.Load(_manifest.K2, _manifest.Workflow.RequestStatusUpdate));
            }
        }

        public SmartObjectMethodDescriptor ApprovalMatrix
        {
            get
            {
                var matrix = _manifest.Workflow.ApprovalMatrix;
                return matrix == null ? null : _approvalMatrix ?? (_approvalMatrix = SmartObjectMetadata.LoadMethod(_manifest.K2, matrix.SmartObject, matrix.Method));
            }
        }

        public JObject Render()
        {
            string json;
            if (string.Equals(_manifest.Workflow.Kind, "json-file", StringComparison.OrdinalIgnoreCase))
            {
                var path = Path.GetFullPath(Path.Combine(_manifest.ManifestDirectory, _manifest.Workflow.DefinitionFile));
                if (!File.Exists(path)) throw new CliException("Workflow definition file not found: " + path);
                json = File.ReadAllText(path);
            }
            else if (string.Equals(_manifest.Workflow.Kind, "request-approval", StringComparison.OrdinalIgnoreCase))
            {
                json = WorkflowJsonBuilder.BuildRequestApproval(_manifest.Workflow, SmartObject, _loadSmartForm == null ? null : _loadSmartForm(), ApprovalMatrix);
            }
            else if (string.Equals(_manifest.Workflow.Kind, "call-subworkflow", StringComparison.OrdinalIgnoreCase))
                json = WorkflowJsonBuilder.BuildCallSubWorkflow(_manifest.Workflow);
            else if (string.Equals(_manifest.Workflow.Kind, "stage-router", StringComparison.OrdinalIgnoreCase))
            {
                json = WorkflowJsonBuilder.BuildStageRouter(_manifest.Workflow);
            }
            else if (string.Equals(_manifest.Workflow.Kind, "stage-task", StringComparison.OrdinalIgnoreCase))
                json = WorkflowJsonBuilder.BuildStageTask(_manifest.Workflow);
            else if (string.Equals(_manifest.Workflow.Kind, "case-lifecycle", StringComparison.OrdinalIgnoreCase))
            {
                if (_caseLifecycleResolver == null) _caseLifecycleResolver = SmartObjectMetadata.LoadMethod(_manifest.K2, _manifest.Workflow.CaseLifecycle.ResolverSmartObject, _manifest.Workflow.CaseLifecycle.ResolverMethod);
                if (_caseLifecycleState == null) _caseLifecycleState = SmartObjectMetadata.LoadMethod(_manifest.K2, _manifest.Workflow.CaseLifecycle.StateSmartObject, _manifest.Workflow.CaseLifecycle.StateMethod);
                json = WorkflowJsonBuilder.BuildCaseLifecycle(_manifest.Workflow, _caseLifecycleResolver, _caseLifecycleState);
            }
            else json = WorkflowJsonBuilder.BuildStartEnd(_manifest.Workflow.Name);
            return WorkflowJsonBuilder.ParseAndValidate(json, _manifest.Workflow.Name);
        }
    }
}
