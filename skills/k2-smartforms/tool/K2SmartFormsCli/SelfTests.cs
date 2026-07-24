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
            TestNativeChartComposition();
            TestMetricCardComposition();
            TestLifecycleComposition();
            TestHiddenPropertyComposition();
            TestEditableListHiddenPropertyComposition();
            TestMalformedEditableListRejected();
            TestViewIdentityRebase();
            TestFlatFormViewOrdering();
            TestMultiTableWorkflowStateReconciliation();
            Console.WriteLine("SELFTEST SUCCEEDED: identity normalization, required/read-only gate, live lookup placement, defaults, master-detail buttons, native chart, metric-card, lifecycle, capture and editable-list hidden-property composition, editable-list add-row default, editable-list structural rejection, identity-preserving View repair rebase, flat Form ordering, multi-table workflow-state reconciliation");
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

        private static void TestNativeChartComposition()
        {
            var view = NewView("Case Trend", "CaseMetrics", "capture", "Period", "Count");
            view.DefaultListMethod = "List";
            view.Charts.Add(new ViewChartDefinition { Name = "chtCaseTrend", Title = "Case intake trend", Type = "line", CategoryProperty = "Period", ValueProperty = "Count", Height = 240 });
            var xml = "<Views><View ID='view-id'><Name>Case Trend</Name><Controls/><Canvas><Sections><Section Type='Body'><Control LayoutType='Grid'><Columns><Column/><Column/></Columns><Rows><Row ID='existing'><Cells><Cell/><Cell/></Cells></Row></Rows></Control></Section></Sections></Canvas><Sources/><Events><Event Type='User' SourceType='View'><Name>Init</Name><Handlers><Handler><Actions><Action ID='a' DefinitionID='d' Type='Execute' ExecutionType='Synchronous'><Properties>" +
                "<Property><Name>Location</Name><Value>View</Value></Property><Property><Name>Method</Name><DisplayValue>List</DisplayValue><NameValue>List</NameValue><Value>List</Value></Property>" +
                "<Property><Name>ViewID</Name><DisplayValue>Case Trend</DisplayValue><NameValue>Case Trend</NameValue><Value>view-id</Value></Property>" +
                "<Property><Name>ObjectID</Name><DisplayValue>Case Metrics</DisplayValue><NameValue>CaseMetrics</NameValue><Value>object-id</Value></Property></Properties><Results/></Action></Actions></Handler></Handlers></Event></Events></View></Views>";
            var transformed = ViewChartLayoutDefinition.Apply(xml, view);
            ViewChartLayoutDefinition.Verify(transformed, view);
            var document = XDocument.Parse(transformed);
            Assert(document.Descendants("Control").Any(x => (string)x.Attribute("Type") == "GenericChart" && (string)x.Element("Name") == "chtCaseTrend"), "native GenericChart emitted");
            Assert(document.Descendants("Cell").Any(x => (string)x.Attribute("ColumnSpan") == "2"), "chart spans generated grid");
        }

        private static void TestMetricCardComposition()
        {
            var view=NewView("Operations KPIs","DashboardSummary","capture","OpenCaseCount","SLAAtRiskCount");view.DefaultListMethod="List";
            view.MetricCards.Add(new ViewMetricCardDefinition{Property="OpenCaseCount",Label="Open cases",Tone="neutral"});
            view.MetricCards.Add(new ViewMetricCardDefinition{Property="SLAAtRiskCount",Label="SLA at risk",Tone="warning"});
            var xml="<Views><View ID='view-id'><Controls/><Canvas><Sections><Section Type='Body'><Control LayoutType='Grid'><Columns><Column/><Column/></Columns><Rows><Row><Cells><Cell/><Cell/></Cells></Row></Rows></Control></Section></Sections></Canvas><Events><Event><Name>Init</Name><Handlers><Handler><Actions><Action ID='a' DefinitionID='d' Type='Execute'><Properties><Property><Name>Method</Name><Value>List</Value></Property></Properties><Results><Result SourceID='object-id' SourceName='OpenCaseCount' SourceDisplayName='OpenCaseCount'/><Result SourceID='object-id' SourceName='SLAAtRiskCount' SourceDisplayName='SLAAtRiskCount'/></Results></Action></Actions></Handler></Handlers></Event></Events></View></Views>";
            var transformed=ViewMetricCardLayoutDefinition.Apply(xml,view);ViewMetricCardLayoutDefinition.Verify(transformed,view);var document=XDocument.Parse(transformed);
            Assert(document.Descendants("Control").Count(x=>(string)x.Attribute("Type")=="DataLabel")==2,"metric-card data labels emitted");
            Assert(document.Descendants("Result").Count(x=>((string)x.Attribute("TargetName")??string.Empty).StartsWith("dlb"))==2,"metric-card results mapped");
        }

        private static void TestLifecycleComposition()
        {
            var view = NewView("Case Header", "Case", "capture", "CaseNumber", "CurrentStageCode");
            var tracker = new ViewLifecycleDefinition { Name = "Case Lifecycle", Property = "CurrentStageCode" };
            tracker.Stages.Add(new ViewLifecycleStageDefinition { Code = "CAPTURE", Label = "Capture" });
            tracker.Stages.Add(new ViewLifecycleStageDefinition { Code = "INVESTIGATE", Label = "Investigate" });
            tracker.Stages.Add(new ViewLifecycleStageDefinition { Code = "CLOSE", Label = "Close" });
            view.LifecycleTrackers.Add(tracker);
            var xml = "<View><Fields><Field ID='case'><FieldName>CaseNumber</FieldName></Field><Field ID='stage'><FieldName>CurrentStageCode</FieldName></Field></Fields><Controls><Control ID='case-control' Type='TextBox' FieldID='case'><Name>CaseNumber</Name><Properties/></Control><Control ID='stage-control' Type='TextBox' FieldID='stage'><Name>CurrentStageCode</Name><Properties/></Control></Controls><Layout><Control ID='case-control'/><Control ID='stage-control'/></Layout></View>";
            var transformed = ViewLifecycleLayoutDefinition.Apply(xml, view);
            ViewLifecycleLayoutDefinition.Verify(transformed, view);
            var document = XDocument.Parse(transformed);
            var progress = document.Descendants("Control").Single(x => (string)x.Attribute("ID") == "stage-control" && x.Attribute("Type") != null);
            Assert((string)progress.Attribute("Type") == "Progress", "lifecycle property control transformed to native Progress");
            Assert((string)progress.Attribute("FieldID") == "stage", "lifecycle SmartObject field binding preserved");
        }

        private static void TestFlatFormViewOrdering()
        {
            var definition = new FormDefinition { Name = "Operations" };
            definition.Views.Add("KPIs");
            definition.Views.Add("Chart");
            definition.Views.Add("Chart Data");
            var xml = "<Forms><Form ID='form'><Controls>" +
                "<Control ID='kpis' Type='AreaItem'><Properties/></Control>" +
                "<Control ID='chart' Type='AreaItem'><Properties/></Control>" +
                "<Control ID='data' Type='AreaItem'><Properties/></Control>" +
                "</Controls><Panels><Panel><Areas>" +
                "<Area><Items><Item ID='data' ViewID='3' ViewName='Chart Data'/></Items></Area>" +
                "<Area><Items><Item ID='chart' ViewID='2' ViewName='Chart'/></Items></Area>" +
                "<Area><Items><Item ID='kpis' ViewID='1' ViewName='KPIs'/></Items></Area>" +
                "</Areas></Panel></Panels></Form></Forms>";
            var transformed = FormLayoutDefinition.Apply(xml, definition, null,
                new Dictionary<string, string>(), new Dictionary<Guid, ResolvedHeaderControlTransfer>());
            FormLayoutDefinition.Verify(transformed, definition, null,
                new Dictionary<string, string>(), new Dictionary<Guid, ResolvedHeaderControlTransfer>());
            var actual = XDocument.Parse(transformed).Descendants("Item").Select(x => (string)x.Attribute("ViewName")).ToList();
            Assert(actual.SequenceEqual(definition.Views), "flat Form areas follow manifest order");
        }

        private static void TestHiddenPropertyComposition()
        {
            var view = NewView("Case Entry", "Case", "capture", "CaseId", "Title");
            view.HiddenProperties.Add("CaseId");
            view.PropertyLabels["Title"] = "Case title";
            var xml = "<View><Fields><Field ID='case-id'><FieldName>CaseId</FieldName></Field><Field ID='title'><FieldName>Title</FieldName></Field></Fields><Controls>" +
                "<Control ID='case-label' Type='Label'><Properties><Property><Name>Text</Name><Value>Case Id</Value></Property></Properties></Control>" +
                "<Control ID='case-control' Type='TextBox' FieldID='case-id'><Properties/></Control>" +
                "<Control ID='title-label' Type='Label'><Properties><Property><Name>Text</Name><Value>Title</Value></Property></Properties></Control>" +
                "<Control ID='title-control' Type='TextBox' FieldID='title'><Properties/></Control></Controls>" +
                "<Canvas><Sections><Section Type='Body'><Control LayoutType='Grid'><Columns><Column ID='column-1'/><Column ID='column-2'/></Columns><Rows>" +
                "<Row><Cells><Cell><Control ID='case-label'/></Cell><Cell><Control ID='case-control'/></Cell></Cells></Row>" +
                "<Row><Cells><Cell><Control ID='title-label'/></Cell><Cell><Control ID='title-control'/></Cell></Cells></Row>" +
                "</Rows></Control></Section></Sections></Canvas></View>";
            var transformed = ViewPresentationDefinition.Apply(xml, view, false, false);
            ViewPresentationDefinition.Verify(transformed, view, false, false);
            var document = XDocument.Parse(transformed);
            Assert(!document.Descendants("Row").Any(row => row.Descendants("Control").Any(control => (string)control.Attribute("ID") == "case-control")), "hidden property row removed");
            Assert(document.Descendants("Row").Any(row => row.Descendants("Control").Any(control => (string)control.Attribute("ID") == "title-control")), "visible property row retained");
            Assert(document.Descendants("Control").Any(control => (string)control.Attribute("ID") == "title-label" && control.Descendants("Property").Any(property => (string)property.Element("Name") == "Text" && (string)property.Element("Value") == "Case title")), "friendly property label applied");
        }

        private static void TestEditableListHiddenPropertyComposition()
        {
            var view = NewView("Evidence", "Evidence", "capture-list", "First", "Middle", "Last");
            view.HiddenProperties.Add("Middle");
            var source = EditableListXml(false);
            var transformed = ViewPresentationDefinition.Apply(source, view, false, false);
            ViewPresentationDefinition.Verify(transformed, view, false, false);

            var document = XDocument.Parse(transformed);
            var body = document.Descendants("Section").Single(x => (string)x.Attribute("Type") == "Body").Element("Control");
            var rows = body.Element("Rows").Elements("Row").ToList();
            var columns = body.Element("Columns").Elements("Column").ToList();
            Assert(rows.Count == 4, "editable-list Header, Display, Footer, and Edit rows retained");
            Assert(rows.All(row => row.Element("Cells").Elements("Cell").Count() == 2), "editable-list template cells reduced from three to two");
            Assert(columns.Count == 2, "editable-list columns reduced from three to two");
            Assert(document.Descendants("Control").Count(control => (string)control.Attribute("Type") == "Column") == 2,
                "editable-list Column control definitions reduced from three to two");
            Assert(document.Descendants("Control").Count(control => (string)control.Attribute("Type") == "Cell") == 8,
                "editable-list Cell control definitions reduced from twelve to eight");
            Assert(columns.Sum(column => int.Parse(((string)column.Attribute("Size")).TrimEnd('%'))) == 100, "editable-list widths total 100 percent");
            Assert(!document.Descendants("Control").Single(control => (string)control.Attribute("Type") == "View")
                .Descendants("Property").Any(property => (string)property.Element("Name") == "ShowAddRow"),
                "editable-list Add new row link disabled by omitting ShowAddRow");

            foreach (var visible in new[] { "first", "last" })
            {
                Assert(rows.Where(row => Template(document, row) != "Footer").All(row =>
                    row.Descendants("Control").Any(control => ((string)control.Attribute("ID") ?? string.Empty).StartsWith(visible + "-", StringComparison.Ordinal))),
                    "visible editable-list property '" + visible + "' retains Header, Display, and Edit placement");
            }
            Assert(!body.Descendants("Control").Any(control => ((string)control.Attribute("ID") ?? string.Empty).StartsWith("middle-", StringComparison.Ordinal)),
                "hidden editable-list property has no visible placement");
            Assert(document.Descendants("Field").Any(field => (string)field.Element("FieldName") == "Middle"), "hidden editable-list field definition retained");
            Assert(document.Descendants("Control").Any(control => (string)control.Attribute("FieldID") == "field-middle" && control.Attribute("Type") != null),
                "hidden editable-list field-bound controls retained");
            Assert(document.Descendants("Parameter").Any(parameter => (string)parameter.Attribute("TargetID") == "Middle"),
                "hidden editable-list method input mapping retained");
            Assert(document.Descendants("Result").Any(result => (string)result.Attribute("SourceID") == "Middle"),
                "hidden editable-list method result mapping retained");

            var secondPass = ViewPresentationDefinition.Apply(transformed, view, false, false);
            ViewPresentationDefinition.Verify(secondPass, view, false, false);
            Assert(string.Equals(transformed, secondPass, StringComparison.Ordinal), "editable-list hidden-property transformation is idempotent");

            var allPropertiesView = NewView("All Evidence", "Evidence", "capture-list");
            allPropertiesView.Options.Add("all-properties");
            var allProperties = ViewPresentationDefinition.Apply(source, allPropertiesView, false, false);
            ViewPresentationDefinition.Verify(allProperties, allPropertiesView, false, false);
        }

        private static void TestMalformedEditableListRejected()
        {
            var view = NewView("Malformed Evidence", "Evidence", "capture-list", "First", "Middle", "Last");
            AssertThrows(delegate { ViewPresentationDefinition.Apply(EditableListXml(true), view, false, false); }, "exactly one Header");

            var addRowEnabled = EditableListXml(false);
            AssertThrows(delegate { ViewPresentationDefinition.Verify(addRowEnabled, view, false, false); }, "omitting the ShowAddRow property");
        }

        private static string EditableListXml(bool malformed)
        {
            var names = new[] { "First", "Middle", "Last" };
            var keys = new[] { "first", "middle", "last" };
            var sizes = new[] { "34%", "33%", "33%" };
            var view = new XElement("View",
                new XElement("Fields"),
                new XElement("Controls",
                    new XElement("Control", new XAttribute("ID", "view-control"), new XAttribute("Type", "View"),
                        new XElement("Name", "Evidence"),
                        new XElement("Properties",
                            new XElement("Property",
                                new XElement("Name", "ShowAddRow"),
                                new XElement("DisplayValue", "true"),
                                new XElement("Value", "true"))))),
                new XElement("Canvas",
                    new XElement("Sections",
                        new XElement("Section", new XAttribute("Type", "Body"),
                            new XElement("Control", new XAttribute("ID", "body"), new XAttribute("LayoutType", "Grid"),
                                new XElement("Columns"),
                                new XElement("Rows"))))),
                new XElement("Events",
                    new XElement("Event",
                        new XElement("Action",
                            new XElement("Parameters"),
                            new XElement("Results")))));
            var fields = view.Element("Fields");
            var controls = view.Element("Controls");
            var body = view.Descendants("Section").Single().Element("Control");
            var columns = body.Element("Columns");
            var rows = body.Element("Rows");
            var parameters = view.Descendants("Parameters").Single();
            var results = view.Descendants("Results").Single();

            controls.Add(ControlDefinition("body", "ListTable", null));
            for (var i = 0; i < names.Length; i++)
            {
                fields.Add(new XElement("Field", new XAttribute("ID", "field-" + keys[i]), new XElement("FieldName", names[i])));
                columns.Add(new XElement("Column", new XAttribute("ID", "column-" + keys[i]), new XAttribute("Size", sizes[i])));
                controls.Add(ControlDefinition("column-" + keys[i], "Column", sizes[i]));
                controls.Add(FieldControlDefinition(keys[i] + "-header", "Label", "field-" + keys[i]));
                controls.Add(FieldControlDefinition(keys[i] + "-display", "DataLabel", "field-" + keys[i]));
                controls.Add(FieldControlDefinition(keys[i] + "-edit", "TextBox", "field-" + keys[i]));
                parameters.Add(new XElement("Parameter", new XAttribute("SourceID", "field-" + keys[i]), new XAttribute("TargetID", names[i])));
                results.Add(new XElement("Result", new XAttribute("SourceID", names[i]), new XAttribute("TargetID", "field-" + keys[i])));
            }

            foreach (var template in new[] { "Header", "Display", "Footer", "Edit" })
            {
                var rowKey = template.ToLowerInvariant();
                controls.Add(ControlDefinition("row-" + rowKey, "Row", template));
                var cells = new XElement("Cells");
                for (var i = 0; i < names.Length; i++)
                {
                    var cellId = "cell-" + rowKey + "-" + keys[i];
                    controls.Add(ControlDefinition(cellId, "Cell", null));
                    var cell = new XElement("Cell", new XAttribute("ID", cellId));
                    if (template != "Footer")
                    {
                        var suffix = template == "Header" ? "header" : template == "Display" ? "display" : "edit";
                        cell.Add(new XElement("Control", new XAttribute("ID", keys[i] + "-" + suffix)));
                    }
                    cells.Add(cell);
                }
                if (!malformed || template == "Footer")
                    rows.Add(new XElement("Row", new XAttribute("ID", "row-" + rowKey), cells));
            }

            return new XDocument(view).ToString(SaveOptions.DisableFormatting);
        }

        private static void TestViewIdentityRebase()
        {
            var generatedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var expectedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var source = new XDocument(
                new XElement("SourceCode.Forms",
                    new XElement("Views",
                        new XElement("View", new XAttribute("ID", generatedId),
                            new XElement("Name", "Evidence1"),
                            new XElement("DisplayName", "Evidence1"),
                            new XElement("Controls",
                                new XElement("Control", new XAttribute("ID", generatedId), new XAttribute("Type", "View"))),
                            new XElement("Events",
                                new XElement("Event", new XAttribute("SourceID", generatedId),
                                    new XElement("Property", new XElement("Value", generatedId))))))));
            var rebased = SmartFormsManager.RebaseViewIdentity(
                source.ToString(SaveOptions.DisableFormatting), expectedId, "Evidence");
            var document = XDocument.Parse(rebased);
            var view = document.Descendants("View").Single();
            Assert((string)view.Attribute("ID") == expectedId.ToString(), "View repair root identity rebased");
            Assert((string)view.Element("Name") == "Evidence" && (string)view.Element("DisplayName") == "Evidence",
                "View repair exact name/display name restored");
            Assert(document.Descendants("Control").Single().Attribute("ID").Value == expectedId.ToString(),
                "View repair root control self-reference rebased");
            Assert(rebased.IndexOf(generatedId.ToString(), StringComparison.OrdinalIgnoreCase) < 0,
                "View repair old generated identity fully removed");

            var compositeDocument = XDocument.Parse(source.ToString(SaveOptions.DisableFormatting));
            compositeDocument.Descendants("Event").Single().SetAttributeValue("Composite", generatedId + "-suffix");
            var composite = compositeDocument.ToString(SaveOptions.DisableFormatting);
            AssertThrows(delegate
            {
                SmartFormsManager.RebaseViewIdentity(composite, expectedId, "Evidence");
            }, "composite self-reference");
        }

        private static XElement ControlDefinition(string id, string type, string value)
        {
            var properties = new XElement("Properties");
            if (value != null)
                properties.Add(new XElement("Property", new XElement("Name", type == "Row" ? "Template" : "Size"), new XElement("Value", value)));
            return new XElement("Control", new XAttribute("ID", id), new XAttribute("Type", type), new XElement("Name", id), properties);
        }

        private static XElement FieldControlDefinition(string id, string type, string fieldId)
        {
            return new XElement("Control", new XAttribute("ID", id), new XAttribute("Type", type), new XAttribute("FieldID", fieldId),
                new XElement("Name", id), new XElement("Properties"));
        }

        private static string Template(XDocument document, XElement row)
        {
            var id = (string)row.Attribute("ID");
            var definition = document.Descendants("Control").First(control =>
                control.Attribute("Type") != null && (string)control.Attribute("ID") == id);
            var property = definition.Descendants("Property").First(item => (string)item.Element("Name") == "Template");
            return (string)property.Element("Value");
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
