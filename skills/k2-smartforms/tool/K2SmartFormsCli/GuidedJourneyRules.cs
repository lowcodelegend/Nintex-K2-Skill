using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class GuidedJourneyRules
    {
        public static string Apply(string xml, FormDefinition definition, ResolvedCommonHeader header)
        {
            if (definition.GuidedJourney == null) return xml;
            var document = Parse(xml);
            var form = FindForm(document);
            var controls = RequiredChild(form, "Controls");
            var panels = RequiredChild(form, "Panels").Elements().Where(x => x.Name.LocalName == "Panel").ToList();
            var validationGroupId = FindValidationGroupId(form);

            for (var index = 0; index < definition.GuidedJourney.Steps.Count; index++)
            {
                var step = definition.GuidedJourney.Steps[index];
                var panel = panels.Single(x => string.Equals(ChildValue(x, "Name"), step.Tab, StringComparison.OrdinalIgnoreCase));
                var areas = RequiredChild(panel, "Areas");
                var progressArea = BuildProgressArea(form, controls, definition, step, index);
                var first = areas.Elements().FirstOrDefault(x => x.Name.LocalName == "Area");
                if (index == 0 && header != null && first != null &&
                    first.Descendants().Any(x => x.Name.LocalName == "Item" &&
                        string.Equals((string)x.Attribute("ViewID"), header.ViewGuid.ToString(), StringComparison.OrdinalIgnoreCase)))
                    first.AddAfterSelf(progressArea);
                else if (first == null) areas.Add(progressArea);
                else first.AddBeforeSelf(progressArea);

                var buttonArea = BuildNavigationArea(form, controls, definition, step, index, validationGroupId);
                if (buttonArea == null) continue;
                var actionArea = FindActionArea(form, areas, definition, step);
                if (actionArea != null) actionArea.AddBeforeSelf(buttonArea);
                else
                {
                    var footerArea = index == definition.GuidedJourney.Steps.Count - 1 && header != null && header.Footer != null
                        ? areas.Elements().FirstOrDefault(x => x.Descendants().Any(item => item.Name.LocalName == "Item" &&
                            string.Equals((string)item.Attribute("ViewID"), header.Footer.ViewGuid.ToString(), StringComparison.OrdinalIgnoreCase)))
                        : null;
                    if (footerArea == null) areas.Add(buttonArea); else footerArea.AddBeforeSelf(buttonArea);
                }
            }
            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static void Verify(string xml, FormDefinition definition, ResolvedCommonHeader header)
        {
            if (definition.GuidedJourney == null) return;
            var form = FindForm(Parse(xml));
            var controls = RequiredChild(form, "Controls");
            var baseState = RequiredChild(RequiredChild(form, "States"), "State");
            var panels = RequiredChild(form, "Panels").Elements().Where(x => x.Name.LocalName == "Panel").ToList();
            var validationGroupId = FindValidationGroupId(form);

            for (var index = 0; index < definition.GuidedJourney.Steps.Count; index++)
            {
                var step = definition.GuidedJourney.Steps[index];
                var panel = panels.Single(x => string.Equals(ChildValue(x, "Name"), step.Tab, StringComparison.OrdinalIgnoreCase));
                var progressName = ProgressName(index);
                var progress = controls.Elements().SingleOrDefault(x => x.Name.LocalName == "Control" &&
                    string.Equals((string)x.Attribute("Type"), "Progress", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ChildValue(x, "Name"), progressName, StringComparison.OrdinalIgnoreCase));
                if (progress == null || !PanelContainsControl(panel, (string)progress.Attribute("ID")))
                    throw new CliException("K2 Form '" + definition.Name + "' guided journey step '" + step.Code + "' is missing its native Progress control.");
                AssertProperty(progress, "FixedListItems", SerializeSteps(definition.GuidedJourney, step.Code), definition.Name, step.Code);
                AssertProperty(progress, "Text", step.Code, definition.Name, step.Code);
                AssertProperty(progress, "IsReadOnly", "true", definition.Name, step.Code);
                AssertProperty(progress, "IsEnabled", "true", definition.Name, step.Code);

                var journeyTitle = controls.Elements().SingleOrDefault(x => x.Name.LocalName == "Control" &&
                    string.Equals((string)x.Attribute("Type"), "Label", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ChildValue(x, "Name"), JourneyTitleName(index), StringComparison.OrdinalIgnoreCase));
                if (journeyTitle == null || !PanelContainsControl(panel, (string)journeyTitle.Attribute("ID")) ||
                    !string.Equals(ReadProperty(journeyTitle, "Text"), definition.GuidedJourney.Title, StringComparison.Ordinal))
                    throw new CliException("K2 Form '" + definition.Name + "' guided journey step '" + step.Code +
                        "' is missing its native journey title.");

                var journeyDescription = controls.Elements().SingleOrDefault(x => x.Name.LocalName == "Control" &&
                    string.Equals((string)x.Attribute("Type"), "DataLabel", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ChildValue(x, "Name"), JourneyDescriptionName(index), StringComparison.OrdinalIgnoreCase));
                if (journeyDescription == null || !PanelContainsControl(panel, (string)journeyDescription.Attribute("ID")) ||
                    !string.Equals(ReadProperty(journeyDescription, "Text"), definition.GuidedJourney.Description, StringComparison.Ordinal))
                    throw new CliException("K2 Form '" + definition.Name + "' guided journey step '" + step.Code +
                        "' is missing its native journey description.");

                var heading = controls.Elements().SingleOrDefault(x => x.Name.LocalName == "Control" &&
                    string.Equals((string)x.Attribute("Type"), "Label", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ChildValue(x, "Name"), HeadingName(index), StringComparison.OrdinalIgnoreCase));
                if (heading == null || !PanelContainsControl(panel, (string)heading.Attribute("ID")) ||
                    !string.Equals(ReadProperty(heading, "Text"), StepHeading(definition.GuidedJourney, step, index), StringComparison.Ordinal))
                    throw new CliException("K2 Form '" + definition.Name + "' guided journey step '" + step.Code + "' is missing its current-screen heading.");

                var description = controls.Elements().SingleOrDefault(x => x.Name.LocalName == "Control" &&
                    string.Equals((string)x.Attribute("Type"), "DataLabel", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ChildValue(x, "Name"), DescriptionName(index), StringComparison.OrdinalIgnoreCase));
                if (description == null || !PanelContainsControl(panel, (string)description.Attribute("ID")) ||
                    !string.Equals(ReadProperty(description, "Text"), step.Description, StringComparison.Ordinal))
                    throw new CliException("K2 Form '" + definition.Name + "' guided journey step '" + step.Code + "' is missing its screen description.");

                if (index > 0) VerifyNavigationButton(form, baseState, controls, panel, definition,
                    BackName(index), definition.GuidedJourney.BackButtonText,
                    definition.GuidedJourney.Steps[index - 1].Tab, false, null);
                if (string.Equals(step.Advance, "continue", StringComparison.OrdinalIgnoreCase))
                    VerifyNavigationButton(form, baseState, controls, panel, definition,
                        ContinueName(index), definition.GuidedJourney.ContinueButtonText,
                        definition.GuidedJourney.Steps[index + 1].Tab,
                        definition.GuidedJourney.ValidateOnContinue && !string.IsNullOrWhiteSpace(validationGroupId),
                        validationGroupId);
                if (string.Equals(step.Advance, "complete", StringComparison.OrdinalIgnoreCase))
                    VerifyCompletionButton(baseState, controls, panel, definition);
            }

            var saveStep = definition.GuidedJourney.Steps[definition.GuidedJourney.Steps.Count - 2];
            var savePanel = panels.Single(x => string.Equals(ChildValue(x, "Name"), saveStep.Tab, StringComparison.OrdinalIgnoreCase));
            var saveControl = FindNamedControl(controls, "btnSave", "Button");
            if (saveControl == null || !PanelContainsControl(savePanel, (string)saveControl.Attribute("ID")))
                throw new CliException("K2 Form '" + definition.Name + "' guided journey Save step does not contain the generated btnSave action.");

            if (definition.WorkflowStartButton != null)
            {
                var submitStep = definition.GuidedJourney.Steps.Last();
                var submitPanel = panels.Single(x => string.Equals(ChildValue(x, "Name"), submitStep.Tab, StringComparison.OrdinalIgnoreCase));
                var submitControl = FindNamedControl(controls, definition.WorkflowStartButton.Name, "Button");
                if (submitControl == null || !PanelContainsControl(submitPanel, (string)submitControl.Attribute("ID")))
                    throw new CliException("K2 Form '" + definition.Name + "' guided journey final step does not contain its workflow Submit action.");
            }
        }

        private static XElement BuildProgressArea(XElement form, XElement controls, FormDefinition definition,
            GuidedJourneyStepDefinition step, int index)
        {
            var ns = form.Name.Namespace;
            var progressId = NewId();
            var journeyTitleId = NewId();
            var journeyDescriptionId = NewId();
            var headingId = NewId();
            var descriptionId = NewId();
            var tableId = NewId();
            var row1Id = NewId();
            var row2Id = NewId();
            var row3Id = NewId();
            var row4Id = NewId();
            var row5Id = NewId();
            var cell1Id = NewId();
            var cell2Id = NewId();
            var cell3Id = NewId();
            var cell4Id = NewId();
            var cell5Id = NewId();
            var areaId = NewId();
            var itemId = NewId();
            var progressName = ProgressName(index);
            var headingName = HeadingName(index);
            var descriptionName = DescriptionName(index);

            controls.Add(new XElement(ns + "Control", new XAttribute("ID", journeyTitleId), new XAttribute("Type", "Label"),
                new XElement(ns + "Name", JourneyTitleName(index)), new XElement(ns + "DisplayName", JourneyTitleName(index)),
                new XElement(ns + "Properties",
                    Property(ns, "ControlName", JourneyTitleName(index)),
                    Property(ns, "Text", definition.GuidedJourney.Title),
                    Property(ns, "Width", "100%")),
                new XElement(ns + "Styles",
                    new XElement(ns + "Style", new XAttribute("IsDefault", "True"),
                        new XElement(ns + "Font", new XElement(ns + "Weight", "Bold"), new XElement(ns + "Color", "#101828"))))));
            controls.Add(new XElement(ns + "Control", new XAttribute("ID", journeyDescriptionId), new XAttribute("Type", "DataLabel"),
                new XElement(ns + "Name", JourneyDescriptionName(index)), new XElement(ns + "DisplayName", JourneyDescriptionName(index)),
                new XElement(ns + "Properties",
                    Property(ns, "ControlName", JourneyDescriptionName(index)),
                    Property(ns, "Text", definition.GuidedJourney.Description),
                    Property(ns, "Width", "100%")),
                new XElement(ns + "Styles",
                    new XElement(ns + "Style", new XAttribute("IsDefault", "True"),
                        new XElement(ns + "Font", new XElement(ns + "Color", "#667085")),
                        new XElement(ns + "Padding", new XElement(ns + "Bottom", "14px"))))));
            controls.Add(Control(ns, progressId, "Progress", progressName,
                Property(ns, "Width", "100%"),
                Property(ns, "DataSourceType", "Static"),
                Property(ns, "FixedListItems", SerializeSteps(definition.GuidedJourney, step.Code),
                    string.Join("; ", definition.GuidedJourney.Steps.Select(x => x.Label).ToArray())),
                Property(ns, "Text", step.Code),
                Property(ns, "IsReadOnly", "true"),
                Property(ns, "IsEnabled", "true")));
            controls.Add(new XElement(ns + "Control", new XAttribute("ID", headingId), new XAttribute("Type", "Label"),
                new XElement(ns + "Name", headingName), new XElement(ns + "DisplayName", headingName),
                new XElement(ns + "Properties",
                    Property(ns, "ControlName", headingName),
                    Property(ns, "Text", StepHeading(definition.GuidedJourney, step, index)),
                    Property(ns, "Width", "100%")),
                new XElement(ns + "Styles",
                    new XElement(ns + "Style", new XAttribute("IsDefault", "True"),
                        new XElement(ns + "Font", new XElement(ns + "Weight", "Bold"), new XElement(ns + "Color", "#17263a")),
                        new XElement(ns + "Padding", new XElement(ns + "Top", "14px"))))));
            controls.Add(new XElement(ns + "Control", new XAttribute("ID", descriptionId), new XAttribute("Type", "DataLabel"),
                new XElement(ns + "Name", descriptionName), new XElement(ns + "DisplayName", descriptionName),
                new XElement(ns + "Properties",
                    Property(ns, "ControlName", descriptionName),
                    Property(ns, "Text", step.Description),
                    Property(ns, "Width", "100%")),
                new XElement(ns + "Styles",
                    new XElement(ns + "Style", new XAttribute("IsDefault", "True"),
                        new XElement(ns + "Font", new XElement(ns + "Color", "#53657a")),
                        new XElement(ns + "Padding", new XElement(ns + "Top", "8px"), new XElement(ns + "Bottom", "16px"))))));
            controls.Add(Control(ns, tableId, "Table", "tblJourneyProgress" + (index + 1),
                Property(ns, "IsResponsive", "true")));
            controls.Add(Control(ns, row1Id, "Row", "Journey Progress Row " + (index + 1)));
            controls.Add(Control(ns, row2Id, "Row", "Journey Heading Row " + (index + 1)));
            controls.Add(Control(ns, row3Id, "Row", "Journey Description Row " + (index + 1)));
            controls.Add(Control(ns, cell1Id, "Cell", "Journey Progress Cell " + (index + 1)));
            controls.Add(Control(ns, cell2Id, "Cell", "Journey Heading Cell " + (index + 1)));
            controls.Add(Control(ns, cell3Id, "Cell", "Journey Description Cell " + (index + 1)));
            controls.Add(Control(ns, areaId, "Area", "Journey Progress Area " + (index + 1)));
            controls.Add(Control(ns, itemId, "AreaItem", "Journey Progress Area Item " + (index + 1)));

            return new XElement(ns + "Area", new XAttribute("ID", areaId),
                new XElement(ns + "Items", new XElement(ns + "Item", new XAttribute("ID", itemId),
                    new XElement(ns + "Canvas",
                        new XElement(ns + "Control", new XAttribute("ID", tableId), new XAttribute("LayoutType", "Grid"),
                            new XElement(ns + "Columns", new XElement(ns + "Column", new XAttribute("ID", NewId()), new XAttribute("Size", "100%"))),
                            new XElement(ns + "Rows",
                                BuildRow(ns, row1Id, cell1Id, journeyTitleId),
                                BuildRow(ns, row2Id, cell2Id, journeyDescriptionId),
                                BuildRow(ns, row3Id, cell3Id, progressId),
                                BuildRow(ns, row4Id, cell4Id, headingId),
                                BuildRow(ns, row5Id, cell5Id, descriptionId)))))));
        }

        private static XElement BuildNavigationArea(XElement form, XElement controls, FormDefinition definition,
            GuidedJourneyStepDefinition step, int index, string validationGroupId)
        {
            var hasBack = index > 0;
            var hasContinue = string.Equals(step.Advance, "continue", StringComparison.OrdinalIgnoreCase);
            var hasComplete = string.Equals(step.Advance, "complete", StringComparison.OrdinalIgnoreCase);
            if (!hasBack && !hasContinue && !hasComplete) return null;
            var ns = form.Name.Namespace;
            var tableId = NewId();
            var rowId = NewId();
            var leftCellId = NewId();
            var rightCellId = NewId();
            var areaId = NewId();
            var itemId = NewId();
            string backId = null;
            string forwardId = null;

            controls.Add(Control(ns, tableId, "Table", "tblJourneyActions" + (index + 1),
                Property(ns, "IsResponsive", "true")));
            controls.Add(Control(ns, rowId, "Row", "Journey Actions Row " + (index + 1)));
            controls.Add(FormActionAlignment.CellControl(ns, leftCellId,
                "Journey Back Cell " + (index + 1), FormActionAlignment.Left));
            controls.Add(FormActionAlignment.CellControl(ns, rightCellId,
                "Journey Continue Cell " + (index + 1), FormActionAlignment.Right));
            if (hasBack)
            {
                backId = NewId();
                controls.Add(Control(ns, backId, "Button", BackName(index),
                    Property(ns, "Text", definition.GuidedJourney.BackButtonText)));
                AddNavigationRule(form, definition, backId, BackName(index),
                    definition.GuidedJourney.Steps[index - 1].Tab, false, validationGroupId);
            }
            if (hasContinue)
            {
                forwardId = NewId();
                controls.Add(Control(ns, forwardId, "Button", ContinueName(index),
                    Property(ns, "Text", definition.GuidedJourney.ContinueButtonText),
                    Property(ns, "ButtonStyle", "mainaction")));
                AddNavigationRule(form, definition, forwardId, ContinueName(index),
                    definition.GuidedJourney.Steps[index + 1].Tab,
                    definition.GuidedJourney.ValidateOnContinue, validationGroupId);
            }
            if (hasComplete)
            {
                forwardId = NewId();
                controls.Add(Control(ns, forwardId, "Button", definition.CompletionButton.Name,
                    Property(ns, "Text", definition.CompletionButton.Text),
                    Property(ns, "ButtonStyle", "mainaction")));
                AddCompletionRule(form, definition, forwardId);
            }
            controls.Add(Control(ns, areaId, "Area", "Journey Actions Area " + (index + 1)));
            controls.Add(Control(ns, itemId, "AreaItem", "Journey Actions Area Item " + (index + 1)));

            return new XElement(ns + "Area", new XAttribute("ID", areaId),
                new XElement(ns + "Items", new XElement(ns + "Item", new XAttribute("ID", itemId),
                    new XElement(ns + "Canvas",
                        new XElement(ns + "Control", new XAttribute("ID", tableId), new XAttribute("LayoutType", "Grid"),
                            new XElement(ns + "Columns",
                                new XElement(ns + "Column", new XAttribute("ID", NewId()), new XAttribute("Size", "50%")),
                                new XElement(ns + "Column", new XAttribute("ID", NewId()), new XAttribute("Size", "50%"))),
                            new XElement(ns + "Rows",
                                new XElement(ns + "Row", new XAttribute("ID", rowId),
                                    new XElement(ns + "Cells",
                                        BuildCell(ns, leftCellId, backId),
                                        BuildCell(ns, rightCellId, forwardId)))))))));
        }

        private static void AddNavigationRule(XElement form, FormDefinition definition, string controlId,
            string controlName, string targetTab, bool validate, string validationGroupId)
        {
            var ns = form.Name.Namespace;
            var events = RequiredChild(RequiredChild(form, "States"), "State").Elements()
                .FirstOrDefault(x => x.Name.LocalName == "Events");
            if (events == null)
            {
                events = new XElement(ns + "Events");
                RequiredChild(RequiredChild(form, "States"), "State").Add(events);
            }
            var actions = new XElement(ns + "Actions");
            if (validate && !string.IsNullOrWhiteSpace(validationGroupId))
                actions.Add(BuildValidateAction(ns, validationGroupId));
            actions.Add(BuildFocusAction(form, targetTab));
            events.Add(ControlRuleDefinition.BuildSystemEvent(ns, controlId, "OnClick"));
            events.Add(new XElement(ns + "Event",
                new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "User"), new XAttribute("SourceID", controlId),
                new XAttribute("SourceType", "Control"), new XAttribute("SourceName", controlName),
                new XAttribute("SourceDisplayName", controlName),
                new XElement(ns + "Name", "OnClick"),
                new XElement(ns + "Properties",
                    Property(ns, "RuleFriendlyName", "When " + controlName + " is Clicked"),
                    Property(ns, "Location", definition.Name)),
                new XElement(ns + "Handlers",
                    new XElement(ns + "Handler", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                        new XElement(ns + "Properties",
                            Property(ns, "HandlerName", "IfLogicalHandler"),
                            Property(ns, "Location", "form")),
                        actions))));
        }

        private static void VerifyNavigationButton(XElement form, XElement baseState, XElement controls, XElement panel,
            FormDefinition definition, string name, string text, string targetTab, bool expectValidation, string validationGroupId)
        {
            var control = FindNamedControl(controls, name, "Button");
            if (control == null || !PanelContainsControl(panel, (string)control.Attribute("ID")))
                throw new CliException("K2 Form '" + definition.Name + "' guided journey is missing navigation button '" + name + "'.");
            AssertProperty(control, "Text", text, definition.Name, name);
            var id = (string)control.Attribute("ID");
            FormActionAlignment.VerifyButtonCell(panel, controls, id,
                name.StartsWith("btnJourneyBack", StringComparison.OrdinalIgnoreCase)
                    ? FormActionAlignment.Left
                    : FormActionAlignment.Right,
                "K2 Form '" + definition.Name + "' guided journey button '" + name + "'");
            var rules = baseState.Descendants().Where(x => x.Name.LocalName == "Event" &&
                string.Equals((string)x.Attribute("Type"), "User", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("SourceID"), id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ChildValue(x, "Name"), "OnClick", StringComparison.OrdinalIgnoreCase)).ToList();
            if (rules.Count != 1)
                throw new CliException("K2 Form '" + definition.Name + "' guided journey button '" + name + "' must have exactly one OnClick rule.");
            ControlRuleDefinition.VerifySystemEvent(baseState, id, "OnClick",
                "K2 Form '" + definition.Name + "' guided journey button '" + name + "'");
            var actions = rules[0].Descendants().Where(x => x.Name.LocalName == "Action").ToList();
            var targetPanel = RequiredChild(form, "Panels").Elements().Single(x => x.Name.LocalName == "Panel" &&
                string.Equals(ChildValue(x, "Name"), targetTab, StringComparison.OrdinalIgnoreCase));
            var expectedCount = expectValidation ? 2 : 1;
            if (actions.Count != expectedCount ||
                !IsFocusAction(actions.Last(), (string)targetPanel.Attribute("ID")))
                throw new CliException("K2 Form '" + definition.Name + "' guided journey button '" + name +
                    "' must end with exactly one native Focus action to tab '" + targetTab + "'.");
            if (expectValidation && (!string.Equals((string)actions[0].Attribute("Type"), "Validate", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(ReadProperty(actions[0], "GroupID"), validationGroupId, StringComparison.OrdinalIgnoreCase)))
                throw new CliException("K2 Form '" + definition.Name + "' guided journey Continue button '" + name +
                    "' must validate the Form validation group before changing screens.");
        }

        private static void AddCompletionRule(XElement form, FormDefinition definition, string controlId)
        {
            var ns = form.Name.Namespace;
            var events = RequiredChild(RequiredChild(form, "States"), "State").Elements()
                .FirstOrDefault(x => x.Name.LocalName == "Events");
            if (events == null)
            {
                events = new XElement(ns + "Events");
                RequiredChild(RequiredChild(form, "States"), "State").Add(events);
            }
            var button = definition.CompletionButton;
            events.Add(ControlRuleDefinition.BuildSystemEvent(ns, controlId, "OnClick"));
            events.Add(new XElement(ns + "Event",
                new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "User"), new XAttribute("SourceID", controlId),
                new XAttribute("SourceType", "Control"), new XAttribute("SourceName", button.Name),
                new XAttribute("SourceDisplayName", button.Name),
                new XElement(ns + "Name", "OnClick"),
                new XElement(ns + "Properties",
                    Property(ns, "RuleFriendlyName", "When " + button.Name + " is Clicked"),
                    Property(ns, "Location", definition.Name)),
                new XElement(ns + "Handlers",
                    new XElement(ns + "Handler", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                        new XElement(ns + "Properties",
                            Property(ns, "HandlerName", "IfLogicalHandler"),
                            Property(ns, "Location", "form")),
                        new XElement(ns + "Actions",
                            BuildCompletionMessage(ns, button.MessageTitle, button.MessageBody))))));
        }

        private static void VerifyCompletionButton(XElement baseState, XElement controls, XElement panel,
            FormDefinition definition)
        {
            var button = definition.CompletionButton;
            var control = FindNamedControl(controls, button.Name, "Button");
            if (control == null || !PanelContainsControl(panel, (string)control.Attribute("ID")))
                throw new CliException("K2 Form '" + definition.Name +
                    "' guided journey final step does not contain its workflow-free completion action.");
            AssertProperty(control, "Text", button.Text, definition.Name, button.Name);
            AssertProperty(control, "ButtonStyle", "mainaction", definition.Name, button.Name);
            var id = (string)control.Attribute("ID");
            FormActionAlignment.VerifyButtonCell(panel, controls, id, FormActionAlignment.Right,
                "K2 Form '" + definition.Name + "' guided journey completion button '" + button.Name + "'");
            ControlRuleDefinition.VerifySystemEvent(baseState, id, "OnClick",
                "K2 Form '" + definition.Name + "' guided journey completion button '" + button.Name + "'");
            var rules = baseState.Descendants().Where(x => x.Name.LocalName == "Event" &&
                string.Equals((string)x.Attribute("Type"), "User", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("SourceID"), id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ChildValue(x, "Name"), "OnClick", StringComparison.OrdinalIgnoreCase)).ToList();
            if (rules.Count != 1)
                throw new CliException("K2 Form '" + definition.Name + "' guided journey completion button '" +
                    button.Name + "' must have exactly one OnClick rule.");
            var actions = rules[0].Descendants().Where(x => x.Name.LocalName == "Action").ToList();
            if (actions.Count != 1 ||
                !string.Equals((string)actions[0].Attribute("Type"), "ShowMessage", StringComparison.OrdinalIgnoreCase) ||
                !HasMessageValue(actions[0], "Title", button.MessageTitle) ||
                !HasMessageValue(actions[0], "Body", button.MessageBody))
                throw new CliException("K2 Form '" + definition.Name + "' guided journey completion button '" +
                    button.Name + "' must show the configured saved-draft confirmation exactly once.");
        }

        private static XElement FindActionArea(XElement form, XElement areas, FormDefinition definition, GuidedJourneyStepDefinition step)
        {
            string actionName = null;
            if (string.Equals(step.Advance, "save", StringComparison.OrdinalIgnoreCase)) actionName = "btnSave";
            if (string.Equals(step.Advance, "submit", StringComparison.OrdinalIgnoreCase)) actionName = definition.WorkflowStartButton.Name;
            if (actionName == null) return null;
            var control = FindNamedControl(RequiredChild(form, "Controls"), actionName, "Button");
            var controlId = control == null ? null : (string)control.Attribute("ID");
            return string.IsNullOrWhiteSpace(controlId) ? null : areas.Elements().FirstOrDefault(x =>
                x.Descendants().Any(y => y.Name.LocalName == "Control" &&
                    string.Equals((string)y.Attribute("ID"), controlId, StringComparison.OrdinalIgnoreCase)));
        }

        private static XElement BuildValidateAction(XNamespace ns, string groupId)
        {
            return new XElement(ns + "Action", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "Validate"), new XAttribute("ExecutionType", "Synchronous"),
                new XElement(ns + "Properties",
                    Property(ns, "Location", "Form"),
                    Property(ns, "MessageLocation", "Control"),
                    Property(ns, "GroupID", groupId, "ValidationGroupForEvent"),
                    Property(ns, "IgnoreInvisibleControls", "true"),
                    Property(ns, "IgnoreDisabledControls", "true"),
                    Property(ns, "IgnoreReadOnlyControls", "true")));
        }

        private static XElement BuildCompletionMessage(XNamespace ns, string title, string body)
        {
            return new XElement(ns + "Action", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "ShowMessage"), new XAttribute("ExecutionType", "Synchronous"),
                new XElement(ns + "Properties",
                    Property(ns, "Location", "Form"),
                    Property(ns, "MessageLocation", "Popup")),
                new XElement(ns + "Parameters",
                    MessageParameter(ns, "Size", "small"),
                    MessageParameter(ns, "Type", "info"),
                    MessageParameter(ns, "Title", title),
                    MessageParameter(ns, "Body", body)));
        }

        private static XElement MessageParameter(XNamespace ns, string target, string value)
        {
            return new XElement(ns + "Parameter", new XAttribute("SourceID", "Sources"),
                new XAttribute("SourceType", "Value"), new XAttribute("TargetID", target),
                new XAttribute("TargetName", target), new XAttribute("TargetType", "MessageProperty"),
                new XElement(ns + "SourceValue", new XAttribute(XNamespace.Xml + "space", "preserve"),
                    new XElement(ns + "Source", new XAttribute("SourceType", "Value"), value)));
        }

        private static bool HasMessageValue(XElement action, string target, string expected)
        {
            return action.Descendants().Any(x => x.Name.LocalName == "Parameter" &&
                string.Equals((string)x.Attribute("TargetID"), target, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Descendants().FirstOrDefault(y => y.Name.LocalName == "Source") == null
                    ? null
                    : x.Descendants().First(y => y.Name.LocalName == "Source").Value,
                    expected, StringComparison.Ordinal));
        }

        private static XElement BuildFocusAction(XElement form, string tabName)
        {
            var ns = form.Name.Namespace;
            var panel = RequiredChild(form, "Panels").Elements().Single(x => x.Name.LocalName == "Panel" &&
                string.Equals(ChildValue(x, "Name"), tabName, StringComparison.OrdinalIgnoreCase));
            return new XElement(ns + "Action", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "Focus"), new XAttribute("ExecutionType", "Synchronous"),
                new XElement(ns + "Properties",
                    Property(ns, "Location", "Form"),
                    Property(ns, "PanelID", (string)panel.Attribute("ID"), tabName)));
        }

        private static bool IsFocusAction(XElement action, string panelId)
        {
            return string.Equals((string)action.Attribute("Type"), "Focus", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadProperty(action, "Location"), "Form", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadProperty(action, "PanelID"), panelId, StringComparison.OrdinalIgnoreCase);
        }

        private static string FindValidationGroupId(XElement form)
        {
            var groups = form.Elements().FirstOrDefault(x => x.Name.LocalName == "ValidationGroups");
            if (groups == null) return null;
            var group = groups.Elements().FirstOrDefault(x => x.Name.LocalName == "ValidationGroup" &&
                string.Equals(ChildValue(x, "Name"), "ValidationGroupForEvent", StringComparison.OrdinalIgnoreCase));
            return group == null ? null : (string)group.Attribute("ID");
        }

        private static string SerializeSteps(GuidedJourneyDefinition journey, string currentCode)
        {
            var items = journey.Steps.Select(x => new
            {
                value = x.Code,
                display = x.Label,
                isDefault = string.Equals(x.Code, currentCode, StringComparison.OrdinalIgnoreCase)
            }).ToArray();
            return new JavaScriptSerializer().Serialize(items);
        }

        private static XElement BuildRow(XNamespace ns, string rowId, string cellId, string controlId)
        {
            return new XElement(ns + "Row", new XAttribute("ID", rowId),
                new XElement(ns + "Cells", BuildCell(ns, cellId, controlId)));
        }

        private static XElement BuildCell(XNamespace ns, string cellId, string controlId)
        {
            var cell = new XElement(ns + "Cell", new XAttribute("ID", cellId));
            if (!string.IsNullOrWhiteSpace(controlId)) cell.Add(new XElement(ns + "Control", new XAttribute("ID", controlId)));
            return cell;
        }

        private static XElement Control(XNamespace ns, string id, string type, string name, params XElement[] properties)
        {
            var values = new XElement(ns + "Properties", Property(ns, "ControlName", name));
            foreach (var property in properties) values.Add(property);
            return new XElement(ns + "Control", new XAttribute("ID", id), new XAttribute("Type", type),
                new XElement(ns + "Name", name), new XElement(ns + "DisplayName", name), values);
        }

        private static XElement Property(XNamespace ns, string name, string value, string display = null)
        {
            return new XElement(ns + "Property",
                new XElement(ns + "Name", name),
                new XElement(ns + "DisplayValue", display ?? value),
                new XElement(ns + "NameValue", display ?? value),
                new XElement(ns + "Value", value ?? string.Empty));
        }

        private static XElement FindNamedControl(XElement controls, string name, string type)
        {
            return controls.Elements().SingleOrDefault(x => x.Name.LocalName == "Control" &&
                string.Equals((string)x.Attribute("Type"), type, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
        }

        private static bool PanelContainsControl(XElement panel, string id)
        {
            return panel.Descendants().Any(x => x.Name.LocalName == "Control" &&
                string.Equals((string)x.Attribute("ID"), id, StringComparison.OrdinalIgnoreCase));
        }

        private static void AssertProperty(XElement control, string name, string expected, string formName, string owner)
        {
            var actual = ReadProperty(control, name);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new CliException("K2 Form '" + formName + "' guided journey '" + owner +
                    "' has " + name + "='" + actual + "', expected '" + expected + "'.");
        }

        private static string ReadProperty(XElement owner, string name)
        {
            return owner.Elements().Where(x => x.Name.LocalName == "Properties").SelectMany(x => x.Elements())
                .Where(x => x.Name.LocalName == "Property" &&
                    string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase))
                .Select(x => ChildValue(x, "Value")).FirstOrDefault();
        }

        private static XElement RequiredChild(XElement parent, string name)
        {
            var child = parent.Elements().FirstOrDefault(x => x.Name.LocalName == name);
            if (child == null) throw new CliException("K2 form definition is missing " + name + ".");
            return child;
        }

        private static string ChildValue(XElement parent, string name)
        {
            var child = parent.Elements().FirstOrDefault(x => x.Name.LocalName == name);
            return child == null ? null : child.Value;
        }

        private static XDocument Parse(string xml)
        {
            try { return XDocument.Parse(xml, LoadOptions.PreserveWhitespace); }
            catch (Exception ex) { throw new CliException("K2 form definition is invalid XML: " + ex.Message); }
        }

        private static XElement FindForm(XDocument document)
        {
            var forms = document.Descendants().Where(x => x.Name.LocalName == "Form" && x.Attribute("ID") != null).ToList();
            if (forms.Count != 1) throw new CliException("K2 form definition must contain exactly one Form element; found " + forms.Count + ".");
            return forms[0];
        }

        private static string ProgressName(int index) { return "prgJourneyStep" + (index + 1); }
        private static string JourneyTitleName(int index) { return "lblJourneyTitle" + (index + 1); }
        private static string JourneyDescriptionName(int index) { return "dlbJourneyDescription" + (index + 1); }
        private static string HeadingName(int index) { return "lblJourneyStepHeading" + (index + 1); }
        private static string DescriptionName(int index) { return "dlbJourneyStepDescription" + (index + 1); }
        private static string BackName(int index) { return "btnJourneyBack" + (index + 1); }
        private static string ContinueName(int index) { return "btnJourneyContinue" + (index + 1); }
        private static string StepHeading(GuidedJourneyDefinition journey, GuidedJourneyStepDefinition step, int index)
        {
            return string.IsNullOrWhiteSpace(step.Title)
                ? "Step " + (index + 1) + " of " + journey.Steps.Count + ": " + step.Label
                : step.Title;
        }
        private static string NewId() { return Guid.NewGuid().ToString(); }
    }
}
