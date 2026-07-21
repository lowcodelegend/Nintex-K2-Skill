using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class SelfTests
    {
        public static void Run()
        {
            TestIdentityNormalization();
            TestRequiredReadOnlyCreateInputGate();
            TestLookupAndDefaultValueRoundTrip();
            TestMasterButtonSuppression();
            TestMultiTableWorkflowStateReconciliation();
            Console.WriteLine("SELFTEST SUCCEEDED: identity normalization, required/read-only gate, live lookup placement, defaults, master-detail buttons, multi-table workflow-state reconciliation");
        }

        private static void TestIdentityNormalization()
        {
            Assert(ResolvedMasterDetailRules.NormalizeConditionDataType("AutoNumber") == "Number", "AutoNumber normalization");
            Assert(ResolvedMasterDetailRules.NormalizeConditionDataType("Autonumber") == "Number", "Autonumber normalization");
            Assert(ResolvedMasterDetailRules.NormalizeConditionDataType("AutoGuid") == "Guid", "AutoGuid normalization");
        }

        private static void TestRequiredReadOnlyCreateInputGate()
        {
            var view = NewView("Claim Editor", "Claim", "capture", "Status");
            view.ReadOnlyProperties.Add("Status");
            AssertThrows(delegate
            {
                SmartFormsManager.ValidateRequiredReadOnlyCreateInputs(view, "Create", "Create", new[] { "Status" }, new string[0]);
            }, "read-only without a supplied value");
            view.DefaultValues["Status"] = "Draft";
            SmartFormsManager.ValidateRequiredReadOnlyCreateInputs(view, "Create", "Create", new[] { "Status" }, new string[0]);
            view.DefaultValues.Clear();
            SmartFormsManager.ValidateRequiredReadOnlyCreateInputs(view, "Update", "Update", new[] { "Status" }, new string[0]);
        }

        private static void TestLookupAndDefaultValueRoundTrip()
        {
            var view = NewView("Claim Lines", "ExpenseLine", "capture-list", "CategoryCode", "Status");
            view.LookupControls.Add(new LookupControlDefinition { Property = "CategoryCode", Lookup = "Category", AllowEmptySelection = false });
            view.ReadOnlyProperties.Add("Status");
            view.DefaultValues["Status"] = "Draft";
            var source = new LookupRuntimeSource
            {
                Name = "Category",
                SmartObjectGuid = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                SmartObjectSystemName = "EXP_Category",
                SmartObjectDisplayName = "Expense Category",
                MethodName = "List",
                MethodDisplayName = "List",
                ValuePropertyName = "CategoryCode",
                ValuePropertyDisplayName = "Category Code",
                DisplayPropertyName = "CategoryName",
                DisplayPropertyDisplayName = "Category Name",
                DisplayPropertyType = "Text"
            };
            var sources = new Dictionary<string, LookupRuntimeSource>(StringComparer.OrdinalIgnoreCase) { { "Category", source } };
            var xml = ViewLookupDefinition.Apply(ViewXml(), view, sources);
            ViewLookupDefinition.Verify(xml, view, sources);

            var document = XDocument.Parse(xml);
            document.Descendants("Layout").Elements("Control").Remove();
            AssertThrows(delegate { ViewLookupDefinition.Verify(document.ToString(), view, sources); }, "not placed in the live View layout");
        }

        private static void TestMasterButtonSuppression()
        {
            var view = NewView("Claim Editor", "Claim", "list");
            var xml = "<View><Controls>" +
                "<Control ID='create' Type='Button'><Name>Create</Name><Properties><Property><Name>Text</Name><Value>Create</Value></Property></Properties></Control>" +
                "<Control ID='save' Type='ToolBarButton'><Name>Save</Name><Properties><Property><Name>Text</Name><Value>Save</Value></Property></Properties></Control>" +
                "</Controls></View>";
            var transformed = ViewPresentationDefinition.Apply(xml, view, true, false);
            ViewPresentationDefinition.Verify(transformed, view, true, false);
            var document = XDocument.Parse(transformed);
            var hidden = 0;
            foreach (var control in document.Descendants("Control"))
                foreach (var property in control.Descendants("Property"))
                    if ((string)property.Element("Name") == "IsVisible" && (string)property.Element("Value") == "false") hidden++;
            Assert(hidden == 2, "Button and ToolBarButton suppression");
        }

        private static void TestMultiTableWorkflowStateReconciliation()
        {
            var masterGuid = Guid.Parse("10000000-0000-0000-0000-000000000001");
            var firstGuid = Guid.Parse("20000000-0000-0000-0000-000000000001");
            var secondGuid = Guid.Parse("30000000-0000-0000-0000-000000000001");
            var contract = new MasterDetailFormDefinition { MasterView = "Request", MasterKeyProperty = "RequestId", MasterReadMethod = "Read" };
            var first = new MasterDetailChildDefinition { View = "Lines", ForeignKeyProperty = "RequestId", ListMethod = "List" };
            var second = new MasterDetailChildDefinition { View = "Attachments", ForeignKeyProperty = "RequestId", ListMethod = "List" };
            contract.Details.Add(first); contract.Details.Add(second);
            var form = new FormDefinition { Name = "Request Form", MasterDetail = contract };
            var resolved = new ResolvedMasterDetailRules
            {
                Definition = contract, MasterViewGuid = masterGuid, MasterViewName = "Request",
                MasterKey = new ResolvedViewField { Id = "requestIdField", Name = "RequestId", DisplayName = "Request ID", DataType = "Number" },
                Details = new List<ResolvedMasterDetailChild>
                {
                    new ResolvedMasterDetailChild { Definition = first, ViewGuid = firstGuid, ViewName = "Lines", ViewDisplayName = "Lines" },
                    new ResolvedMasterDetailChild { Definition = second, ViewGuid = secondGuid, ViewName = "Attachments", ViewDisplayName = "Attachments" }
                }
            };
            var xml = "<Form><Items>" +
                "<Item ID='master' ViewID='" + masterGuid + "' ViewName='Request'/>" +
                "<Item ID='lines' ViewID='" + firstGuid + "' ViewName='Lines'/>" +
                "<Item ID='attachments' ViewID='" + secondGuid + "' ViewName='Attachments'/>" +
                "</Items><States>" + WorkflowStateXml("base", "StartProcess") + WorkflowStateXml("task", "ActionProcess") + "</States></Form>";
            bool changed;
            var reconciled = MasterDetailRules.ReconcileDetailLoads(xml, form, resolved, out changed);
            Assert(changed, "workflow integration drift must be reconciled");
            var document = XDocument.Parse(reconciled);
            Assert(document.Descendants("Action").Count(x => (string)x.Attribute("Type") == "StartProcess") == 1, "StartProcess action preserved");
            Assert(document.Descendants("Action").Count(x => (string)x.Attribute("Type") == "ActionProcess") == 1, "ActionProcess action preserved");
            Assert(document.Descendants("Action").Count(x => (string)x.Attribute("Type") == "Execute" && ReadMethod(x) == "List") == 4, "two detail tables on two master Read paths");
            bool changedAgain;
            var secondPass = MasterDetailRules.ReconcileDetailLoads(reconciled, form, resolved, out changedAgain);
            Assert(!changedAgain && string.Equals(reconciled, secondPass, StringComparison.Ordinal), "master-detail reconciliation is idempotent");
        }

        private static string WorkflowStateXml(string id, string workflowActionType)
        {
            return "<State ID='" + id + "'><Name>" + id + "</Name><Events><Event><Handlers>" +
                "<Handler ID='read-" + id + "'><Actions><Action ID='read-action-" + id + "' Type='Execute' InstanceID='master'><Properties><Property><Name>Method</Name><Value>Read</Value></Property></Properties></Action></Actions></Handler>" +
                "<Handler ID='list-lines-" + id + "'><Actions><Action ID='list-lines-action-" + id + "' Type='Execute' InstanceID='lines'><Properties><Property><Name>Method</Name><Value>List</Value></Property></Properties></Action></Actions></Handler>" +
                "<Handler ID='list-attachments-" + id + "'><Actions><Action ID='list-attachments-action-" + id + "' Type='Execute' InstanceID='attachments'><Properties><Property><Name>Method</Name><Value>List</Value></Property></Properties></Action></Actions></Handler>" +
                "<Handler ID='workflow-" + id + "'><Actions><Action ID='workflow-action-" + id + "' Type='" + workflowActionType + "'><Properties><Property><Name>Marker</Name><Value>preserve</Value></Property></Properties></Action></Actions></Handler>" +
                "</Handlers></Event></Events></State>";
        }

        private static string ReadMethod(XElement action)
        {
            var property = action.Descendants("Property").FirstOrDefault(x => (string)x.Element("Name") == "Method");
            return property == null ? null : (string)property.Element("Value");
        }

        private static ViewDefinition NewView(string name, string smartObject, string type, params string[] properties)
        {
            var view = new ViewDefinition { Name = name, SmartObject = smartObject, Type = type };
            view.Properties.AddRange(properties);
            return view;
        }

        private static string ViewXml()
        {
            return "<View><Fields>" +
                "<Field ID='category'><FieldName>CategoryCode</FieldName></Field>" +
                "<Field ID='status'><FieldName>Status</FieldName></Field>" +
                "</Fields><Controls>" +
                "<Control ID='categoryControl' Type='TextBox' FieldID='category'><Name>CategoryCode</Name><Properties /></Control>" +
                "<Control ID='statusControl' Type='TextBox' FieldID='status'><Name>Status</Name><Properties /></Control>" +
                "</Controls><Layout><Control ID='categoryControl' /><Control ID='statusControl' /></Layout></View>";
        }

        private static void AssertThrows(Action action, string messagePart)
        {
            try { action(); }
            catch (CliException ex)
            {
                if (ex.Message.IndexOf(messagePart, StringComparison.OrdinalIgnoreCase) >= 0) return;
                throw new CliException("Self-test expected error containing '" + messagePart + "' but received: " + ex.Message);
            }
            throw new CliException("Self-test expected an error containing '" + messagePart + "'.");
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new CliException("Self-test failed: " + name + ".");
        }
    }
}
