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
            TestCategoryCleanupPaths();
            TestViewOwnedMasterDetailRules();
            TestMasterDetailValidationComposition();
            TestReusableViewValidationPatternOwnership();
            TestMissingOptionalControlMappings();
            TestRequiredReadOnlyCreateInputGate();
            TestLookupAndDefaultValueRoundTrip();
            TestGovernedLookupValidationComposition();
            TestMasterButtonSuppression();
            TestNativeChartComposition();
            TestMetricCardComposition();
            TestLifecycleComposition();
            TestGuidedJourneyComposition();
            TestGuidedJourneyCompletionComposition();
            TestWorkflowStartActionAlignment();
            TestFormControlAvailability();
            TestFormBooleanControlProperties();
            TestModernWebComponentComposition();
            TestHiddenPropertyComposition();
            TestLabelAboveHiddenCellComposition();
            TestResponsiveGroupedItemView();
            TestEditableListHiddenPropertyComposition();
            TestEditableListFileValidationControl();
            TestMalformedEditableListRejected();
            TestViewIdentityRebase();
            TestFlatFormViewOrdering();
            TestConditionalFormFrameworkSelection();
            TestRedundantFormFrameworkRemoval();
            TestFormPreFillRules();
            TestReplacementRecoveryClassifier();
            TestMultiTableWorkflowStateReconciliation();
            Console.WriteLine("SELFTEST SUCCEEDED: identity normalization, bounded bottom-up category cleanup paths, View-owned master-detail event seams, mutually exclusive native If/Else Save branching, post-Create master hydration and Form method-action rejection, master-detail field-validation composition, reusable-View validation-pattern cleanup ownership, orphan optional control mappings, lookup/detail List classification, governed lookup-domain validation, required/read-only gate, live lookup placement, literal Create defaults, responsive two-column label-above sections, colon labels, semantic TextBox inputs, native max-length/validation-pattern contracts, must-be-true checkbox validation groups, required controls, help popups, semantic native action-cell alignment, master-detail buttons, native chart, metric-card, lifecycle, Form-supported guided-journey progress/current-screen navigation and workflow-free completion validation, Form-level control-metadata availability and browser Boolean property triples, modern Web Component full-body placement, capture and editable-list hidden-property composition, editable-list File edit-template validation, label-above hidden-cell preservation, editable-list add-row default, editable-list structural rejection, identity-preserving View repair rebase, flat Form ordering, conditional per-Form framework selection, redundant profile/header/footer removal, constraint-aware test-data Pre-fill, multi-table workflow-state reconciliation");
        }

        private static void TestCategoryCleanupPaths()
        {
            var application = new ApplicationOptions { RootCategoryPath = @"K2 Skills\APP.Application\\" };
            var leafOnly = SmartFormsManager.CleanupCategoryPaths(application, false);
            Assert(leafOnly.SequenceEqual(new[]
            {
                @"K2 Skills\APP.Application\Admin\Forms",
                @"K2 Skills\APP.Application\Admin\Views",
                @"K2 Skills\APP.Application\Admin",
                @"K2 Skills\APP.Application\Forms",
                @"K2 Skills\APP.Application\Views"
            }), "standalone cleanup owns only fixed SmartForms category descendants");
            var complete = SmartFormsManager.CleanupCategoryPaths(application, true);
            Assert(complete.Last() == @"K2 Skills\APP.Application" && complete.Count == 6,
                "builder cleanup deletes the application root last");
        }

        private static void TestReplacementRecoveryClassifier()
        {
            Assert(SmartFormsManager.IsKnownStaleReplacementFailure(
                    new NullReferenceException()),
                "replacement recovery recognizes a direct stale-session null reference");
            Assert(SmartFormsManager.IsKnownStaleReplacementFailure(
                    new InvalidOperationException("wrapped",
                        new NullReferenceException())),
                "replacement recovery recognizes a wrapped stale-session null reference");
            Assert(!SmartFormsManager.IsKnownStaleReplacementFailure(
                    new InvalidOperationException("authorization failed")),
                "replacement recovery does not hide unrelated deployment failures");
        }

        private static void TestReusableViewValidationPatternOwnership()
        {
            var manifest = new SmartFormsManifest();
            var owned = new ViewDefinition { Name = "Owned View" };
            owned.Validations.Add(new FieldValidationDefinition
            {
                Property = "Email",
                Pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$"
            });
            var reusable = new ViewDefinition { Name = "Reusable View", ReuseExisting = true };
            reusable.Validations.Add(new FieldValidationDefinition
            {
                Property = "Narrative",
                Pattern = @".+"
            });
            manifest.Application.Views.Add(owned);
            manifest.Application.Views.Add(reusable);

            var manager = new SmartFormsManager(manifest);
            Assert(manager.PatternValidations().Count() == 2,
                "verification includes validation-pattern contracts declared by reusable Views");
            var cleanupContracts = manager.PatternValidations(false).ToList();
            Assert(cleanupContracts.Count == 1 && cleanupContracts[0].Key == owned,
                "cleanup owns validation patterns only for Views that the manifest owns");
        }

        private static void TestConditionalFormFrameworkSelection()
        {
            var application = new ApplicationOptions { StyleProfile = "Application Style" };
            var form = new FormDefinition { Name = "Plain Form" };
            Assert(FormFrameworkUsage.UsesStyleProfile(application, form),
                "an explicitly selected application Style Profile applies by default");
            Assert(!FormFrameworkUsage.UsesCommonHeader(application, form),
                "an environment-selected common header does not leak into an undeclared Form");
            Assert(!FormFrameworkUsage.UsesCommonFooter(application, form, true),
                "an environment-selected common footer does not leak into an undeclared Form");

            form.UseStyleProfile = false;
            form.UseCommonHeader = true;
            form.UseCommonFooter = false;
            Assert(!FormFrameworkUsage.UsesStyleProfile(application, form),
                "a Form can explicitly decline the application Style Profile");
            Assert(FormFrameworkUsage.UsesCommonHeader(application, form),
                "a Form can explicitly opt into the environment common header");
            Assert(!FormFrameworkUsage.UsesCommonFooter(application, form, true),
                "a Form can use the header without the footer");

            application.CommonHeader = new CommonHeaderDefinition { Enabled = true };
            var inherited = new FormDefinition { Name = "Inherited Framework Form" };
            Assert(FormFrameworkUsage.UsesCommonHeader(application, inherited),
                "an explicitly selected application common header applies by default");
            Assert(FormFrameworkUsage.UsesCommonFooter(application, inherited, true),
                "the selected application footer applies by default when available");
            inherited.UseCommonHeader = false;
            Assert(!FormFrameworkUsage.UsesCommonFooter(application, inherited, true),
                "declining the header also declines the footer");
        }

        private static void TestRedundantFormFrameworkRemoval()
        {
            var headerGuid = Guid.Parse("71000000-0000-0000-0000-000000000001");
            var footerGuid = Guid.Parse("71000000-0000-0000-0000-000000000002");
            var styleGuid = Guid.Parse("71000000-0000-0000-0000-000000000003");
            var xml = "<Forms><Form ID='form-id'><Name>Conditional Framework Form</Name>" +
                "<Controls><Control ID='form-control' Type='Form'><Properties><Property><Name>StyleProfile</Name><DisplayValue>Old Style</DisplayValue><NameValue>Old Style</NameValue><Value>" + styleGuid + "</Value></Property></Properties></Control>" +
                "<Control ID='header-area' Type='Area'/><Control ID='header-instance' Type='AreaItem'/>" +
                "<Control ID='body-area' Type='Area'/><Control ID='body-instance' Type='AreaItem'/>" +
                "<Control ID='footer-area' Type='Area'/><Control ID='footer-instance' Type='AreaItem'/></Controls>" +
                "<Panels><Panel ID='panel'><Areas>" +
                "<Area ID='header-area'><Items><Item ID='header-instance' ViewID='" + headerGuid + "' ViewName='Shared Header'/></Items></Area>" +
                "<Area ID='body-area'><Items><Item ID='body-instance' ViewID='72000000-0000-0000-0000-000000000001' ViewName='Business View'/></Items></Area>" +
                "<Area ID='footer-area'><Items><Item ID='footer-instance' ViewID='" + footerGuid + "' ViewName='Shared Footer'/></Items></Area>" +
                "</Areas></Panel></Panels>" +
                "<States><State><Events>" +
                "<Event ID='header-rule' Type='User' SourceType='View' InstanceID='header-instance'><Handlers><Handler><Actions/></Handler></Handlers></Event>" +
                "<Event ID='form-rule' Type='User' SourceType='Form'><Handlers><Handler><Actions>" +
                "<Action ID='header-call' Type='Execute' InstanceID='header-instance'/>" +
                "<Action ID='footer-transfer' Type='Transfer'><Parameters><Parameter TargetInstanceID='footer-instance'/></Parameters></Action>" +
                "<Action ID='business-action' Type='Execute' InstanceID='body-instance'/></Actions></Handler></Handlers></Event>" +
                "</Events></State></States></Form></Forms>";

            bool styleChanged;
            var withoutStyle = FormThemeDefinition.RemoveStyleProfile(xml, out styleChanged);
            Assert(styleChanged && FormThemeDefinition.ReadStyleProfile(withoutStyle) == null,
                "redundant Style Profile property is removed");

            bool frameworkChanged;
            var reconciled = FormLayoutDefinition.RemoveFrameworkViews(withoutStyle,
                new[] { headerGuid, footerGuid }, out frameworkChanged);
            Assert(frameworkChanged, "redundant header/footer are removed");
            var document = XDocument.Parse(reconciled);
            Assert(!document.Descendants("Item").Any(x =>
                string.Equals((string)x.Attribute("ViewID"), headerGuid.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals((string)x.Attribute("ViewID"), footerGuid.ToString(), StringComparison.OrdinalIgnoreCase)),
                "removed framework Views have no remaining layout instances");
            Assert(!document.Descendants("Control").Any(x =>
                new[] { "header-area", "header-instance", "footer-area", "footer-instance" }
                    .Contains((string)x.Attribute("ID"), StringComparer.OrdinalIgnoreCase)),
                "removed framework Views have no orphan Form controls");
            Assert(!document.Descendants("Event").Any(x => (string)x.Attribute("ID") == "header-rule") &&
                !document.Descendants("Action").Any(x =>
                    (string)x.Attribute("ID") == "header-call" || (string)x.Attribute("ID") == "footer-transfer"),
                "removed framework Views have no orphan lifecycle rules or actions");
            Assert(document.Descendants("Item").Any(x => (string)x.Attribute("ViewName") == "Business View") &&
                document.Descendants("Action").Any(x => (string)x.Attribute("ID") == "business-action"),
                "business layout and actions survive framework cleanup");
            FormLayoutDefinition.VerifyFrameworkViewsAbsent(reconciled, new[] { headerGuid, footerGuid },
                "Conditional Framework Form");

            bool changedAgain;
            var secondPass = FormLayoutDefinition.RemoveFrameworkViews(reconciled,
                new[] { headerGuid, footerGuid }, out changedAgain);
            Assert(!changedAgain && string.Equals(reconciled, secondPass, StringComparison.Ordinal),
                "framework cleanup is idempotent");
        }

        private static void TestModernWebComponentComposition()
        {
            var view = new ViewDefinition { Name = "Northstar Home", Type = "capture" };
            view.WebComponents.Add(new ViewWebComponentDefinition
            {
                Name = "Northstar Command Palette",
                ControlType = "northstar-command-palette",
                ReplaceBody = true,
                Properties = new Dictionary<string, string>
                {
                    { "Suggestions", "[]" },
                    { "Value", "" },
                    { "TagName", "northstar-command-palette" },
                    { "RuntimeScriptFileNames", "northstar-command-runtime.js" },
                    { "DesigntimeScriptFileNames", "northstar-command-designtime.js" }
                },
                DataBinding = new ViewWebComponentDataBindingDefinition
                {
                    Property = "Suggestions",
                    Method = "List",
                    ServerUserScoped = true
                },
                Events = new List<ViewWebComponentEventDefinition>
                {
                    new ViewWebComponentEventDefinition
                    {
                        Name = "Navigate",
                        Action = "navigate",
                        SourceProperty = "Value",
                        Target = "_self"
                    }
                }
            });
            var xml = "<Views><View ID='" + Guid.NewGuid() + "'><Name>Northstar Home</Name><DisplayName>Northstar Home</DisplayName>" +
                "<Controls><Control ID='view-control' Type='View'/><Control ID='table-control' Type='Table'/></Controls>" +
                "<Canvas><Sections><Section ID='toolbar' Type='ToolBar'><Control ID='toolbar-table'/></Section>" +
                "<Section ID='body' Type='Body'><Control ID='table-control'><Rows/></Control></Section></Sections></Canvas>" +
                "<Sources><Source ID='source' SourceID='11111111-1111-1111-1111-111111111111' SourceName='NorthstarSuggestions' SourceDisplayName='Northstar Suggestions' SourceType='Object' ContextType='Primary'><Fields>" +
                "<Field ID='suggestion-code' Type='ObjectProperty' DataType='Text'><Name>NorthstarSuggestions.SuggestionCode</Name><FieldName>SuggestionCode</FieldName><FieldDisplayName>Suggestion code</FieldDisplayName></Field>" +
                "</Fields></Source></Sources><Events/></View></Views>";
            var transformed = ViewWebComponentLayoutDefinition.Apply(xml, view);
            ViewWebComponentLayoutDefinition.Verify(transformed, view);
            var document = XDocument.Parse(transformed);
            Assert(document.Descendants("Section").Count() == 1 &&
                (string)document.Descendants("Section").Single().Attribute("Type") == "Body",
                "Web Component removes generated toolbar placement");
            Assert(document.Descendants("Control").Count(control =>
                (string)control.Attribute("Type") == "northstar-command-palette") == 1,
                "Web Component is represented by its registered custom-element type");
            var custom = document.Descendants("Control").Single(control =>
                (string)control.Attribute("Type") == "northstar-command-palette");
            var customId = (string)custom.Attribute("ID");
            Assert(document.Descendants("Source").Count(source =>
                (string)source.Attribute("ContextType") == "Association" &&
                (string)source.Attribute("ContextID") == customId) == 1,
                "Web Component has one Designer-hydratable SmartObject association");
            var fieldIds = document.Descendants("Field").Where(field => field.Attribute("ID") != null)
                .Select(field => ((string)field.Attribute("ID")).ToLowerInvariant()).ToList();
            Assert(fieldIds.Count == fieldIds.Distinct().Count(),
                "Web Component SmartObject association has unique View Field IDs");
            Assert(document.Descendants("Event").Count(rule =>
                (string)rule.Attribute("SourceType") == "View" &&
                (string)rule.Element("Name") == "Init" &&
                rule.Descendants("Action").Count(action =>
                    (string)action.Attribute("Type") == "Execute" &&
                    ReadActionProperty(action, "ControlID") == customId &&
                    ReadActionProperty(action, "Method") == "List") == 1) == 1,
                "Web Component has one View Init list-data action");
            Assert(document.Descendants("Event").All(rule =>
                (string)rule.Attribute("SourceID") != customId ||
                (string)rule.Element("Name") != "Initializing"),
                "Web Component does not depend on a synthetic Initializing control event");
            Assert(document.Descendants("Event").Count(rule =>
                (string)rule.Attribute("SourceID") == customId &&
                (string)rule.Element("Name") == "Navigate" &&
                rule.Descendants("Action").Any(action => (string)action.Attribute("Type") == "Navigate")) == 1,
                "Web Component event drives one native Navigate action");
        }

        private static void TestIdentityNormalization()
        {
            Assert(ResolvedMasterDetailRules.NormalizeConditionDataType("AutoNumber") == "Number", "AutoNumber normalization");
            Assert(ResolvedMasterDetailRules.NormalizeConditionDataType("Autonumber") == "Number", "Autonumber normalization");
            Assert(ResolvedMasterDetailRules.NormalizeConditionDataType("AutoGuid") == "Guid", "AutoGuid normalization");
        }

        private static void TestViewOwnedMasterDetailRules()
        {
            var masterGuid = Guid.Parse("10000000-0000-0000-0000-000000000010");
            var detailGuid = Guid.Parse("20000000-0000-0000-0000-000000000010");
            var createDefinition = "30000000-0000-0000-0000-000000000010";
            var updateDefinition = "30000000-0000-0000-0000-000000000020";
            var detailCreateDefinition = "40000000-0000-0000-0000-000000000010";
            var detailUpdateDefinition = "40000000-0000-0000-0000-000000000020";
            var detailDeleteDefinition = "40000000-0000-0000-0000-000000000030";
            var contract = new MasterDetailFormDefinition
            {
                MasterView = "Claim",
                MasterKeyProperty = "ClaimId",
                MasterCreateMethod = "Create",
                MasterUpdateMethod = "Update",
                MasterReadMethod = "Read"
            };
            var childDefinition = new MasterDetailChildDefinition
            {
                View = "Claim Lines",
                ForeignKeyProperty = "ClaimId",
                CreateMethod = "Create",
                UpdateMethod = "Update",
                DeleteMethod = "Delete",
                ListMethod = "List"
            };
            contract.Details.Add(childDefinition);
            var formDefinition = new FormDefinition { Name = "Claim Form", MasterDetail = contract };
            var resolved = new ResolvedMasterDetailRules
            {
                Definition = contract,
                MasterViewGuid = masterGuid,
                MasterViewName = "Claim",
                MasterKey = new ResolvedViewField { Id = "claim-key", Name = "ClaimId", DisplayName = "Claim ID", DataType = "Number" },
                MasterCreateAction = TestViewAction(createDefinition, masterGuid, "Claim", "Create", null, true),
                MasterUpdateAction = TestViewAction(updateDefinition, masterGuid, "Claim", "Update", null, false),
                MasterCreateEvent = new ResolvedViewEvent { DefinitionId = createDefinition, DisplayName = "K2Skills.MasterDetail.Create.ClaimId" },
                MasterUpdateEvent = new ResolvedViewEvent { DefinitionId = updateDefinition, DisplayName = "K2Skills.MasterDetail.Update.ClaimId" },
                RequiredControls = new List<ResolvedRequiredControl>
                {
                    new ResolvedRequiredControl { Property = "Title", ControlId = "title-control", ControlName = "Title Text Box", ControlDisplayName = "Title", IsRequired = true },
                    new ResolvedRequiredControl { Property = "Accepted", ControlId = "accepted-control", ControlName = "Accepted Check Box", ControlDisplayName = "Accepted", IsRequired = true, MustBeTrue = true },
                    new ResolvedRequiredControl { Property = "Amount", ControlId = "amount-control", ControlName = "Amount Text Box", ControlDisplayName = "Amount", Minimum = 0 }
                },
                Details = new List<ResolvedMasterDetailChild>
                {
                    new ResolvedMasterDetailChild
                    {
                        Definition = childDefinition,
                        ViewGuid = detailGuid,
                        ViewName = "Claim Lines",
                        ViewDisplayName = "Claim Lines",
                        CreateAction = TestViewAction(detailCreateDefinition, detailGuid, "Claim Lines", "Create", "Added", false),
                        UpdateAction = TestViewAction(detailUpdateDefinition, detailGuid, "Claim Lines", "Update", "Changed", false),
                        DeleteAction = TestViewAction(detailDeleteDefinition, detailGuid, "Claim Lines", "Delete", "Removed", false),
                        SaveEvent = new ResolvedViewEvent { DefinitionId = detailCreateDefinition, DisplayName = "Save ToolBar Button" },
                        LoadEvent = new ResolvedViewEvent { DefinitionId = "40000000-0000-0000-0000-000000000040", DisplayName = "K2Skills.MasterDetail.Load.ClaimId" },
                        KeyParameterName = "ClaimId"
                    }
                }
            };
            var xml = "<Form ID='form-id'><Name>Claim Form</Name><Controls/>" +
                "<Areas><Area><Items><Item ID='master-instance' ViewID='" + masterGuid + "' ViewName='Claim'/></Items></Area>" +
                "<Area><Items><Item ID='detail-instance' ViewID='" + detailGuid + "' ViewName='Claim Lines'/></Items></Area></Areas>" +
                "<States><State><Events><Event><Handlers><Handler><Actions>" +
                "<Action ID='read-id' DefinitionID='50000000-0000-0000-0000-000000000010' Type='Execute' ExecutionType='Synchronous' InstanceID='master-instance'>" +
                "<Properties><Property><Name>Method</Name><Value>Read</Value></Property></Properties></Action>" +
                "</Actions></Handler></Handlers></Event></Events></State></States></Form>";
            var transformed = MasterDetailRules.Apply(xml, formDefinition, resolved);
            MasterDetailRules.Verify(transformed, formDefinition, resolved);
            var document = XDocument.Parse(transformed);
            var group = document.Descendants("ValidationGroup").Single();
            Assert((string)group.Element("Name") == "ValidationGroupForEvent", "native validation-group name");
            var saveEvent = document.Descendants("Event").Single(x => (string)x.Attribute("SourceName") == "btnSave");
            var saveButtonId = (string)saveEvent.Attribute("SourceID");
            Assert(ReadButtonCellAlignment(document, saveButtonId) == FormActionAlignment.Right,
                "master-detail Save uses native right-aligned Table-cell styling");
            Assert(document.Descendants("Event").Count(x =>
                (string)x.Attribute("Type") == "System" &&
                (string)x.Attribute("SourceType") == "Control" &&
                (string)x.Attribute("SourceID") == saveButtonId &&
                (string)x.Element("Name") == "OnClick") == 1,
                "Form Save button has the canonical K2 system OnClick declaration");
            Assert(saveEvent.Descendants("Condition").All(x =>
                ReadActionProperty(x, "Name") == "AdvancedCondition" &&
                ReadActionProperty(x, "Location") == "Form" &&
                x.Attribute("InstanceID") == null),
                "Form Save branches use canonical local AdvancedCondition shapes");
            Assert(!saveEvent.Descendants("Action").Any(x => !string.IsNullOrWhiteSpace(ReadMethod(x))),
                "Form Save rule contains no embedded View method actions");
            Assert(saveEvent.Descendants("Action").Count(x => ReadActionProperty(x, "EventID") == createDefinition) == 1,
                "master Create is a View-event call");
            var saveHandlers = saveEvent.Element("Handlers").Elements("Handler").ToList();
            Assert(saveHandlers.Count == 2 &&
                ReadActionProperty(saveHandlers[0], "HandlerName") == "IfLogicalHandler" &&
                (string)saveHandlers[1].Attribute("Type") == "Else" &&
                ReadActionProperty(saveHandlers[1], "HandlerName") == "ElseLogicalHandler" &&
                saveHandlers[1].Element("Conditions") == null,
                "blank-key Create and nonblank-key Update are one native mutually exclusive If/Else pair");
            Assert(saveHandlers[0].Descendants("Action").Count(x =>
                    ReadActionProperty(x, "EventID") == createDefinition) == 1 &&
                !saveHandlers[0].Descendants("Action").Any(x =>
                    ReadActionProperty(x, "EventID") == updateDefinition),
                "blank-key Save invokes Create exactly once and Update zero times");
            Assert(saveHandlers[1].Descendants("Action").Count(x =>
                    ReadActionProperty(x, "EventID") == updateDefinition) == 1 &&
                !saveHandlers[1].Descendants("Action").Any(x =>
                    ReadActionProperty(x, "EventID") == createDefinition),
                "nonblank-key Save invokes Update exactly once and Create zero times");
            Assert(saveHandlers[0].Descendants("Action").Last().Attribute("Type").Value == "ShowMessage" &&
                (string)saveHandlers[1].Descendants("Action").Last().Attribute("Type") == "ShowMessage",
                "dismissing either terminal success popup cannot enter the other branch");
            foreach (var seamCall in saveEvent.Descendants("Action").Where(x =>
                ReadActionProperty(x, "EventID") == createDefinition ||
                ReadActionProperty(x, "EventID") == updateDefinition))
            {
                var previous = seamCall.ElementsBeforeSelf("Action").LastOrDefault();
                Assert(previous != null && (string)previous.Attribute("Type") == "Validate" &&
                    ReadActionProperty(previous, "GroupID") == (string)group.Attribute("ID"),
                    "Form validation runs immediately before each master persistence seam");
            }
            Assert(group.Descendants("ValidationGroupControl").Any(x =>
                (string)x.Attribute("ControlID") == "accepted-control" &&
                x.Element("Conditions") != null &&
                x.Descendants("Equals").Any(e => e.Elements("Item").All(i =>
                    (string)i.Attribute("DataType") == "Boolean"))),
                "Form validation retains must-be-true condition");
            Assert(group.Descendants("ValidationGroupControl").Any(x =>
                (string)x.Attribute("ControlID") == "amount-control" &&
                x.Element("Conditions") != null &&
                x.Descendants("GreaterThanEquals").Any() &&
                x.Descendants("Item").Where(i => (string)i.Attribute("SourceID") == "amount-control")
                    .All(i => (string)i.Attribute("DataType") == "Number")),
                "Form validation retains inclusive numeric minimum condition");
            Assert(saveEvent.Descendants("Action").Count(x => ReadActionProperty(x, "EventID") == detailCreateDefinition) == 2,
                "detail Save View event is called once in each branch");
            Assert(saveEvent.Descendants("Parameter").Count(x => (string)x.Attribute("TargetType") == "ViewParameter" &&
                (string)x.Attribute("TargetInstanceID") == "detail-instance" &&
                (string)x.Attribute("TargetID") == "ClaimId") == 2, "master key is transferred to the detail View parameter");

            var missingSystemEvent = XDocument.Parse(transformed);
            missingSystemEvent.Descendants("Event").Single(x =>
                (string)x.Attribute("Type") == "System" &&
                (string)x.Attribute("SourceID") == saveButtonId &&
                (string)x.Element("Name") == "OnClick").Remove();
            AssertThrows(delegate { MasterDetailRules.Verify(missingSystemEvent.ToString(), formDefinition, resolved); },
                "must contain exactly one K2 system OnClick declaration");

            var malformedSystemEvent = XDocument.Parse(transformed);
            malformedSystemEvent.Descendants("Event").Single(x =>
                (string)x.Attribute("Type") == "System" &&
                (string)x.Attribute("SourceID") == saveButtonId &&
                (string)x.Element("Name") == "OnClick")
                .Descendants("Action").Single().SetAttributeValue("Type", "Transfer");
            AssertThrows(delegate { MasterDetailRules.Verify(malformedSystemEvent.ToString(), formDefinition, resolved); },
                "has a malformed K2 system OnClick declaration");

            var unsupportedCondition = XDocument.Parse(transformed);
            var unsupportedName = unsupportedCondition.Descendants("Event").Single(x =>
                (string)x.Attribute("SourceName") == "btnSave").Descendants("Condition").First()
                .Descendants("Property").Single(x => (string)x.Element("Name") == "Name");
            unsupportedName.Element("Value").Value = "SimpleBlankViewFieldCondition";
            AssertThrows(delegate { MasterDetailRules.Verify(unsupportedCondition.ToString(), formDefinition, resolved); },
                "not a canonical mutually exclusive native If/Else pair");

            var siblingConditions = XDocument.Parse(transformed);
            var siblingSave = siblingConditions.Descendants("Event").Single(x =>
                (string)x.Attribute("SourceName") == "btnSave");
            var siblingHandlers = siblingSave.Element("Handlers").Elements("Handler").ToList();
            siblingHandlers[1].Attribute("Type").Remove();
            siblingHandlers[1].Descendants("Property").First(x =>
                (string)x.Element("Name") == "HandlerName").Element("Value").Value = "IfLogicalHandler";
            var notBlank = new XElement(siblingHandlers[0].Element("Conditions"));
            notBlank.Descendants("IsBlank").Single().Name = "IsNotBlank";
            siblingHandlers[1].AddFirst(notBlank);
            AssertThrows(delegate { MasterDetailRules.Verify(siblingConditions.ToString(), formDefinition, resolved); },
                "not a canonical mutually exclusive native If/Else pair");

            var corrupt = saveEvent.Descendants("Action").First(x => ReadActionProperty(x, "EventID") == detailCreateDefinition);
            corrupt.Element("Properties").Add(XElement.Parse("<Property><Name>Method</Name><Value>Create</Value></Property>"));
            AssertThrows(delegate { MasterDetailRules.Verify(document.ToString(), formDefinition, resolved); },
                "embeds a View method action");

            var detailView = "<View ID='" + detailGuid + "'><Name>Claim Lines</Name>" +
                "<Fields><Field ID='claim-field' DataType='Number'><Name>ClaimId</Name><FieldName>ClaimId</FieldName></Field></Fields><Events>" +
                "<Event ID='save-event' DefinitionID='41000000-0000-0000-0000-000000000010'><Handlers><Handler><Actions>" +
                TestViewAction(detailCreateDefinition, detailGuid, "Claim Lines", "Create", "Added", false) +
                TestViewAction(detailUpdateDefinition, detailGuid, "Claim Lines", "Update", "Changed", false) +
                TestViewAction(detailDeleteDefinition, detailGuid, "Claim Lines", "Delete", "Removed", false) +
                "</Actions></Handler></Handlers></Event><Event ID='list-event' DefinitionID='41000000-0000-0000-0000-000000000020'><Handlers><Handler><Actions>" +
                TestViewAction("42000000-0000-0000-0000-000000000010", detailGuid, "Claim Lines", "List", null, false) +
                "</Actions></Handler></Handlers></Event></Events></View>";
            var configured = MasterDetailRules.ConfigureViewRuleSeams(detailView, "Claim Lines",
                new MasterDetailFormDefinition[0], new[] { childDefinition }, new MasterDetailReviewDefinition[0]);
            MasterDetailRules.VerifyDetailViewLoads(configured, "Claim Lines", new[] { childDefinition });
            var configuredDocument = XDocument.Parse(configured);
            Assert(configuredDocument.Descendants("Parameter").Any(x => (string)x.Attribute("DataType") == "Number" &&
                (string)x.Element("Name") == "ClaimId"), "detail key View parameter emitted");
            Assert(configuredDocument.Descendants("Action").Count(x => ReadMethod(x) == "List") == 1,
                "only the View-owned filtered List action remains");

            var masterView = "<View ID='" + masterGuid + "'><Name>Claim</Name>" +
                "<Fields><Field ID='claim-key' DataType='Number'><Name>ClaimId</Name><FieldName>ClaimId</FieldName></Field>" +
                "<Field ID='language-field' DataType='Text'><Name>PreferredLanguageCode</Name><FieldName>PreferredLanguageCode</FieldName></Field>" +
                "<Field ID='nda-field' DataType='Text'><Name>NDAContentVersion</Name><FieldName>NDAContentVersion</FieldName></Field>" +
                "<Field ID='channel-field' DataType='Text'><Name>SubmissionChannelCode</Name><FieldName>SubmissionChannelCode</FieldName></Field></Fields>" +
                "<Controls><Control ID='claim-control' FieldID='claim-key' Type='TextBox'/>" +
                "<Control ID='language-control' FieldID='language-field' Type='TextBox'/>" +
                "<Control ID='nda-control' FieldID='nda-field' Type='TextBox'/>" +
                "<Control ID='channel-control' FieldID='channel-field' Type='TextBox'/></Controls><Events>" +
                "<Event ID='create-event' DefinitionID='43000000-0000-0000-0000-000000000010' SourceType='Control'><Handlers><Handler><Actions>" +
                TestViewAction(createDefinition, masterGuid, "Claim", "Create", null, true) +
                "</Actions></Handler></Handlers></Event>" +
                "<Event ID='update-event' DefinitionID='43000000-0000-0000-0000-000000000020' SourceType='Control'><Handlers><Handler><Actions>" +
                TestViewAction(updateDefinition, masterGuid, "Claim", "Update", null, false) +
                "</Actions></Handler></Handlers></Event>" +
                "<Event ID='read-event' DefinitionID='43000000-0000-0000-0000-000000000030' SourceType='Control'><Handlers><Handler><Actions>" +
                TestViewAction("30000000-0000-0000-0000-000000000030", masterGuid, "Claim", "Read", null, false) +
                "</Actions></Handler></Handlers></Event></Events></View>";
            var masterViewDocument = XDocument.Parse(masterView);
            var defaults = new[]
            {
                new[] { "PreferredLanguageCode", "language-field", "language-control", "en" },
                new[] { "NDAContentVersion", "nda-field", "nda-control", "FTA-WB-2022-04" },
                new[] { "SubmissionChannelCode", "channel-field", "channel-control", "PUBLIC_WEB" }
            };
            var sourceCreate = masterViewDocument.Descendants("Action").Single(x => ReadMethod(x) == "Create");
            var sourceUpdate = masterViewDocument.Descendants("Action").Single(x => ReadMethod(x) == "Update");
            var sourceRead = masterViewDocument.Descendants("Action").Single(x => ReadMethod(x) == "Read");
            foreach (var value in defaults)
            {
                sourceCreate.Element("Parameters").Add(new XElement("Parameter",
                    new XAttribute("SourceID", "Sources"), new XAttribute("SourceType", "Value"),
                    new XAttribute("TargetID", value[0]), new XAttribute("TargetType", "ObjectProperty"),
                    new XElement("SourceValue", value[3])));
                sourceUpdate.Element("Parameters").Add(new XElement("Parameter",
                    new XAttribute("SourceID", value[1]), new XAttribute("SourceType", "ViewField"),
                    new XAttribute("TargetID", value[0]), new XAttribute("TargetType", "ObjectProperty")));
                var results = sourceRead.Element("Results");
                if (results == null) { results = new XElement("Results"); sourceRead.Add(results); }
                results.Add(new XElement("Result",
                    new XAttribute("SourceID", value[0]), new XAttribute("SourceType", "ObjectProperty"),
                    new XAttribute("TargetID", value[2]), new XAttribute("TargetType", "Control")));
            }
            var configuredMaster = MasterDetailRules.ConfigureViewRuleSeams(
                masterViewDocument.ToString(SaveOptions.DisableFormatting), "Claim",
                new[] { contract }, new MasterDetailChildDefinition[0], new MasterDetailReviewDefinition[0]);
            MasterDetailRules.VerifyMasterViewRules(configuredMaster, "Claim", new[] { contract });
            var configuredMasterDocument = XDocument.Parse(configuredMaster);
            var masterRuleNames = configuredMasterDocument.Descendants("Event")
                .Where(x => (string)x.Attribute("SourceType") == "Rule")
                .Select(x => ReadActionProperty(x, "RuleName")).ToList();
            Assert(masterRuleNames.Contains("K2Skills.MasterDetail.Create.ClaimId"),
                "master Create custom rule emitted");
            Assert(masterRuleNames.Contains("K2Skills.MasterDetail.Update.ClaimId"),
                "master Update custom rule emitted");
            Assert(configuredMasterDocument.Descendants("Event")
                .Where(x => (string)x.Attribute("SourceType") == "Rule")
                .SelectMany(x => x.Descendants("Action"))
                .All(x => string.IsNullOrWhiteSpace((string)x.Attribute("IsReference")) &&
                    string.IsNullOrWhiteSpace((string)x.Attribute("IsInherited"))),
                "master persistence wrappers contain no inheritance metadata");
            Assert(configuredMasterDocument.Descendants("Event")
                .Where(x => (string)x.Attribute("SourceType") == "Rule")
                .SelectMany(x => x.Elements("Handlers").Elements("Handler"))
                .All(x => ReadActionProperty(x, "Location") == "view"),
                "custom View rule handlers use the canonical view context");
            var createSeam = configuredMasterDocument.Descendants("Event").Single(x =>
                ReadActionProperty(x, "RuleName") == "K2Skills.MasterDetail.Create.ClaimId");
            Assert(createSeam.Descendants("Action").Select(ReadMethod)
                .SequenceEqual(new[] { "Create", "Read" }),
                "master Create seam synchronously re-reads the persisted master");
            foreach (var value in defaults)
                Assert(createSeam.Descendants("Action").Single(x => ReadMethod(x) == "Read")
                    .Descendants("Result").Any(x =>
                        (string)x.Attribute("SourceID") == value[0] &&
                        (string)x.Attribute("TargetID") == value[2]),
                    "post-Create Read hydrates " + value[0] + " for a later intentional Update");
            var missingHydration = XDocument.Parse(configuredMaster);
            missingHydration.Descendants("Event").Single(x =>
                ReadActionProperty(x, "RuleName") == "K2Skills.MasterDetail.Create.ClaimId")
                .Descendants("Action").Single(x => ReadMethod(x) == "Read")
                .Descendants("Result").Single(x =>
                    (string)x.Attribute("SourceID") == "PreferredLanguageCode").Remove();
            AssertThrows(delegate
            {
                MasterDetailRules.VerifyMasterViewRules(missingHydration.ToString(), "Claim", new[] { contract });
            }, "does not hydrate Update View field 'PreferredLanguageCode'");
            var invalidMaster = XDocument.Parse(configuredMaster);
            invalidMaster.Descendants("Event").First(x =>
                ReadActionProperty(x, "RuleName") == "K2Skills.MasterDetail.Create.ClaimId")
                .SetAttributeValue("SourceType", "Control");
            AssertThrows(delegate
            {
                MasterDetailRules.VerifyMasterViewRules(invalidMaster.ToString(), "Claim", new[] { contract });
            }, "must contain exactly one View-owned persistence rule");
            invalidMaster = XDocument.Parse(configuredMaster);
            var invalidHandler = invalidMaster.Descendants("Event").First(x =>
                ReadActionProperty(x, "RuleName") == "K2Skills.MasterDetail.Create.ClaimId")
                .Descendants("Handler").First();
            invalidHandler.Descendants("Property").First(x =>
                (string)x.Element("Name") == "Location").Element("Value").Value = "Claim";
            AssertThrows(delegate
            {
                MasterDetailRules.VerifyMasterViewRules(invalidMaster.ToString(), "Claim", new[] { contract });
            }, "canonical Handler Location 'view'");
        }

        private static void TestMasterDetailValidationComposition()
        {
            var masterGuid = Guid.Parse("11000000-0000-0000-0000-000000000010");
            var detailGuid = Guid.Parse("21000000-0000-0000-0000-000000000010");
            var contract = new MasterDetailFormDefinition
            {
                MasterView = "Submission",
                MasterKeyProperty = "ClaimId",
                MasterCreateMethod = "Create",
                MasterUpdateMethod = "Update",
                MasterReadMethod = "Read"
            };
            var child = new MasterDetailChildDefinition
            {
                View = "Evidence",
                ForeignKeyProperty = "ClaimId",
                CreateMethod = "Create",
                UpdateMethod = "Update",
                DeleteMethod = "Delete",
                ListMethod = "List"
            };
            contract.Details.Add(child);

            var masterDefinition = NewView("Submission", "Submission", "capture",
                "ClaimId", "EmailAddress", "Narrative", "Amount", "Accepted");
            masterDefinition.Methods.AddRange(new[] { "Create", "Read", "Update" });
            masterDefinition.RequiredProperties.AddRange(new[] { "EmailAddress", "Narrative", "Accepted" });
            masterDefinition.Validations.Add(new FieldValidationDefinition
            {
                Property = "EmailAddress", Required = true, MaxLength = 320, Format = "email",
                Message = "Enter a valid email address.",
                ValidationPatternGuid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
                ValidationPatternName = "K2Skills.Submission.EmailAddress"
            });
            masterDefinition.Validations.Add(new FieldValidationDefinition
            {
                Property = "Narrative", Required = true, MinLength = 100, MaxLength = 2000,
                Message = "Enter at least 100 characters.",
                ValidationPatternGuid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
                ValidationPatternName = "K2Skills.Submission.Narrative"
            });
            masterDefinition.Validations.Add(new FieldValidationDefinition
            {
                Property = "Amount", Minimum = 0, Message = "Amount cannot be negative."
            });
            masterDefinition.Validations.Add(new FieldValidationDefinition
            {
                Property = "Accepted", Required = true, MustBeTrue = true,
                Message = "Accept before continuing."
            });

            var masterDocument = new XDocument(new XElement("View", new XAttribute("ID", masterGuid),
                new XElement("Name", "Submission"),
                new XElement("Fields",
                    TestField("claim-key", "ClaimId", "Number"),
                    TestField("email-field", "EmailAddress", "Text"),
                    TestField("narrative-field", "Narrative", "Memo"),
                    TestField("amount-field", "Amount", "Decimal"),
                    TestField("accepted-field", "Accepted", "YesNo")),
                new XElement("Controls",
                    FieldControlDefinition("claim-control", "TextBox", "claim-key"),
                    FieldControlDefinition("email-control", "TextBox", "email-field"),
                    FieldControlDefinition("narrative-control", "TextArea", "narrative-field"),
                    FieldControlDefinition("amount-control", "TextBox", "amount-field"),
                    FieldControlDefinition("accepted-control", "CheckBox", "accepted-field")),
                new XElement("Events",
                    TestMethodEvent("master-create-event",
                        TestViewAction("31000000-0000-0000-0000-000000000010", masterGuid,
                            "Submission", "Create", null, true)),
                    TestMethodEvent("master-update-event",
                        TestViewAction("31000000-0000-0000-0000-000000000020", masterGuid,
                            "Submission", "Update", null, false)),
                    TestMethodEvent("master-read-event",
                        TestViewAction("31000000-0000-0000-0000-000000000030", masterGuid,
                            "Submission", "Read", null, false)))));
            FieldValidationDefinitionXml.Apply(masterDocument, masterDefinition);
            var configuredMaster = MasterDetailRules.ConfigureViewRuleSeams(
                masterDocument.ToString(SaveOptions.DisableFormatting), "Submission",
                new[] { contract }, new MasterDetailChildDefinition[0], new MasterDetailReviewDefinition[0]);
            var configuredMasterDocument = XDocument.Parse(configuredMaster);
            FieldValidationDefinitionXml.Verify(configuredMasterDocument, masterDefinition);
            MasterDetailRules.VerifyMasterViewRules(configuredMaster, "Submission", new[] { contract });
            Assert(configuredMasterDocument.Descendants("Action").Count(
                MasterDetailRules.IsMasterPersistenceSeamAction) == 3,
                "master Create, post-Create Read, and Update seam actions are recognized as Form-validated internal paths");
            Assert(configuredMasterDocument.Descendants("Control").Single(x =>
                (string)x.Attribute("ID") == "email-control").Descendants("Property").Any(x =>
                    (string)x.Element("Name") == "MaxLength" && (string)x.Element("Value") == "320"),
                "master seam composition preserves native MaxLength");
            var masterGroup = configuredMasterDocument.Descendants("ValidationGroup").Single(x =>
                (string)x.Element("Name") == FieldValidationDefinitionXml.GroupName);
            Assert(masterGroup.Descendants("ValidationGroupControl").Any(x =>
                (string)x.Attribute("ControlID") == "accepted-control" &&
                x.Element("Conditions") != null &&
                x.Descendants("Equals").Any(e => e.Elements("Item").All(i =>
                    (string)i.Attribute("DataType") == "Boolean"))),
                "master seam composition preserves must-be-true condition");
            Assert(masterGroup.Descendants("ValidationGroupControl").Any(x =>
                (string)x.Attribute("ControlID") == "amount-control" &&
                x.Element("Conditions") != null &&
                x.Descendants("GreaterThanEquals").Any()),
                "master seam composition preserves numeric minimum condition");

            var detailDefinition = NewView("Evidence", "Evidence", "capture-list",
                "EvidenceId", "ClaimId", "Title", "FileContent");
            detailDefinition.Methods.AddRange(new[] { "Create", "Update", "Delete", "List" });
            detailDefinition.RequiredProperties.AddRange(new[] { "Title", "FileContent" });
            detailDefinition.Validations.Add(new FieldValidationDefinition
                { Property = "Title", Required = true, MaxLength = 300 });
            detailDefinition.Validations.Add(new FieldValidationDefinition
                { Property = "FileContent", Required = true });
            var detailDocument = new XDocument(new XElement("View", new XAttribute("ID", detailGuid),
                new XElement("Name", "Evidence"),
                new XElement("Fields",
                    TestField("evidence-field", "EvidenceId", "Number"),
                    TestField("detail-submission-field", "ClaimId", "Number"),
                    TestField("title-field", "Title", "Text"),
                    TestField("file-field", "FileContent", "File")),
                new XElement("Controls",
                    FieldControlDefinition("evidence-control", "TextBox", "evidence-field"),
                    FieldControlDefinition("detail-submission-control", "TextBox", "detail-submission-field"),
                    FieldControlDefinition("title-control", "TextBox", "title-field"),
                    FieldControlDefinition("file-control", "File", "file-field")),
                new XElement("Events",
                    TestMethodEvent("detail-save-event",
                        TestViewAction("41000000-0000-0000-0000-000000000010", detailGuid,
                            "Evidence", "Create", "Added", false),
                        TestViewAction("41000000-0000-0000-0000-000000000020", detailGuid,
                            "Evidence", "Update", "Changed", false),
                        TestViewAction("41000000-0000-0000-0000-000000000030", detailGuid,
                            "Evidence", "Delete", "Removed", false)),
                    TestMethodEvent("detail-list-event",
                        TestViewAction("41000000-0000-0000-0000-000000000040", detailGuid,
                            "Evidence", "List", null, false)))));
            FieldValidationDefinitionXml.Apply(detailDocument, detailDefinition);
            var configuredDetail = MasterDetailRules.ConfigureViewRuleSeams(
                detailDocument.ToString(SaveOptions.DisableFormatting), "Evidence",
                new MasterDetailFormDefinition[0], new[] { child }, new MasterDetailReviewDefinition[0]);
            var configuredDetailDocument = XDocument.Parse(configuredDetail);
            FieldValidationDefinitionXml.Verify(configuredDetailDocument, detailDefinition);
            MasterDetailRules.VerifyDetailViewLoads(configuredDetail, "Evidence", new[] { child });
            var detailGroup = configuredDetailDocument.Descendants("ValidationGroup").Single(x =>
                (string)x.Element("Name") == FieldValidationDefinitionXml.GroupName);
            Assert(detailGroup.Descendants("ValidationGroupControl").Any(x =>
                (string)x.Attribute("ControlID") == "file-control" &&
                (string)x.Attribute("IsRequired") == "True"),
                "capture-list detail keeps required native File validation");
        }

        private static XElement TestField(string id, string name, string dataType)
        {
            return new XElement("Field", new XAttribute("ID", id), new XAttribute("DataType", dataType),
                new XElement("Name", name), new XElement("FieldName", name),
                new XElement("FieldDisplayName", name));
        }

        private static XElement TestMethodEvent(string id, params string[] actions)
        {
            return new XElement("Event", new XAttribute("ID", id),
                new XElement("Handlers", new XElement("Handler",
                    new XElement("Actions", actions.Select(XElement.Parse)))));
        }

        private static string ReadActionProperty(XElement action, string name)
        {
            var property = action.Descendants("Property").FirstOrDefault(x => (string)x.Element("Name") == name);
            return property == null ? null : (string)property.Element("Value");
        }

        private static string TestViewAction(string definitionId, Guid viewId, string viewName, string method, string itemState, bool includeKeyResult)
        {
            var state = itemState == null ? string.Empty : " ItemState='" + itemState + "'";
            var results = includeKeyResult
                ? "<Results><Result SourceID='ClaimId' SourceType='ObjectProperty' TargetID='claim-key' TargetType='ViewField'/></Results>"
                : string.Equals(method, "Read", StringComparison.OrdinalIgnoreCase)
                    ? "<Results><Result SourceID='ClaimId' SourceType='ObjectProperty' TargetID='claim-control' TargetType='Control'/></Results>"
                    : string.Empty;
            return "<Action ID='prototype-id' DefinitionID='" + definitionId + "' Type='Execute' ExecutionType='Synchronous'" + state + ">" +
                "<Properties><Property><Name>Location</Name><Value>View</Value></Property>" +
                "<Property><Name>Method</Name><Value>" + method + "</Value></Property>" +
                "<Property><Name>ViewID</Name><DisplayValue>" + viewName + "</DisplayValue><Value>" + viewId + "</Value></Property></Properties>" +
                "<Parameters><Parameter SourceID='claim-key' SourceName='ClaimId' SourceType='ViewField' TargetID='ClaimId' TargetType='ObjectProperty'/>" +
                "<Parameter SourceID='title-control' SourceName='Title' SourceType='Control' TargetID='Title' TargetType='ObjectProperty'/></Parameters>" +
                results + "</Action>";
        }

        private static void TestMissingOptionalControlMappings()
        {
            var view = new ViewDefinition { Name = "Claim" };
            view.Properties.Add("CaseId");
            view.HiddenProperties.Add("CaseId");
            var hidden = XDocument.Parse(
                "<View><Fields><Field ID='case-field'><Name>CaseId</Name><FieldName>CaseId</FieldName><FieldDisplayName>Case ID</FieldDisplayName></Field></Fields>" +
                "<Controls><Control ID='case-control' Type='DropDownList' FieldID='case-field'/></Controls><Events><Event><Handlers><Handler><Actions><Action><Parameters>" +
                "<Parameter SourceType='Control' SourceID='case-control' SourceName='CaseId Drop-Down List' TargetID='CaseId'/>" +
                "</Parameters></Action></Actions></Handler></Handlers></Event></Events></View>");
            ViewPresentationDefinition.RewriteHiddenControlMappings(hidden, view);
            var rewritten = hidden.Descendants("Parameter").Single();
            Assert((string)rewritten.Attribute("SourceType") == "ViewField", "hidden control mapping rewritten to ViewField");
            Assert((string)rewritten.Attribute("SourceID") == "case-field", "hidden control mapping uses field identity");

            var document = XDocument.Parse(
                "<View><Controls><Control ID='present' Type='TextBox'/></Controls><Events><Event><Handlers><Handler><Actions><Action><Parameters>" +
                "<Parameter SourceType='Control' SourceID='present' SourceName='Present' TargetID='Present'/>" +
                "<Parameter SourceType='Control' SourceID='missing' SourceName='Missing optional' TargetID='Optional'/>" +
                "</Parameters></Action></Actions></Handler></Handlers></Event></Events></View>");
            ViewPresentationDefinition.PruneMissingOptionalControlMappings(document, view);
            Assert(document.Descendants("Parameter").Count() == 1, "orphan optional control mapping pruned");
            Assert((string)document.Descendants("Parameter").Single().Attribute("SourceID") == "present", "valid control mapping retained");

            var required = XDocument.Parse(
                "<View><Controls/><Events><Event><Handlers><Handler><Actions><Action><Parameters>" +
                "<Parameter SourceType='Control' SourceID='missing' SourceName='Missing required' TargetName='Required' IsRequired='True'/>" +
                "</Parameters></Action></Actions></Handler></Handlers></Event></Events></View>");
            AssertThrows(delegate { ViewPresentationDefinition.PruneMissingOptionalControlMappings(required, view); },
                "references a removed control");
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
                ValuePropertyType = "Text",
                DisplayPropertyName = "CategoryName",
                DisplayPropertyDisplayName = "Category Name",
                DisplayPropertyType = "Text"
            };
            var sources = new Dictionary<string, LookupRuntimeSource>(StringComparer.OrdinalIgnoreCase) { { "Category", source } };
            var xml = ViewLookupDefinition.Apply(ViewXml(), view, sources);
            ViewLookupDefinition.Verify(xml, view, sources);

            var document = XDocument.Parse(xml);
            var lookup = document.Descendants("Control").Single(x => (string)x.Attribute("ID") == "categoryControl" && x.Attribute("Type") != null);
            Assert(lookup.Descendants("Property").Any(x => (string)x.Element("Name") == "OriginalProperty" &&
                (string)x.Element("Value") == "CategoryCode"), "lookup original property preserved");
            var population = document.Descendants("Event").Single(x => (string)x.Element("Name") == "Init")
                .Descendants("Action").Single(x => (string)x.Attribute("Type") == "Execute" &&
                    x.Descendants("Property").Any(p => (string)p.Element("Name") == "ControlID" &&
                        (string)p.Element("Value") == "categoryControl"));
            Assert(population.Descendants("Property").Any(x => (string)x.Element("Name") == "ObjectID" &&
                (string)x.Element("Value") == source.SmartObjectGuid.ToString()), "lookup population source rewritten");
            Assert(population.Descendants("Result").Any(x => (string)x.Attribute("SourceID") == source.SmartObjectGuid.ToString() &&
                (string)x.Attribute("TargetID") == "categoryControl"), "lookup List result populates dropdown");
            var lookupSource = document.Descendants("Source").Single(x =>
                (string)x.Attribute("ContextType") == "Association" &&
                (string)x.Attribute("ContextID") == "categoryControl");
            Assert((string)lookupSource.Attribute("SourceID") == source.SmartObjectGuid.ToString(),
                "lookup SmartObject registered as the control association source");
            Assert(lookupSource.Descendants("Field").Count(x =>
                (string)x.Element("FieldName") == "CategoryCode" &&
                (string)x.Attribute("DataType") == "Text") == 1, "lookup value field registered");
            Assert(lookupSource.Descendants("Field").Count(x =>
                (string)x.Element("FieldName") == "CategoryName" &&
                (string)x.Attribute("DataType") == "Text") == 1, "lookup display field registered");
            var secondPass = ViewLookupDefinition.Apply(xml, view, sources);
            Assert(string.Equals(xml, secondPass, StringComparison.Ordinal), "lookup population transformation is idempotent");

            var child = new MasterDetailChildDefinition { View = view.Name, ListMethod = "List" };
            var suppressed = MasterDetailRules.SuppressUnfilteredDetailLoads(xml, view.Name, new[] { child });
            ViewLookupDefinition.Verify(suppressed, view, sources);
            var suppressedDocument = XDocument.Parse(suppressed);
            Assert(suppressedDocument.Descendants("Action").Count(x => (string)x.Attribute("Type") == "Execute" &&
                x.Descendants("Property").Any(p => (string)p.Element("Name") == "Method" && (string)p.Element("Value") == "List") &&
                x.Descendants("Property").Any(p => (string)p.Element("Name") == "ControlID" && (string)p.Element("Value") == "categoryControl")) == 1,
                "master-detail suppression preserves dropdown population");
            Assert(!suppressedDocument.Descendants("Action").Any(x => (string)x.Attribute("ID") == "unfiltered-detail-list"),
                "master-detail suppression removes only the unfiltered detail load");

            population.Remove();
            AssertThrows(delegate { ViewLookupDefinition.Verify(document.ToString(), view, sources); }, "population actions");
            document = XDocument.Parse(xml);
            document.Descendants("Source").Single(x =>
                (string)x.Attribute("ContextType") == "Association" &&
                (string)x.Attribute("ContextID") == "categoryControl").Remove();
            AssertThrows(delegate { ViewLookupDefinition.Verify(document.ToString(), view, sources); }, "association sources");
            document = XDocument.Parse(xml);
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
                    new ResolvedMasterDetailChild { Definition = first, ViewGuid = firstGuid, ViewName = "Lines", ViewDisplayName = "Lines",
                        LoadEvent = new ResolvedViewEvent { DefinitionId = "60000000-0000-0000-0000-000000000010", DisplayName = "Load Lines" },
                        KeyParameterName = "RequestId" },
                    new ResolvedMasterDetailChild { Definition = second, ViewGuid = secondGuid, ViewName = "Attachments", ViewDisplayName = "Attachments",
                        LoadEvent = new ResolvedViewEvent { DefinitionId = "60000000-0000-0000-0000-000000000020", DisplayName = "Load Attachments" },
                        KeyParameterName = "RequestId" }
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
            Assert(document.Descendants("Action").Count(x => (string)x.Attribute("Type") == "Execute" &&
                (ReadActionProperty(x, "EventID") == "60000000-0000-0000-0000-000000000010" ||
                 ReadActionProperty(x, "EventID") == "60000000-0000-0000-0000-000000000020")) == 2,
                "two View-owned filtered detail load events on the base-state master Read path");
            Assert(document.Descendants("Action").Count(x => (string)x.Attribute("Type") == "Execute" &&
                ReadMethod(x) == "List" &&
                x.Descendants("Property").Any(p => (string)p.Element("Name") == "ControlID" && (string)p.Element("Value") == "line-type-lookup")) == 2,
                "inherited lookup population actions preserved and excluded from detail data loads");
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

        private static void TestGuidedJourneyComposition()
        {
            var form = new FormDefinition { Name = "New Case" };
            form.Tabs.Add(new FormTabDefinition { Name = "What happened", Views = new List<string> { "Case Entry" } });
            form.Tabs.Add(new FormTabDefinition { Name = "Evidence", Views = new List<string> { "Evidence" } });
            form.Tabs.Add(new FormTabDefinition { Name = "Review", Views = new List<string> { "Case Review" } });
            form.WorkflowStartButton = new WorkflowStartButtonDefinition { Name = "btnSubmitCase", Text = "Submit case", Tab = "Review" };
            form.MasterDetail = new MasterDetailFormDefinition
            {
                MasterView = "Case Entry",
                MasterKeyProperty = "CaseId",
                Review = new MasterDetailReviewDefinition { View = "Case Review", KeyProperty = "CaseId", Tab = "Review" }
            };
            form.MasterDetail.Details.Add(new MasterDetailChildDefinition { View = "Evidence", ForeignKeyProperty = "CaseId" });
            form.GuidedJourney = new GuidedJourneyDefinition
            {
                Title = "Report a concern",
                Description = "Tell us what happened, attach evidence, and review before submitting."
            };
            form.GuidedJourney.Steps.Add(new GuidedJourneyStepDefinition
            {
                Code = "INCIDENT", Label = "Describe", Title = "What happened?",
                Description = "Describe the concern in your own words.",
                Tab = "What happened", Advance = "continue"
            });
            form.GuidedJourney.Steps.Add(new GuidedJourneyStepDefinition
            {
                Code = "EVIDENCE", Label = "Evidence", Description = "Add any files or supporting details.",
                Tab = "Evidence", Advance = "save"
            });
            form.GuidedJourney.Steps.Add(new GuidedJourneyStepDefinition
            {
                Code = "REVIEW", Label = "Review", Description = "Check the report before submitting it.",
                Tab = "Review", Advance = "submit"
            });

            var xml = "<Forms><Form ID='form-id'><Name>New Case</Name><Controls>" +
                "<Control ID='entry-area' Type='Area'/><Control ID='entry-item' Type='AreaItem'/>" +
                "<Control ID='evidence-area' Type='Area'/><Control ID='evidence-item' Type='AreaItem'/>" +
                "<Control ID='review-area' Type='Area'/><Control ID='review-item' Type='AreaItem'/>" +
                "<Control ID='save-area' Type='Area'/><Control ID='save-item' Type='AreaItem'/>" +
                "<Control ID='submit-area' Type='Area'/><Control ID='submit-item' Type='AreaItem'/>" +
                "<Control ID='save' Type='Button'><Name>btnSave</Name><Properties><Property><Name>Text</Name><Value>Save draft and review</Value></Property></Properties></Control>" +
                "<Control ID='submit' Type='Button'><Name>btnSubmitCase</Name><Properties><Property><Name>Text</Name><Value>Submit case</Value></Property></Properties></Control>" +
                "</Controls><ValidationGroups><ValidationGroup ID='validation-group'><Name>ValidationGroupForEvent</Name></ValidationGroup></ValidationGroups>" +
                "<Panels>" +
                "<Panel ID='p1'><Name>What happened</Name><Areas><Area ID='entry-area'><Items><Item ID='entry-item' ViewName='Case Entry'/></Items></Area></Areas></Panel>" +
                "<Panel ID='p2'><Name>Evidence</Name><Areas><Area ID='evidence-area'><Items><Item ID='evidence-item' ViewName='Evidence'/></Items></Area><Area ID='save-area'><Items><Item ID='save-item'><Canvas><Control ID='save'/></Canvas></Item></Items></Area></Areas></Panel>" +
                "<Panel ID='p3'><Name>Review</Name><Areas><Area ID='review-area'><Items><Item ID='review-item' ViewName='Case Review'/></Items></Area><Area ID='submit-area'><Items><Item ID='submit-item'><Canvas><Control ID='submit'/></Canvas></Item></Items></Area></Areas></Panel>" +
                "</Panels><States><State><Events/></State></States></Form></Forms>";
            var transformed = GuidedJourneyRules.Apply(xml, form, null);
            GuidedJourneyRules.Verify(transformed, form, null);
            var document = XDocument.Parse(transformed);
            Assert(document.Descendants("Control").Count(x =>
                    (string)x.Attribute("Type") == "DataLabel" &&
                    ((string)x.Element("Name") ?? string.Empty).StartsWith(
                        "prgJourneyStep", StringComparison.Ordinal)) == 3 &&
                !document.Descendants("Control").Any(x =>
                    (string)x.Attribute("Type") == "Progress"),
                "guided journey emits one Form-supported textual progress indicator per screen");
            var firstProgress = document.Descendants("Control").Single(x =>
                (string)x.Element("Name") == "prgJourneyStep1");
            Assert(firstProgress.Descendants("Property").Any(x =>
                    (string)x.Element("Name") == "Text" &&
                    (string)x.Element("Value") == "Step 1 of 3: Describe"),
                "guided journey progress indicator states the current position and short label");
            var journeyTextControls = document.Descendants("Control").Where(x =>
                new[] { "Label", "DataLabel" }.Contains((string)x.Attribute("Type")) &&
                new[] { "lblJourneyTitle", "dlbJourneyDescription", "prgJourneyStep",
                    "lblJourneyStepHeading", "dlbJourneyStepDescription" }.Any(prefix =>
                        ((string)x.Element("Name") ?? string.Empty).StartsWith(
                            prefix, StringComparison.Ordinal))).ToList();
            Assert(journeyTextControls.Count == 15 &&
                journeyTextControls.All(control => control.Descendants("Property").Any(property =>
                    (string)property.Element("Name") == "LiteralVal" &&
                    (string)property.Element("DisplayValue") == "false" &&
                    (string)property.Element("NameValue") == "false" &&
                    (string)property.Element("Value") == "false")),
                "guided journey Form-level Label/DataLabel controls emit complete LiteralVal Boolean triples");
            Assert(document.Descendants("Control").Count(x =>
                    ((string)x.Element("Name") ?? string.Empty).StartsWith("lblJourneyTitle", StringComparison.Ordinal)) == 3 &&
                document.Descendants("Control").Count(x =>
                    ((string)x.Element("Name") ?? string.Empty).StartsWith("dlbJourneyDescription", StringComparison.Ordinal)) == 3,
                "guided journey emits its native title and description on every screen");
            var firstScreenHeading = document.Descendants("Control").Single(x =>
                (string)x.Element("Name") == "lblJourneyStepHeading1");
            Assert(firstScreenHeading.Descendants("Property").Any(x =>
                    (string)x.Element("Name") == "Text" &&
                    (string)x.Element("Value") == "What happened?"),
                "guided journey separates the short step label from the content-card title");
            var continueRule = document.Descendants("Event").Single(x => (string)x.Attribute("Type") == "User" &&
                (string)x.Attribute("SourceName") == "btnJourneyContinue1");
            var actions = continueRule.Descendants("Action").ToList();
            Assert(actions.Count == 2 && (string)actions[0].Attribute("Type") == "Validate" &&
                (string)actions[1].Attribute("Type") == "Focus",
                "Continue validates the visible screen before focusing the next step");
            var backRules = document.Descendants("Event").Where(x => (string)x.Attribute("Type") == "User" &&
                ((string)x.Attribute("SourceName") ?? string.Empty).StartsWith("btnJourneyBack", StringComparison.Ordinal)).ToList();
            Assert(backRules.Count == 2 && backRules.All(x => x.Descendants("Action").Count() == 1 &&
                (string)x.Descendants("Action").Single().Attribute("Type") == "Focus"),
                "Back navigation is non-destructive and does not validate");
            var continueButton = document.Descendants("Control").Single(x =>
                (string)x.Attribute("Type") == "Button" &&
                (string)x.Element("Name") == "btnJourneyContinue1");
            var backButton = document.Descendants("Control").First(x =>
                (string)x.Attribute("Type") == "Button" &&
                ((string)x.Element("Name") ?? string.Empty).StartsWith("btnJourneyBack", StringComparison.Ordinal));
            Assert(ReadButtonCellAlignment(document, (string)continueButton.Attribute("ID")) ==
                    FormActionAlignment.Right &&
                ReadButtonCellAlignment(document, (string)backButton.Attribute("ID")) ==
                    FormActionAlignment.Left,
                "guided Continue and Back use native semantic right/left Cell alignment");
            var drifted = XDocument.Parse(transformed);
            RemoveButtonCellAlignment(drifted, (string)continueButton.Attribute("ID"));
            AssertThrows(delegate { GuidedJourneyRules.Verify(
                    drifted.ToString(SaveOptions.DisableFormatting), form, null); },
                "native Table cell alignment");
        }

        private static void TestGuidedJourneyCompletionComposition()
        {
            var form = new FormDefinition { Name = "New Draft Case" };
            form.Tabs.Add(new FormTabDefinition { Name = "Details", Views = new List<string> { "Case Entry" } });
            form.Tabs.Add(new FormTabDefinition { Name = "Evidence", Views = new List<string> { "Evidence" } });
            form.Tabs.Add(new FormTabDefinition { Name = "Review & Finish", Views = new List<string> { "Case Review" } });
            form.CompletionButton = new CompletionButtonDefinition
            {
                Name = "btnFinishDraft",
                Text = "Finish",
                Tab = "Review & Finish",
                MessageTitle = "Draft complete",
                MessageBody = "Your draft is saved. It has not been submitted."
            };
            form.MasterDetail = new MasterDetailFormDefinition
            {
                MasterView = "Case Entry",
                MasterKeyProperty = "CaseId",
                Review = new MasterDetailReviewDefinition
                {
                    View = "Case Review", KeyProperty = "CaseId", Tab = "Review & Finish"
                }
            };
            form.MasterDetail.Details.Add(new MasterDetailChildDefinition { View = "Evidence", ForeignKeyProperty = "CaseId" });
            form.GuidedJourney = new GuidedJourneyDefinition
            {
                Title = "Prepare a case",
                Description = "Complete the screens, save the draft, and review it."
            };
            form.GuidedJourney.Steps.Add(new GuidedJourneyStepDefinition
            {
                Code = "DETAILS", Label = "Details", Description = "Describe the case.",
                Tab = "Details", Advance = "continue"
            });
            form.GuidedJourney.Steps.Add(new GuidedJourneyStepDefinition
            {
                Code = "EVIDENCE", Label = "Evidence", Description = "Add supporting records.",
                Tab = "Evidence", Advance = "save"
            });
            form.GuidedJourney.Steps.Add(new GuidedJourneyStepDefinition
            {
                Code = "REVIEW", Label = "Review", Description = "Review the saved draft.",
                Tab = "Review & Finish", Advance = "complete"
            });

            var xml = "<Forms><Form ID='form-id'><Name>New Draft Case</Name><Controls>" +
                "<Control ID='entry-area' Type='Area'/><Control ID='entry-item' Type='AreaItem'/>" +
                "<Control ID='evidence-area' Type='Area'/><Control ID='evidence-item' Type='AreaItem'/>" +
                "<Control ID='review-area' Type='Area'/><Control ID='review-item' Type='AreaItem'/>" +
                "<Control ID='save-area' Type='Area'/><Control ID='save-item' Type='AreaItem'/>" +
                "<Control ID='save' Type='Button'><Name>btnSave</Name><Properties><Property><Name>Text</Name><Value>Save draft and review</Value></Property></Properties></Control>" +
                "</Controls><ValidationGroups><ValidationGroup ID='validation-group'><Name>ValidationGroupForEvent</Name></ValidationGroup></ValidationGroups>" +
                "<Panels>" +
                "<Panel ID='p1'><Name>Details</Name><Areas><Area ID='entry-area'><Items><Item ID='entry-item' ViewName='Case Entry'/></Items></Area></Areas></Panel>" +
                "<Panel ID='p2'><Name>Evidence</Name><Areas><Area ID='evidence-area'><Items><Item ID='evidence-item' ViewName='Evidence'/></Items></Area><Area ID='save-area'><Items><Item ID='save-item'><Canvas><Control ID='save'/></Canvas></Item></Items></Area></Areas></Panel>" +
                "<Panel ID='p3'><Name>Review &amp; Finish</Name><Areas><Area ID='review-area'><Items><Item ID='review-item' ViewName='Case Review'/></Items></Area></Areas></Panel>" +
                "</Panels><States><State><Events/></State></States></Form></Forms>";
            var transformed = GuidedJourneyRules.Apply(xml, form, null);
            GuidedJourneyRules.Verify(transformed, form, null);
            var document = XDocument.Parse(transformed);
            var finish = document.Descendants("Control").Single(x =>
                (string)x.Attribute("Type") == "Button" &&
                (string)x.Element("Name") == "btnFinishDraft");
            Assert(finish.Descendants("Property").Any(x =>
                (string)x.Element("Name") == "ButtonStyle" &&
                (string)x.Element("Value") == "mainaction"),
                "workflow-free completion is the final primary action");
            var finishRule = document.Descendants("Event").Single(x =>
                (string)x.Attribute("Type") == "User" &&
                (string)x.Attribute("SourceName") == "btnFinishDraft");
            Assert(finishRule.Descendants("Action").Count() == 1 &&
                (string)finishRule.Descendants("Action").Single().Attribute("Type") == "ShowMessage",
                "workflow-free completion is an explicit native saved-draft confirmation");
            Assert(document.Descendants("Event").Any(x =>
                (string)x.Attribute("Type") == "System" &&
                (string)x.Attribute("SourceID") == (string)finish.Attribute("ID") &&
                (string)x.Element("Name") == "OnClick"),
                "workflow-free completion retains the Designer-hydratable system event");
            Assert(ReadButtonCellAlignment(document, (string)finish.Attribute("ID")) ==
                FormActionAlignment.Right,
                "workflow-free Finish uses native right-aligned Table-cell styling");
        }

        private static void TestWorkflowStartActionAlignment()
        {
            var definition = new FormDefinition { Name = "Submit Probe" };
            definition.Views.Add("Review View");
            definition.Tabs.Add(new FormTabDefinition
            {
                Name = "Review",
                Views = new List<string> { "Review View" }
            });
            definition.WorkflowStartButton = new WorkflowStartButtonDefinition
            {
                Name = "btnSubmitCase", Text = "Submit case", Tab = "Review"
            };
            var xml = "<Forms><Form ID='form'><Name>Submit Probe</Name><Controls>" +
                "<Control ID='old-panel' Type='Panel'><Name>Old</Name><Properties/></Control>" +
                "<Control ID='view-area' Type='Area'><Name>View Area</Name><Properties/></Control>" +
                "<Control ID='view-item' Type='AreaItem'><Name>Review View</Name><Properties/></Control>" +
                "</Controls><Panels><Panel ID='old-panel'><Name>Old</Name><Areas>" +
                "<Area ID='view-area'><Items><Item ID='view-item' ViewID='view-id' ViewName='Review View'/></Items></Area>" +
                "</Areas></Panel></Panels><States><State><Events/></State></States></Form></Forms>";
            var transformed = FormLayoutDefinition.Apply(xml, definition, null,
                new Dictionary<string, string>(), new Dictionary<Guid, ResolvedHeaderControlTransfer>());
            FormLayoutDefinition.Verify(transformed, definition, null,
                new Dictionary<string, string>(), new Dictionary<Guid, ResolvedHeaderControlTransfer>());
            var document = XDocument.Parse(transformed);
            var submit = document.Descendants("Control").Single(x =>
                (string)x.Attribute("Type") == "Button" &&
                (string)x.Element("Name") == "btnSubmitCase");
            Assert(ReadButtonCellAlignment(document, (string)submit.Attribute("ID")) ==
                FormActionAlignment.Right,
                "workflow Submit uses native right-aligned Table-cell styling");
            RemoveButtonCellAlignment(document, (string)submit.Attribute("ID"));
            AssertThrows(delegate
            {
                FormLayoutDefinition.Verify(document.ToString(SaveOptions.DisableFormatting),
                    definition, null, new Dictionary<string, string>(),
                    new Dictionary<Guid, ResolvedHeaderControlTransfer>());
            }, "native Table cell alignment");
        }

        private static void TestFormControlAvailability()
        {
            const string formWithProgress =
                "<Forms><Form ID='form'><Controls>" +
                "<Control ID='progress' Type='Progress'><Name>prgJourneyStep1</Name></Control>" +
                "<Control ID='cell' Type='Cell'><Name>Action Cell</Name><Styles>" +
                "<Style IsDefault='True'><Text><Align>Right</Align></Text></Style>" +
                "</Styles></Control></Controls></Form></Forms>";
            var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Progress", "UnavailableOnFormLevel" },
                { "Cell", "None" }
            };
            AssertThrows(delegate
            {
                SmartFormsManager.VerifyFormControlAvailability(
                    formWithProgress, "Availability Probe", flags);
            }, "UnavailableOnFormLevel");

            const string formWithSupportedControls =
                "<Forms><Form ID='form'><Controls>" +
                "<Control ID='progress' Type='DataLabel'><Name>prgJourneyStep1</Name></Control>" +
                "<Control ID='cell' Type='Cell'><Name>Action Cell</Name><Styles>" +
                "<Style IsDefault='True'><Text><Align>Right</Align></Text></Style>" +
                "</Styles></Control></Controls></Form></Forms>";
            flags["DataLabel"] = "None";
            SmartFormsManager.VerifyFormControlAvailability(
                formWithSupportedControls, "Availability Probe", flags);
        }

        private static void TestFormBooleanControlProperties()
        {
            var metadata = SmartFormsManager.ReadBooleanControlProperties(
                "<Properties><Prop ID='Title' type='string'/>" +
                "<Prop ID='IsVisible' type='bool'><Value>true</Value></Prop></Properties>");
            Assert(metadata.SetEquals(new[] { "IsVisible" }),
                "installed control metadata identifies Boolean properties");
            var booleanProperties = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Panel", metadata }
            };
            const string malformed =
                "<Forms><Form ID='form'><Controls><Control ID='review' Type='Panel'>" +
                "<Name>Review</Name><Properties><Property><Name>IsVisible</Name>" +
                "<DisplayValue>false</DisplayValue><NameValue/><Value>false</Value>" +
                "</Property></Properties></Control></Controls></Form></Forms>";
            AssertThrows(delegate
            {
                SmartFormsManager.VerifyFormBooleanControlProperties(
                    malformed, "Boolean Probe", booleanProperties);
            }, "Boolean DisplayValue/NameValue/Value triple");

            var panel = XElement.Parse(
                "<Control ID='review' Type='Panel'><Name>Review</Name><Properties/></Control>");
            MasterDetailRules.SetElementProperty(panel, "IsVisible", "false");
            var valid = "<Forms><Form ID='form'><Controls>" +
                panel.ToString(SaveOptions.DisableFormatting) +
                "</Controls></Form></Forms>";
            SmartFormsManager.VerifyFormBooleanControlProperties(
                valid, "Boolean Probe", booleanProperties);
            var property = panel.Descendants("Property").Single();
            Assert((string)property.Element("DisplayValue") == "false" &&
                (string)property.Element("NameValue") == "false" &&
                (string)property.Element("Value") == "false",
                "master-detail review visibility emits a complete Boolean property triple");

            const string labelMetadata =
                "<Properties><Prop ID='LiteralVal' type='bool'>" +
                "<InitialValue>false</InitialValue></Prop></Properties>";
            var initial = SmartFormsManager.ReadInitialBooleanControlProperties(labelMetadata);
            Assert(initial.SetEquals(new[] { "LiteralVal" }),
                "installed control metadata identifies design-time initialized Boolean properties");
            var labelBooleans = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Label", SmartFormsManager.ReadBooleanControlProperties(labelMetadata) }
            };
            var initialLabelBooleans = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Label", initial }
            };
            const string missingInitial =
                "<Forms><Form ID='form'><Controls><Control ID='title' Type='Label'>" +
                "<Name>Title</Name><Properties><Property><Name>Text</Name><Value>Title</Value>" +
                "</Property></Properties></Control></Controls></Form></Forms>";
            AssertThrows(delegate
            {
                SmartFormsManager.VerifyFormBooleanControlProperties(
                    missingInitial, "Boolean Probe", labelBooleans, initialLabelBooleans);
            }, "metadata-initialized Boolean property");

            const string completeInitial =
                "<Forms><Form ID='form'><Controls><Control ID='title' Type='Label'>" +
                "<Name>Title</Name><Properties><Property><Name>LiteralVal</Name>" +
                "<DisplayValue>false</DisplayValue><NameValue>false</NameValue><Value>false</Value>" +
                "</Property></Properties></Control></Controls></Form></Forms>";
            SmartFormsManager.VerifyFormBooleanControlProperties(
                completeInitial, "Boolean Probe", labelBooleans, initialLabelBooleans);
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
            view.Options.Add("labels-left");
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
            Assert(document.Descendants("Control").Any(control => (string)control.Attribute("ID") == "title-label" && control.Descendants("Property").Any(property => (string)property.Element("Name") == "Text" && (string)property.Element("Value") == "Case title:")), "friendly property label and default colon applied");
        }

        private static void TestResponsiveGroupedItemView()
        {
            var view = NewView("Public Intake", "Submission", "capture", "EmailAddress", "PhoneNumber", "Narrative", "NDAAccepted");
            Assert(view.LayoutColumns == 2 && view.Responsive &&
                !view.Options.Contains("labels-left", StringComparer.OrdinalIgnoreCase) &&
                view.Options.Contains("colon-labels", StringComparer.OrdinalIgnoreCase),
                "two-column label-above responsive Item View defaults");
            view.RequiredProperties.Add("EmailAddress");
            view.RequiredProperties.Add("NDAAccepted");
            view.Methods.Add("Create");
            view.Validations.Add(new FieldValidationDefinition
            {
                Property = "EmailAddress", Required = true, MinLength = 6, MaxLength = 120,
                Format = "email", Message = "Enter a valid email address.",
                ValidationPatternGuid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ValidationPatternName = "K2Skills.PublicIntake.EmailAddress"
            });
            view.Validations.Add(new FieldValidationDefinition { Property = "Narrative", MaxLength = 2000 });
            view.Validations.Add(new FieldValidationDefinition
            {
                Property = "NDAAccepted", Required = true, MustBeTrue = true
            });
            view.PropertyLabels["EmailAddress"] = "Email address";
            view.Sections.Add(new ViewSectionDefinition { Title = "Contact", Properties = new List<string> { "EmailAddress", "PhoneNumber" } });
            view.Sections.Add(new ViewSectionDefinition { Title = "Report", Properties = new List<string> { "Narrative", "NDAAccepted" } });
            view.Help.Add(new ViewHelpDefinition { Property = "NDAAccepted", LinkText = "Read the NDA", Title = "NDA", Body = "Approved terms." });

            var fields = new XElement("Fields");
            var controls = new XElement("Controls", ControlDefinition("body", "Table", null),
                ControlDefinition("column-1", "Column", "50%"), ControlDefinition("column-2", "Column", "50%"));
            var rows = new XElement("Rows");
            var names = new[] { "EmailAddress", "PhoneNumber", "Narrative", "NDAAccepted" };
            var types = new[] { "TextArea", "TextBox", "TextArea", "CheckBox" };
            XElement currentCells = null;
            for (var i = 0; i < names.Length; i++)
            {
                var key = "p" + i;
                fields.Add(new XElement("Field", new XAttribute("ID", "field-" + key), new XElement("FieldName", names[i])));
                controls.Add(FieldControlDefinition("label-" + key, "Label", "field-" + key));
                controls.Add(FieldControlDefinition("input-" + key, types[i], "field-" + key));
                if (i % 2 == 0)
                {
                    currentCells = new XElement("Cells");
                    rows.Add(new XElement("Row", new XAttribute("ID", "row-" + key), currentCells));
                }
                currentCells.Add(new XElement("Cell",
                    new XElement("Control", new XAttribute("ID", "label-" + key)),
                    new XElement("Control", new XAttribute("ID", "input-" + key))));
            }
            var xml = new XDocument(new XElement("View", new XAttribute("ID", "view-id"),
                new XElement("Name", view.Name), fields, controls,
                new XElement("Canvas", new XElement("Sections", new XElement("Section", new XAttribute("Type", "Body"),
                    new XElement("Control", new XAttribute("ID", "body"), new XAttribute("LayoutType", "Grid"),
                        new XElement("Columns",
                            new XElement("Column", new XAttribute("ID", "column-1"), new XAttribute("Size", "50%")),
                            new XElement("Column", new XAttribute("ID", "column-2"), new XAttribute("Size", "50%"))), rows)))),
                new XElement("States", new XElement("State", new XElement("Events",
                    new XElement("Event", new XElement("Handlers", new XElement("Handler",
                        new XElement("Actions", new XElement("Action",
                            new XAttribute("ID", "create-action"), new XAttribute("Type", "Execute"),
                            new XElement("Properties",
                                new XElement("Property", new XElement("Name", "Method"),
                                    new XElement("Value", "Create"))))))))))))).ToString(SaveOptions.DisableFormatting);

            var transformed = ViewPresentationDefinition.Apply(xml, view, false, false);
            ViewPresentationDefinition.Verify(transformed, view, false, false);
            var document = XDocument.Parse(transformed);
            Assert((string)document.Descendants("Control").Single(x => (string)x.Attribute("ID") == "input-p0" && x.Attribute("Type") != null).Attribute("Type") == "TextBox",
                "email Memo input promoted to TextBox");
            Assert(document.Descendants("Control").Single(x => (string)x.Attribute("ID") == "body" && x.Attribute("Type") != null).Descendants("Property")
                .Any(x => (string)x.Element("Name") == "IsResponsive" && (string)x.Element("Value") == "true"), "body Table responsive");
            Assert(document.Descendants("Control").Single(x => (string)x.Attribute("ID") == "label-p0" && x.Attribute("Type") != null).Descendants("Property")
                .Any(x => (string)x.Element("Name") == "Text" && (string)x.Element("Value") == "Email address:"), "label-above custom label keeps colon suffix");
            var generatedRowNames = document.Descendants("Control").Where(x =>
                (string)x.Attribute("Type") == "Row" &&
                (((string)x.Element("Name")) ?? string.Empty).StartsWith("Label Above Row ", StringComparison.Ordinal))
                .Select(x => (string)x.Element("Name")).ToList();
            Assert(generatedRowNames.Count > 0 &&
                generatedRowNames.Count == generatedRowNames.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                "label-above generated Row control names are unique");
            Assert(document.Descendants("Cell").Any(cell =>
                cell.Descendants("Control").Any(x => (string)x.Attribute("ID") == "label-p0") &&
                cell.Descendants("Control").Any(x => (string)x.Attribute("ID") == "input-p0")), "email label and input share one label-above cell");
            var emailControl = document.Descendants("Control").Single(x =>
                (string)x.Attribute("ID") == "input-p0" && x.Attribute("Type") != null);
            Assert(emailControl.Descendants("Property").Any(x =>
                (string)x.Element("Name") == "MaxLength" && (string)x.Element("Value") == "120"),
                "email maximum length applied");
            Assert(emailControl.Descendants("Property").Any(x =>
                (string)x.Element("Name") == "ValidationPattern" &&
                (string)x.Element("Value") == "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "email validation pattern applied");
            var validationGroup = document.Descendants("ValidationGroup").Single(x =>
                (string)x.Element("Name") == FieldValidationDefinitionXml.GroupName);
            Assert(validationGroup.Descendants("ValidationGroupControl").Any(x =>
                (string)x.Attribute("ControlID") == "input-p3" &&
                x.Element("Conditions") != null &&
                x.Descendants("Equals").Any(e => e.Elements("Item").All(i =>
                    (string)i.Attribute("DataType") == "Boolean"))),
                "must-be-true checkbox condition applied with native Boolean expression");
            var createAction = document.Descendants("Action").Single(x =>
                (string)x.Attribute("ID") == "create-action");
            var preceding = createAction.ElementsBeforeSelf("Action").Last();
            Assert((string)preceding.Attribute("Type") == "Validate" &&
                ReadActionProperty(preceding, "GroupID") == (string)validationGroup.Attribute("ID"),
                "field-validation group runs before Create");
            Assert(document.Descendants("Cell").Single(cell =>
                cell.Descendants("Control").Any(x => (string)x.Attribute("ID") == "input-p2")).Attribute("ColumnSpan").Value == "2",
                "narrative TextArea spans both label-above columns");
            var helpButton = document.Descendants("Control").Single(x =>
                (string)x.Attribute("Type") == "Button" &&
                (string)x.Element("Name") == "NDAAccepted More Info");
            Assert((string)helpButton.Descendants("Property").Single(x => (string)x.Element("Name") == "Text").Element("Value") == "Read the NDA",
                "NDA More info button created");
            Assert(document.Descendants("Event").Count(x =>
                (string)x.Attribute("SourceID") == (string)helpButton.Attribute("ID") &&
                (string)x.Element("Name") == "OnClick") == 1, "NDA help uses the Button OnClick event");
            Assert(document.Descendants("Action").Any(x => (string)x.Attribute("Type") == "ShowMessage"), "NDA help popup rule created");
            Assert(document.Descendants("Control").Count(x => (string)x.Attribute("Type") == "Label" &&
                (((string)x.Element("Name")) ?? string.Empty).EndsWith("Section Header", StringComparison.Ordinal)) == 2, "section headers created");
        }

        private static void TestGovernedLookupValidationComposition()
        {
            var view = NewView("Country Entry", "Country Entry", "capture", "CountryCode");
            view.Methods.Add("Create");
            view.LookupControls.Add(new LookupControlDefinition
            {
                Property = "CountryCode",
                Lookup = "Country"
            });
            view.LookupRequiredProperties.Add("CountryCode");
            view.RequiredProperties.Add("CountryCode");
            view.Validations.Add(new FieldValidationDefinition
            {
                Property = "CountryCode",
                Required = true,
                MinLength = 2,
                MaxLength = 2,
                Pattern = "[A-Z]{2}",
                Message = "Choose a valid country."
            });
            var document = new XDocument(new XElement("View", new XAttribute("ID", "country-view"),
                new XElement("Name", view.Name),
                new XElement("Fields", TestField("country-field", "CountryCode", "Text")),
                new XElement("Controls", FieldControlDefinition("country-control", "DropDown", "country-field")),
                new XElement("Events", TestMethodEvent("country-create-event",
                    TestViewAction("71000000-0000-0000-0000-000000000010",
                        Guid.Parse("71000000-0000-0000-0000-000000000001"),
                        view.Name, "Create", null, false)))));
            FieldValidationDefinitionXml.Apply(document, view);
            FieldValidationDefinitionXml.Verify(document, view);
            var control = document.Descendants("Control").Single(x =>
                (string)x.Attribute("ID") == "country-control");
            Assert(!control.Descendants("Property").Any(x =>
                new[] { "MaxLength", "ValidationPattern" }.Contains(
                    (string)x.Element("Name"), StringComparer.OrdinalIgnoreCase)),
                "governed lookup retains its declared text contract without TextBox-only properties");
            var group = document.Descendants("ValidationGroup").Single(x =>
                (string)x.Element("Name") == FieldValidationDefinitionXml.GroupName);
            Assert(group.Descendants("ValidationGroupControl").Any(x =>
                (string)x.Attribute("ControlID") == "country-control" &&
                (string)x.Attribute("IsRequired") == "True"),
                "governed lookup remains required in native validation");
            Assert(FieldValidationDefinitionXml.SatisfiesTextConstraint("AE", view.Validations[0]) &&
                !FieldValidationDefinitionXml.SatisfiesTextConstraint("ARE", view.Validations[0]) &&
                !FieldValidationDefinitionXml.SatisfiesTextConstraint("ae", view.Validations[0]),
                "governed lookup domain values satisfy declared length and pattern constraints");
        }

        private static void TestLabelAboveHiddenCellComposition()
        {
            var view = NewView("Contact Demo", "Contact", "capture", "TechnicalId", "DisplayName");
            view.HiddenProperties.Add("TechnicalId");
            view.PropertyLabels["DisplayName"] = "Display name";
            var xml = "<View><Fields><Field ID='technical-field'><FieldName>TechnicalId</FieldName></Field><Field ID='name-field'><FieldName>DisplayName</FieldName></Field></Fields><Controls>" +
                "<Control ID='technical-label' Type='Label'><Name>TechnicalId Label</Name><Properties><Property><Name>Text</Name><Value>Technical Id:</Value></Property></Properties></Control>" +
                "<Control ID='technical-input' Type='TextBox' FieldID='technical-field'><Name>TechnicalId Text Box</Name><Properties/></Control>" +
                "<Control ID='name-label' Type='Label'><Name>DisplayName Label</Name><Properties><Property><Name>Text</Name><Value>Display Name:</Value></Property></Properties></Control>" +
                "<Control ID='name-input' Type='TextBox' FieldID='name-field'><Name>DisplayName Text Box</Name><Properties/></Control></Controls>" +
                "<Canvas><Sections><Section Type='Body'><Control LayoutType='Grid'><Columns><Column ID='column-1'/><Column ID='column-2'/></Columns><Rows>" +
                "<Row><Cells><Cell><Control ID='technical-label'/><Control ID='technical-input'/></Cell><Cell><Control ID='name-label'/><Control ID='name-input'/></Cell></Cells></Row>" +
                "</Rows></Control></Section></Sections></Canvas></View>";
            var transformed = ViewPresentationDefinition.Apply(xml, view, false, false);
            ViewPresentationDefinition.Verify(transformed, view, false, false);
            var document = XDocument.Parse(transformed);
            Assert(!document.Descendants("Cell").Any(x => x.Descendants("Control").Any(c => (string)c.Attribute("ID") == "technical-input")),
                "hidden label-above property removes only its cell");
            Assert(document.Descendants("Cell").Any(x => x.Descendants("Control").Any(c => (string)c.Attribute("ID") == "name-input")),
                "adjacent visible label-above property is preserved");
            Assert(document.Descendants("Control").Single(x => (string)x.Attribute("ID") == "name-label" && x.Attribute("Type") != null)
                .Descendants("Property").Any(x => (string)x.Element("Name") == "Text" && (string)x.Element("Value") == "Display name:"),
                "label-above friendly label retains the colon suffix");
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

        private static void TestEditableListFileValidationControl()
        {
            var view = NewView("Evidence", "Evidence", "capture-list", "First", "FileContent", "Last");
            view.Methods.Add("Create");
            view.RequiredProperties.Add("FileContent");
            view.Validations.Add(new FieldValidationDefinition
                { Property = "FileContent", Required = true });
            var document = XDocument.Parse(EditableListXml(false));
            document.Descendants("Field").Single(x =>
                (string)x.Element("FieldName") == "Middle").Element("FieldName").Value = "FileContent";
            document.Descendants("Control").Single(x =>
                (string)x.Attribute("ID") == "middle-display" &&
                x.Attribute("Type") != null).SetAttributeValue("Type", "FilePostBack");
            document.Descendants("Control").Single(x =>
                (string)x.Attribute("ID") == "middle-edit" &&
                x.Attribute("Type") != null).SetAttributeValue("Type", "FilePostBack");
            document.Root.Element("Events").ReplaceWith(new XElement("Events",
                TestMethodEvent("file-create-event",
                    TestViewAction("51000000-0000-0000-0000-000000000010",
                        Guid.Parse("51000000-0000-0000-0000-000000000001"),
                        "Evidence", "Create", "Added", false))));

            var selected = ViewPresentationDefinition.FindEditableFieldControl(document, view, "FileContent");
            Assert((string)selected.Attribute("ID") == "middle-edit",
                "editable-list File validation targets the Edit template instead of the display File control");
            FieldValidationDefinitionXml.Apply(document, view);
            FieldValidationDefinitionXml.Verify(document, view);
            var group = document.Descendants("ValidationGroup").Single(x =>
                (string)x.Element("Name") == FieldValidationDefinitionXml.GroupName);
            Assert(group.Descendants("ValidationGroupControl").Any(x =>
                (string)x.Attribute("ControlID") == "middle-edit" &&
                (string)x.Attribute("IsRequired") == "True"),
                "required File validation group uses the editable-list Edit control");
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
                "<Handler ID='lookup-lines-" + id + "'><Actions><Action ID='lookup-lines-action-" + id + "' DefinitionID='70000000-0000-0000-0000-000000000001' Type='Execute' InstanceID='lines' IsReference='True' IsInherited='True'><Properties><Property><Name>Method</Name><Value>List</Value></Property><Property><Name>ControlID</Name><Value>line-type-lookup</Value></Property><Property><Name>ObjectID</Name><Value>71000000-0000-0000-0000-000000000001</Value></Property></Properties></Action></Actions></Handler>" +
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
            view.LayoutColumns = 2;
            if (string.Equals(type, "capture", StringComparison.OrdinalIgnoreCase))
                view.Options.Add("colon-labels");
            view.Properties.AddRange(properties);
            return view;
        }

        private static string ViewXml()
        {
            return "<View ID='22222222-2222-2222-2222-222222222222'><Name>Claim Lines</Name><DisplayName>Claim Lines</DisplayName><Fields>" +
                "<Field ID='category'><FieldName>CategoryCode</FieldName></Field>" +
                "<Field ID='status'><FieldName>Status</FieldName></Field>" +
                "</Fields><Controls>" +
                "<Control ID='categoryControl' Type='TextBox' FieldID='category'><Name>CategoryCode</Name><Properties /></Control>" +
                "<Control ID='statusControl' Type='TextBox' FieldID='status'><Name>Status</Name><Properties /></Control>" +
                "</Controls><Layout><Control ID='categoryControl' /><Control ID='statusControl' /></Layout>" +
                "<Events><Event Type='User' SourceType='View' SourceID='22222222-2222-2222-2222-222222222222'><Name>Init</Name><Handlers><Handler><Actions>" +
                "<Action Type='Execute'><Properties><Property><Name>Location</Name><Value>View</Value></Property><Property><Name>Method</Name><Value>List</Value></Property>" +
                "<Property><Name>ViewID</Name><Value>22222222-2222-2222-2222-222222222222</Value></Property><Property><Name>ControlID</Name><Value>categoryControl</Value></Property>" +
                "<Property><Name>ObjectID</Name><Value>99999999-9999-9999-9999-999999999999</Value></Property></Properties><Results><Result SourceID='99999999-9999-9999-9999-999999999999' SourceType='Result' TargetID='categoryControl' TargetType='Control' /></Results></Action>" +
                "<Action ID='unfiltered-detail-list' Type='Execute'><Properties><Property><Name>Location</Name><Value>View</Value></Property><Property><Name>Method</Name><Value>List</Value></Property>" +
                "<Property><Name>ViewID</Name><Value>22222222-2222-2222-2222-222222222222</Value></Property><Property><Name>ObjectID</Name><Value>88888888-8888-8888-8888-888888888888</Value></Property></Properties></Action>" +
                "</Actions></Handler></Handlers></Event><Event><Handlers><Handler><Actions><Action Type='Execute'><Properties><Property><Name>Method</Name><Value>Create</Value></Property></Properties>" +
                "<Parameters><Parameter SourceID='statusControl' SourceType='Control' TargetID='Status' TargetType='ObjectProperty' /></Parameters>" +
                "</Action></Actions></Handler></Handlers></Event></Events></View>";
        }

        private static void TestFormPreFillRules()
        {
            var viewGuid = Guid.Parse("61000000-0000-0000-0000-000000000001");
            var form = new FormDefinition { Name = "Pre-fill Probe" };
            Assert(form.PreFill.EffectiveEnabled, "Pre-fill defaults to enabled");
            form.Views.Add("Probe View");
            form.PreFill.Enabled = true;
            var resolved = new ResolvedFormPreFill();
            resolved.Targets.Add(new ResolvedPreFillTarget
            {
                ViewGuid = viewGuid, ViewName = "Probe View", Property = "EmailAddress",
                ControlId = "email-control", ControlName = "Email Text Box", Value = "prefill@example.com"
            });
            resolved.Targets.Add(new ResolvedPreFillTarget
            {
                ViewGuid = viewGuid, ViewName = "Probe View", Property = "Accepted",
                ControlId = "accepted-control", ControlName = "Accepted Check Box", Value = "true"
            });
            resolved.ManualProperties.Add("Probe View.FileContent");
            var xml = "<Forms><Form ID='form-id'><Name>Pre-fill Probe</Name><Controls>" +
                "<Control ID='visible-panel' Type='Panel'><Name>Entry</Name><Properties/></Control>" +
                "<Control ID='review-panel' Type='Panel'><Name>Review</Name><Properties/></Control>" +
                "<Control ID='probe-instance' Type='AreaItem'><Name>Probe View</Name><Properties/></Control>" +
                "</Controls><Panels>" +
                "<Panel ID='visible-panel'><Name>Entry</Name><Areas><Area ID='view-area'><Items>" +
                "<Item ID='probe-instance' ViewID='" + viewGuid + "' ViewName='Probe View'/></Items></Area></Areas></Panel>" +
                "<Panel ID='review-panel'><Name>Review</Name><Areas/></Panel>" +
                "</Panels><States><State><Events/></State></States></Form></Forms>";
            var transformed = FormPreFillRules.Apply(xml, form, resolved);
            FormPreFillRules.Verify(transformed, form, resolved);
            var document = XDocument.Parse(transformed);
            var button = document.Descendants("Control").Single(x =>
                (string)x.Element("Name") == FormPreFillRules.ButtonName &&
                x.Attribute("Type") != null);
            Assert((string)button.Descendants("Property").Single(x =>
                (string)x.Element("Name") == "Text").Element("Value") == "Pre-fill",
                "Pre-fill button has the required user-facing text");
            Assert(ReadButtonCellAlignment(document, (string)button.Attribute("ID")) ==
                FormActionAlignment.Left,
                "Pre-fill uses native left-aligned Table-cell styling");
            Assert(document.Descendants("Panel").Single(x => (string)x.Attribute("ID") == "review-panel")
                .Element("Areas").Elements("Area").Last().Descendants("Control")
                .Any(x => (string)x.Attribute("ID") == (string)button.Attribute("ID")),
                "Pre-fill button is last on the last visible panel");

            var guidedForm = new FormDefinition
            {
                Name = "Guided Pre-fill Probe",
                GuidedJourney = new GuidedJourneyDefinition()
            };
            guidedForm.Views.Add("Probe View");
            guidedForm.PreFill.Enabled = true;
            var guided = FormPreFillRules.Apply(xml, guidedForm, resolved);
            FormPreFillRules.Verify(guided, guidedForm, resolved);
            var guidedDocument = XDocument.Parse(guided);
            var guidedButton = guidedDocument.Descendants("Control").Single(x =>
                (string)x.Element("Name") == FormPreFillRules.ButtonName &&
                x.Attribute("Type") != null);
            Assert(guidedDocument.Descendants("Panel").Single(x =>
                    (string)x.Attribute("ID") == "visible-panel")
                .Element("Areas").Elements("Area").Last().Descendants("Control")
                .Any(x => (string)x.Attribute("ID") == (string)guidedButton.Attribute("ID")),
                "Pre-fill button is last on the first guided-journey panel");
            var transfer = document.Descendants("Action").Single(x => (string)x.Attribute("Type") == "Transfer");
            Assert(transfer.Descendants("Parameter").Count() == 2 &&
                transfer.Descendants("Parameter").All(x =>
                    (string)x.Attribute("TargetInstanceID") == "probe-instance"),
                "Pre-fill uses one Form transfer with exact View instances");
            Assert(document.Descendants("Action").Single(x =>
                (string)x.Attribute("Type") == "ShowMessage").Value.Contains("test-only"),
                "Pre-fill finishes with a test-only warning");

            string value;
            Assert(ResolvedFormPreFill.TryBuildValue(TestInputControl("TextBox", "Text"), "EmailAddress",
                new FieldValidationDefinition
                {
                    Property = "EmailAddress", Format = "email", MinLength = 20, MaxLength = 40
                }, out value) && value.Length >= 20 && value.Length <= 40 && value.Contains("@"),
                "Pre-fill creates a bounded email value");
            Assert(ResolvedFormPreFill.TryBuildValue(TestInputControl("TextArea", "Memo"), "Narrative",
                new FieldValidationDefinition
                {
                    Property = "Narrative", MinLength = 100, MaxLength = 120
                }, out value) && value.Length >= 100 && value.Length <= 120,
                "Pre-fill creates minimum-length narrative text");
            Assert(ResolvedFormPreFill.TryBuildValue(TestInputControl("TextBox", "Text"), "ResidenceCountryCode",
                new FieldValidationDefinition
                {
                    Property = "ResidenceCountryCode", MaxLength = 2
                }, out value) && value == "AE",
                "Pre-fill uses a deliberate bounded country code without truncation");
            Assert(ResolvedFormPreFill.TryBuildValue(TestInputControl("TextBox", "Text"), "ShortReference",
                new FieldValidationDefinition
                {
                    Property = "ShortReference", MaxLength = 2
                }, out value) && value == "XX",
                "Pre-fill synthesizes a bounded short value instead of clipping its label");
            Assert(!ResolvedFormPreFill.TryBuildValue(TestInputControl("TextBox", "Text"), "EmailAddress",
                new FieldValidationDefinition
                {
                    Property = "EmailAddress", Format = "email", MaxLength = 5
                }, out value),
                "Pre-fill leaves an impossible bounded format for manual input");
            Assert(ResolvedFormPreFill.TryBuildValue(TestInputControl("TextBox", "Number"), "Amount",
                new FieldValidationDefinition
                {
                    Property = "Amount", Minimum = 0, ExclusiveMinimum = true, Maximum = 10
                }, out value) && decimal.Parse(value) > 0 && decimal.Parse(value) < 10,
                "Pre-fill creates an in-range numeric value");
            Assert(!ResolvedFormPreFill.TryBuildValue(TestInputControl("TextBox", "Text"), "Code",
                new FieldValidationDefinition { Property = "Code", Pattern = "[A-Z]{3}" }, out value),
                "Pre-fill leaves custom patterns without examples for manual input");
            Assert(ResolvedFormPreFill.TryBuildValue(TestInputControl("TextBox", "Text"), "Code",
                new FieldValidationDefinition { Property = "Code", Pattern = "[A-Z]{3}", Example = "ABC" },
                out value) && value == "ABC", "Pre-fill uses a valid custom-pattern example");
            Assert(!ResolvedFormPreFill.TryBuildValue(TestInputControl("FilePostBack", "File"),
                "FileContent", null, out value), "Pre-fill leaves File upload manual");
            Assert(SmartFormsManifest.IsCountryReferenceProperty("ResidenceCountryCode") &&
                SmartFormsManifest.IsCountryReferenceProperty("CountryId") &&
                !SmartFormsManifest.IsCountryReferenceProperty("CountryName"),
                "editable country reference properties are recognized as governed lookups");
            var countryView = new ViewDefinition { Name = "Country Probe", Type = "capture" };
            countryView.Properties.Add("ResidenceCountryCode");
            Assert(SmartFormsManifest.FindUngovernedCountryReferences(countryView)
                .SequenceEqual(new[] { "ResidenceCountryCode" }),
                "free-text country references are rejected");
            countryView.LookupControls.Add(new LookupControlDefinition
                { Property = "ResidenceCountryCode", Lookup = "Country" });
            countryView.LookupRequiredProperties.Add("ResidenceCountryCode");
            Assert(SmartFormsManifest.FindUngovernedCountryReferences(countryView).Count == 0,
                "bound required country lookups satisfy the governed contract");

            var disabled = new FormDefinition { Name = "Pre-fill Probe" };
            disabled.Views.Add("Probe View");
            disabled.PreFill.Enabled = false;
            disabled.PreFill.DisabledReason = "Removed for production go-live.";
            FormPreFillRules.Verify(xml, disabled, new ResolvedFormPreFill());
            AssertThrows(delegate
            {
                FormPreFillRules.Verify(transformed, disabled, resolved);
            }, "retains the test-only Pre-fill helper");
        }

        private static string ReadButtonCellAlignment(XDocument document, string buttonId)
        {
            var reference = document.Descendants("Control").Single(x =>
                (string)x.Attribute("ID") == buttonId &&
                x.Ancestors("Cell").Any());
            var cellId = (string)reference.Ancestors("Cell").First().Attribute("ID");
            return document.Descendants("Control").Single(x =>
                    (string)x.Attribute("Type") == "Cell" &&
                    (string)x.Attribute("ID") == cellId)
                .Descendants("Align").Select(x => x.Value).SingleOrDefault();
        }

        private static void RemoveButtonCellAlignment(XDocument document, string buttonId)
        {
            var reference = document.Descendants("Control").Single(x =>
                (string)x.Attribute("ID") == buttonId &&
                x.Ancestors("Cell").Any());
            var cellId = (string)reference.Ancestors("Cell").First().Attribute("ID");
            var cell = document.Descendants("Control").Single(x =>
                (string)x.Attribute("Type") == "Cell" &&
                (string)x.Attribute("ID") == cellId);
            var styles = cell.Element("Styles");
            if (styles != null) styles.Remove();
        }

        private static XElement TestInputControl(string type, string dataType)
        {
            return new XElement("Control", new XAttribute("ID", Guid.NewGuid()),
                new XAttribute("Type", type), new XElement("Name", "Test Control"),
                new XElement("Properties", new XElement("Property",
                    new XElement("Name", "DataType"), new XElement("Value", dataType))));
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
