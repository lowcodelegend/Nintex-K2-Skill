using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class FormLayoutDefinition
    {
        public static string Apply(string xml, FormDefinition definition, ResolvedCommonHeader header, IDictionary<string, string> headerParameters)
        {
            var document = Parse(xml);
            var form = FindForm(document);
            ApplyViewTitles(form, definition);
            XElement headerArea = null;
            if (header != null)
            {
                headerArea = FindHeaderArea(form, header, definition.Name);
                ApplyHeader(form, header, headerParameters);
            }
            if (definition.Tabs.Count == 0) return document.ToString(SaveOptions.DisableFormatting);
            form.SetAttributeValue("Layout", "TabControl");
            var controls = RequiredChild(form, "Controls");
            var panels = RequiredChild(form, "Panels");
            var originalPanels = panels.Elements().Where(x => x.Name.LocalName == "Panel").ToList();

            var viewAreas = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var viewName in definition.Views)
            {
                var item = originalPanels.SelectMany(x => x.Descendants())
                    .FirstOrDefault(x => x.Name.LocalName == "Item" && string.Equals((string)x.Attribute("ViewName"), viewName, StringComparison.OrdinalIgnoreCase));
                if (item == null) throw new CliException("Generated form '" + definition.Name + "' has no layout item for view '" + viewName + "'.");
                var area = item.Ancestors().FirstOrDefault(x => x.Name.LocalName == "Area");
                if (area == null) throw new CliException("Generated form '" + definition.Name + "' view '" + viewName + "' has no containing area.");
                viewAreas[viewName] = new XElement(area);
            }

            foreach (var oldPanelControl in controls.Elements().Where(x => x.Name.LocalName == "Control" && string.Equals((string)x.Attribute("Type"), "Panel", StringComparison.OrdinalIgnoreCase)).ToList())
                oldPanelControl.Remove();
            panels.RemoveNodes();

            foreach (var tab in definition.Tabs)
            {
                var panelId = NewId();
                controls.Add(BuildPanelControl(controls.Name.Namespace, panelId, tab.Name));
                var panel = new XElement(panels.Name.Namespace + "Panel",
                    new XAttribute("ID", panelId),
                    new XAttribute("DisplayName", tab.Name),
                    new XAttribute("Layout", "Rows"),
                    new XElement(panels.Name.Namespace + "Name", tab.Name),
                    new XElement(panels.Name.Namespace + "DisplayName", tab.Name));
                var areas = new XElement(panels.Name.Namespace + "Areas");
                panel.Add(areas);
                if (headerArea != null && object.ReferenceEquals(tab, definition.Tabs[0])) areas.Add(new XElement(headerArea));
                foreach (var viewName in tab.Views) areas.Add(new XElement(viewAreas[viewName]));
                if (tab.Worklist != null) areas.Add(BuildWorklistArea(form, controls, tab, definition.Name));
                panels.Add(panel);
            }
            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static void Verify(string xml, FormDefinition definition, ResolvedCommonHeader header, IDictionary<string, string> headerParameters)
        {
            var document = Parse(xml);
            var form = FindForm(document);
            VerifyViewTitles(form, definition);
            if (header != null) VerifyHeader(form, header, headerParameters, definition.Name);
            if (definition.Tabs.Count == 0) return;
            if (!string.Equals((string)form.Attribute("Layout"), "TabControl", StringComparison.OrdinalIgnoreCase))
                throw new CliException("K2 Form '" + definition.Name + "' has multiple tabs but Layout is '" + (string)form.Attribute("Layout") + "', expected 'TabControl'.");
            var controls = RequiredChild(form, "Controls");
            var panels = RequiredChild(form, "Panels").Elements().Where(x => x.Name.LocalName == "Panel").ToList();
            if (panels.Count != definition.Tabs.Count)
                throw new CliException("K2 Form '" + definition.Name + "' has " + panels.Count + " tabs, expected " + definition.Tabs.Count + ".");

            for (var index = 0; index < definition.Tabs.Count; index++)
            {
                var expected = definition.Tabs[index];
                var panel = panels[index];
                if (!string.Equals(ChildValue(panel, "Name"), expected.Name, StringComparison.Ordinal))
                    throw new CliException("K2 Form '" + definition.Name + "' tab " + (index + 1) + " is '" + ChildValue(panel, "Name") + "', expected '" + expected.Name + "'.");
                var actualViews = panel.Descendants().Where(x => x.Name.LocalName == "Item" && x.Attribute("ViewID") != null)
                    .Where(x => header == null || !string.Equals((string)x.Attribute("ViewID"), header.ViewGuid.ToString(), StringComparison.OrdinalIgnoreCase))
                    .Select(x => (string)x.Attribute("ViewName") ?? ChildValue(x, "Name")).ToList();
                if (!actualViews.SequenceEqual(expected.Views, StringComparer.OrdinalIgnoreCase))
                    throw new CliException("K2 Form '" + definition.Name + "' tab '" + expected.Name + "' has the wrong view layout.");
                var headerCount = panel.Descendants().Count(x => x.Name.LocalName == "Item" && header != null && string.Equals((string)x.Attribute("ViewID"), header.ViewGuid.ToString(), StringComparison.OrdinalIgnoreCase));
                if (header != null && headerCount != (index == 0 ? 1 : 0))
                    throw new CliException("K2 Form '" + definition.Name + "' common header must occur once on the first tab only.");
                if (expected.Worklist != null) VerifyWorklist(form, controls, panel, definition, expected);
            }
        }

        private static XElement FindHeaderArea(XElement form, ResolvedCommonHeader header, string formName)
        {
            var item = form.Descendants().FirstOrDefault(x => x.Name.LocalName == "Item" &&
                string.Equals((string)x.Attribute("ViewID"), header.ViewGuid.ToString(), StringComparison.OrdinalIgnoreCase));
            if (item == null) throw new CliException("Generated form '" + formName + "' has no layout item for common header '" + header.ViewName + "'.");
            var area = item.Ancestors().FirstOrDefault(x => x.Name.LocalName == "Area");
            if (area == null) throw new CliException("Generated form '" + formName + "' common header has no containing area.");
            return area;
        }

        private static void ApplyHeader(XElement form, ResolvedCommonHeader header, IDictionary<string, string> parameters)
        {
            var item = form.Descendants().First(x => x.Name.LocalName == "Item" && string.Equals((string)x.Attribute("ViewID"), header.ViewGuid.ToString(), StringComparison.OrdinalIgnoreCase));
            var instanceId = (string)item.Attribute("ID");
            var controls = RequiredChild(form, "Controls");
            var control = controls.Elements().FirstOrDefault(x => x.Name.LocalName == "Control" && string.Equals((string)x.Attribute("ID"), instanceId, StringComparison.OrdinalIgnoreCase));
            if (control == null) throw new CliException("Generated form has no AreaItem control for common header '" + header.ViewName + "'.");
            SetProperty(control, "Title", header.Title ?? string.Empty);
            if (parameters == null || parameters.Count == 0) return;
            if (string.IsNullOrWhiteSpace(header.InitializeEvent))
                throw new CliException("Common header '" + header.ViewName + "' has parameter bindings but no initialize event was configured.");
            var action = form.Descendants().FirstOrDefault(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("InstanceID"), instanceId, StringComparison.OrdinalIgnoreCase) &&
                x.Descendants().Any(p => p.Name.LocalName == "Property" &&
                    ((string.Equals(ChildValue(p, "Name"), "EventID", StringComparison.OrdinalIgnoreCase) && header.InitializeEventDefinitionId != Guid.Empty && string.Equals(ChildValue(p, "Value"), header.InitializeEventDefinitionId.ToString(), StringComparison.OrdinalIgnoreCase)) ||
                     (string.Equals(ChildValue(p, "Name"), "Method", StringComparison.OrdinalIgnoreCase) && string.Equals(ChildValue(p, "Value"), header.InitializeEvent, StringComparison.OrdinalIgnoreCase)))));
            if (action == null)
            {
                var available = form.Descendants().Where(x => x.Name.LocalName == "Action" && string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase))
                    .Select(x => "instance=" + ((string)x.Attribute("InstanceID") ?? "") + ", event=" + (x.Descendants().Where(p => p.Name.LocalName == "Property" && string.Equals(ChildValue(p, "Name"), "EventID", StringComparison.OrdinalIgnoreCase)).Select(p => ChildValue(p, "Value")).FirstOrDefault() ?? "") + ", method=" + (x.Descendants().Where(p => p.Name.LocalName == "Property" && string.Equals(ChildValue(p, "Name"), "Method", StringComparison.OrdinalIgnoreCase)).Select(p => ChildValue(p, "Value")).FirstOrDefault() ?? "")).ToArray();
                throw new CliException("Generated form has no invocation of common header initialize event '" + header.InitializeEvent + "'. Execute actions: " + string.Join("; ", available));
            }
            var actionParameters = action.Elements().FirstOrDefault(x => x.Name.LocalName == "Parameters");
            if (actionParameters == null) { actionParameters = new XElement(action.Name.Namespace + "Parameters"); action.Add(actionParameters); }
            foreach (var binding in parameters)
            {
                foreach (var duplicate in actionParameters.Elements().Where(x => x.Name.LocalName == "Parameter" && string.Equals((string)x.Attribute("TargetID"), binding.Key, StringComparison.OrdinalIgnoreCase)).ToList()) duplicate.Remove();
                actionParameters.Add(new XElement(action.Name.Namespace + "Parameter",
                    new XAttribute("SourceID", "Sources"), new XAttribute("SourceType", "Value"),
                    new XAttribute("TargetInstanceID", instanceId), new XAttribute("TargetID", binding.Key),
                    new XAttribute("TargetName", binding.Key), new XAttribute("TargetType", "ViewParameter"), new XAttribute("IsRequired", "True"),
                    new XElement(action.Name.Namespace + "SourceValue", new XAttribute(XNamespace.Xml + "space", "preserve"), binding.Value ?? string.Empty)));
            }
        }

        private static void VerifyHeader(XElement form, ResolvedCommonHeader header, IDictionary<string, string> parameters, string formName)
        {
            var item = form.Descendants().FirstOrDefault(x => x.Name.LocalName == "Item" && string.Equals((string)x.Attribute("ViewID"), header.ViewGuid.ToString(), StringComparison.OrdinalIgnoreCase));
            if (item == null) throw new CliException("K2 Form '" + formName + "' is missing common header '" + header.ViewName + "'.");
            var instanceId = (string)item.Attribute("ID");
            var controls = RequiredChild(form, "Controls");
            var control = controls.Elements().FirstOrDefault(x => x.Name.LocalName == "Control" && string.Equals((string)x.Attribute("ID"), instanceId, StringComparison.OrdinalIgnoreCase));
            var title = control == null ? null : ReadProperty(control, "Title") ?? string.Empty;
            if (!string.Equals(title, header.Title ?? string.Empty, StringComparison.Ordinal))
                throw new CliException("K2 Form '" + formName + "' common header title does not match the environment configuration.");
            if (parameters == null || parameters.Count == 0) return;
            var action = form.Descendants().FirstOrDefault(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("InstanceID"), instanceId, StringComparison.OrdinalIgnoreCase) &&
                x.Descendants().Any(p => p.Name.LocalName == "Property" &&
                    ((string.Equals(ChildValue(p, "Name"), "EventID", StringComparison.OrdinalIgnoreCase) && header.InitializeEventDefinitionId != Guid.Empty && string.Equals(ChildValue(p, "Value"), header.InitializeEventDefinitionId.ToString(), StringComparison.OrdinalIgnoreCase)) ||
                     (string.Equals(ChildValue(p, "Name"), "Method", StringComparison.OrdinalIgnoreCase) && string.Equals(ChildValue(p, "Value"), header.InitializeEvent, StringComparison.OrdinalIgnoreCase)))));
            if (action == null) throw new CliException("K2 Form '" + formName + "' does not invoke common header initialize event '" + header.InitializeEvent + "'.");
            foreach (var binding in parameters)
            {
                var parameter = action.Descendants().FirstOrDefault(x => x.Name.LocalName == "Parameter" && string.Equals((string)x.Attribute("TargetID"), binding.Key, StringComparison.OrdinalIgnoreCase));
                var actual = parameter == null ? null : parameter.Elements().FirstOrDefault(x => x.Name.LocalName == "SourceValue");
                if (actual == null || !string.Equals(actual.Value, binding.Value ?? string.Empty, StringComparison.Ordinal))
                    throw new CliException("K2 Form '" + formName + "' common header parameter '" + binding.Key + "' is not configured as expected.");
            }
        }

        private static void ApplyViewTitles(XElement form, FormDefinition definition)
        {
            var controls = RequiredChild(form, "Controls");
            foreach (var viewName in definition.Views)
            {
                var item = form.Descendants().FirstOrDefault(x => x.Name.LocalName == "Item" && string.Equals((string)x.Attribute("ViewName"), viewName, StringComparison.OrdinalIgnoreCase));
                if (item == null) throw new CliException("Generated form '" + definition.Name + "' has no layout item for view '" + viewName + "'.");
                var id = (string)item.Attribute("ID");
                var control = controls.Elements().FirstOrDefault(x => x.Name.LocalName == "Control" && string.Equals((string)x.Attribute("ID"), id, StringComparison.OrdinalIgnoreCase));
                if (control == null) throw new CliException("Generated form '" + definition.Name + "' has no AreaItem control for view '" + viewName + "'.");
                SetProperty(control, "Title", definition.ResolveViewTitle(viewName));
            }
        }

        private static void VerifyViewTitles(XElement form, FormDefinition definition)
        {
            var controls = RequiredChild(form, "Controls");
            foreach (var viewName in definition.Views)
            {
                var item = form.Descendants().FirstOrDefault(x => x.Name.LocalName == "Item" && string.Equals((string)x.Attribute("ViewName"), viewName, StringComparison.OrdinalIgnoreCase));
                if (item == null) throw new CliException("K2 Form '" + definition.Name + "' has no layout item for view '" + viewName + "'.");
                var id = (string)item.Attribute("ID");
                var control = controls.Elements().FirstOrDefault(x => x.Name.LocalName == "Control" && string.Equals((string)x.Attribute("ID"), id, StringComparison.OrdinalIgnoreCase));
                if (control == null) throw new CliException("K2 Form '" + definition.Name + "' has no AreaItem control for view '" + viewName + "'.");
                var expected = definition.ResolveViewTitle(viewName);
                var actual = ReadProperty(control, "Title") ?? string.Empty;
                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                    throw new CliException("K2 Form '" + definition.Name + "' view '" + viewName + "' has title '" + actual + "', expected '" + expected + "'.");
            }
        }

        private static XElement BuildPanelControl(XNamespace ns, string id, string name)
        {
            return new XElement(ns + "Control",
                new XAttribute("ID", id), new XAttribute("Type", "Panel"),
                new XElement(ns + "Name", name), new XElement(ns + "DisplayName", name),
                new XElement(ns + "Properties",
                    Property(ns, "ControlName", name, true),
                    Property(ns, "Title", name, true)));
        }

        private static XElement BuildWorklistArea(XElement form, XElement controls, FormTabDefinition tab, string formName)
        {
            var ns = form.Name.Namespace;
            var worklist = tab.Worklist;
            var controlId = NewId();
            var areaId = NewId();
            var itemId = NewId();
            var controlName = tab.Name + " Worklist";

            controls.Add(new XElement(ns + "Control",
                new XAttribute("ID", controlId), new XAttribute("Type", "Worklist"),
                new XElement(ns + "Name", controlName), new XElement(ns + "DisplayName", controlName),
                new XElement(ns + "Properties",
                    Property(ns, "ControlName", controlName, true),
                    Property(ns, "Text", controlName, true),
                    Property(ns, "FilterGrouping", "(Default)", true),
                    Property(ns, "RefreshInterval", worklist.RefreshIntervalSeconds.ToString(), true),
                    Property(ns, "showToolBar", Bool(worklist.ShowToolbar), true),
                    Property(ns, "showFilter", Bool(worklist.ShowFilter), true),
                    Property(ns, "showSearch", Bool(worklist.ShowSearch), true),
                    Property(ns, "enableSearch", Bool(worklist.EnableSearch), true),
                    Property(ns, "Width", "100%", true),
                    Property(ns, "Rows", worklist.Rows.ToString(), true),
                    Property(ns, "WatermarkText", "No tasks to display", true),
                    Property(ns, "Height", worklist.Height, true),
                    Property(ns, "ViewType", "Grid", false),
                    Property(ns, "ColumnLayoutConfiguration", "Grid", false),
                    Property(ns, "ColumnLayoutConfigurationValueHolder", "[{\"key\":\"Folio\",\"value\":\"Folio\",\"width\":null},{\"key\":\"EventStartDate\",\"value\":\"Task Start Date\",\"width\":null},{\"key\":\"ProcessName\",\"value\":\"Workflow Name\",\"width\":null}]", false),
                    Property(ns, "ActionMenuItemData", string.Join(",", worklist.Actions.ToArray()), false))));
            controls.Add(BasicControl(ns, areaId, "Area", tab.Name + " Area"));
            controls.Add(BasicControl(ns, itemId, "AreaItem", tab.Name + " Area Item"));
            AddWorklistClickRule(form, formName, tab.Name, controlId, controlName, worklist.OpenTaskInNewWindow);

            return new XElement(ns + "Area", new XAttribute("ID", areaId),
                new XElement(ns + "Items",
                    new XElement(ns + "Item", new XAttribute("ID", itemId),
                        new XElement(ns + "Canvas", new XElement(ns + "Control", new XAttribute("ID", controlId))))));
        }

        private static XElement BasicControl(XNamespace ns, string id, string type, string name)
        {
            return new XElement(ns + "Control", new XAttribute("ID", id), new XAttribute("Type", type),
                new XElement(ns + "Name", name), new XElement(ns + "DisplayName", name),
                new XElement(ns + "Properties", Property(ns, "ControlName", name, true)));
        }

        private static void AddWorklistClickRule(XElement form, string formName, string tabName, string controlId, string controlName, bool newWindow)
        {
            var ns = form.Name.Namespace;
            var state = RequiredChild(RequiredChild(form, "States"), "State");
            var events = state.Elements().FirstOrDefault(x => x.Name.LocalName == "Events");
            if (events == null)
            {
                events = new XElement(ns + "Events");
                state.Add(events);
            }
            events.Add(new XElement(ns + "Event",
                new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "User"), new XAttribute("SourceID", controlId), new XAttribute("SourceType", "Control"), new XAttribute("SourceName", controlName),
                new XElement(ns + "Name", "Clicked"),
                new XElement(ns + "Properties", Property(ns, "RuleFriendlyName", "When " + controlName + " is Clicked", false), Property(ns, "Location", formName + " / " + tabName, false)),
                new XElement(ns + "Handlers",
                    new XElement(ns + "Handler", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                        new XElement(ns + "Properties", Property(ns, "HandlerName", "IfLogicalHandler", false), Property(ns, "Location", "form", false)),
                        new XElement(ns + "Actions",
                            new XElement(ns + "Action", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()), new XAttribute("Type", "Navigate"), new XAttribute("ExecutionType", "Synchronous"),
                                new XElement(ns + "Properties", Property(ns, "Url", "baseURL", false), Property(ns, "Target", newWindow ? "_blank" : "_self", false), Property(ns, "Location", "Form", false)),
                                new XElement(ns + "Parameters", BuildNavigateParameter(ns, controlId, controlName, "BaseURL", true), BuildNavigateParameter(ns, controlId, controlName, "BrowserNavigateDialogResizable", false), BuildNavigateParameter(ns, controlId, controlName, "BrowserNavigateDialogCenter", false), BuildNavigateParameter(ns, controlId, controlName, "BrowserNavigateDialogStatus", false))))))));
        }

        private static XElement BuildNavigateParameter(XNamespace ns, string controlId, string controlName, string target, bool url)
        {
            XElement source;
            if (url)
            {
                source = new XElement(ns + "Source", new XAttribute("SourceID", "data"), new XAttribute("SourceName", "data"),
                    new XAttribute("SourceDisplayName", "Worklist item URL"), new XAttribute("SourceType", "ControlProperty"),
                    new XAttribute("SourcePath", controlId), new XAttribute("SourcePathName", controlName),
                    new XAttribute("ValidationStatus", "Auto"), new XAttribute("ValidationMessages", "PropertyExpressionSource,ControlTypeProperty,Auto,,data,Worklist item URL"));
            }
            else source = new XElement(ns + "Source", new XAttribute("SourceType", "Value"), "yes");
            return new XElement(ns + "Parameter", new XAttribute("SourceID", "Sources"), new XAttribute("SourceType", "Value"),
                new XAttribute("TargetID", target), new XAttribute("TargetName", target), new XAttribute("TargetType", "Value"),
                new XElement(ns + "SourceValue", new XAttribute(XNamespace.Xml + "space", "preserve"), source));
        }

        private static void VerifyWorklist(XElement form, XElement controls, XElement panel, FormDefinition formDefinition, FormTabDefinition tab)
        {
            var reference = panel.Descendants().FirstOrDefault(x => x.Name.LocalName == "Control" && x.Attribute("ID") != null);
            if (reference == null) throw new CliException("K2 Form '" + formDefinition.Name + "' tab '" + tab.Name + "' has no Worklist control reference.");
            var controlId = (string)reference.Attribute("ID");
            var control = controls.Elements().FirstOrDefault(x => x.Name.LocalName == "Control" && string.Equals((string)x.Attribute("ID"), controlId, StringComparison.OrdinalIgnoreCase));
            if (control == null || !string.Equals((string)control.Attribute("Type"), "Worklist", StringComparison.OrdinalIgnoreCase))
                throw new CliException("K2 Form '" + formDefinition.Name + "' tab '" + tab.Name + "' does not contain a native Worklist control.");
            AssertProperty(control, "Rows", tab.Worklist.Rows.ToString(), formDefinition, tab);
            AssertProperty(control, "RefreshInterval", tab.Worklist.RefreshIntervalSeconds.ToString(), formDefinition, tab);
            AssertProperty(control, "showToolBar", Bool(tab.Worklist.ShowToolbar), formDefinition, tab);
            AssertProperty(control, "showFilter", Bool(tab.Worklist.ShowFilter), formDefinition, tab);
            AssertProperty(control, "showSearch", Bool(tab.Worklist.ShowSearch), formDefinition, tab);
            AssertProperty(control, "enableSearch", Bool(tab.Worklist.EnableSearch), formDefinition, tab);
            AssertProperty(control, "Height", tab.Worklist.Height, formDefinition, tab);
            AssertProperty(control, "ActionMenuItemData", string.Join(",", tab.Worklist.Actions.ToArray()), formDefinition, tab);
            var navigate = form.Descendants().FirstOrDefault(x => x.Name.LocalName == "Event" && string.Equals((string)x.Attribute("SourceID"), controlId, StringComparison.OrdinalIgnoreCase) && x.Descendants().Any(a => a.Name.LocalName == "Action" && string.Equals((string)a.Attribute("Type"), "Navigate", StringComparison.OrdinalIgnoreCase)));
            if (navigate == null || !navigate.Descendants().Any(x => x.Name.LocalName == "Source" && string.Equals((string)x.Attribute("SourcePath"), controlId, StringComparison.OrdinalIgnoreCase)))
                throw new CliException("K2 Form '" + formDefinition.Name + "' Worklist tab '" + tab.Name + "' has no click-to-open-task rule.");
        }

        private static XElement Property(XNamespace ns, string name, string value, bool includeDisplayValues)
        {
            var property = new XElement(ns + "Property", new XElement(ns + "Name", name));
            if (includeDisplayValues)
            {
                property.Add(new XElement(ns + "DisplayValue", value));
                property.Add(new XElement(ns + "NameValue", value));
            }
            property.Add(new XElement(ns + "Value", value));
            return property;
        }

        private static void SetProperty(XElement control, string name, string value)
        {
            var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
            if (properties == null)
            {
                properties = new XElement(control.Name.Namespace + "Properties");
                control.Add(properties);
            }
            var property = properties.Elements().FirstOrDefault(x => x.Name.LocalName == "Property" && string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
            if (property == null)
            {
                properties.Add(Property(control.Name.Namespace, name, value, true));
                return;
            }
            SetChildValue(property, "DisplayValue", value);
            SetChildValue(property, "NameValue", value);
            SetChildValue(property, "Value", value);
        }

        private static string ReadProperty(XElement control, string name)
        {
            return control.Descendants().Where(x => x.Name.LocalName == "Property")
                .Where(x => string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase))
                .Select(x => ChildValue(x, "Value")).FirstOrDefault();
        }

        private static void SetChildValue(XElement parent, string name, string value)
        {
            var child = parent.Elements().FirstOrDefault(x => x.Name.LocalName == name);
            if (child == null)
            {
                child = new XElement(parent.Name.Namespace + name);
                parent.Add(child);
            }
            child.Value = value ?? string.Empty;
        }

        private static void AssertProperty(XElement control, string name, string expected, FormDefinition form, FormTabDefinition tab)
        {
            var actual = control.Descendants().Where(x => x.Name.LocalName == "Property")
                .Where(x => string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase))
                .Select(x => ChildValue(x, "Value")).FirstOrDefault();
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new CliException("K2 Form '" + form.Name + "' Worklist tab '" + tab.Name + "' has " + name + "='" + actual + "', expected '" + expected + "'.");
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

        private static string Bool(bool value) { return value ? "true" : "false"; }
        private static string NewId() { return Guid.NewGuid().ToString(); }
    }
}
