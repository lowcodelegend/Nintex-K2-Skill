using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class ViewWebComponentLayoutDefinition
    {
        public static string Apply(string definition, ViewDefinition view)
        {
            if (view.WebComponents == null || view.WebComponents.Count == 0) return definition;
            var component = view.WebComponents.Single();
            var document = XDocument.Parse(definition, LoadOptions.PreserveWhitespace);
            var root = document.Descendants().Single(element => element.Name.LocalName == "View");
            var ns = root.Name.Namespace;
            var controls = root.Elements().Single(element => element.Name.LocalName == "Controls");
            var canvas = root.Elements().Single(element => element.Name.LocalName == "Canvas");
            var sections = canvas.Elements().Single(element => element.Name.LocalName == "Sections");
            var body = sections.Elements().SingleOrDefault(element =>
                element.Name.LocalName == "Section" &&
                string.Equals((string)element.Attribute("Type"), "Body", StringComparison.OrdinalIgnoreCase));
            if (body == null) throw new CliException("View '" + view.Name + "' has no body section for Web Component placement.");
            foreach (var section in sections.Elements().Where(element => element != body).ToList()) section.Remove();

            var tableRef = body.Elements().Single(element => element.Name.LocalName == "Control");
            var tableId = (string)tableRef.Attribute("ID");
            controls.Elements().Single(element => element.Name.LocalName == "Control" &&
                string.Equals((string)element.Attribute("ID"), tableId, StringComparison.OrdinalIgnoreCase));

            var columnId = NewId();
            var rowId = NewId();
            var cellId = NewId();
            var controlId = NewId();
            tableRef.ReplaceNodes(
                new XElement(ns + "Columns", new XElement(ns + "Column", new XAttribute("ID", columnId), new XAttribute("Size", "100%"))),
                new XElement(ns + "Rows", new XElement(ns + "Row", new XAttribute("ID", rowId),
                    new XElement(ns + "Cells", new XElement(ns + "Cell", new XAttribute("ID", cellId),
                        new XElement(ns + "Control", new XAttribute("ID", controlId)))))));

            controls.Add(LayoutControl(ns, columnId, component.Name + " Column", "Column"));
            controls.Add(LayoutControl(ns, rowId, component.Name + " Row", "Row"));
            controls.Add(LayoutControl(ns, cellId, component.Name + " Cell", "Cell"));
            var componentControl = ComponentControl(ns, controlId, component);
            controls.Add(componentControl);

            if (component.DataBinding != null)
                ConfigureDataBinding(root, componentControl, component, view);
            foreach (var ruleEvent in component.Events)
                ConfigureRuleEvent(root, componentControl, component, ruleEvent);

            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static void Verify(string definition, ViewDefinition view)
        {
            if (view.WebComponents == null || view.WebComponents.Count == 0) return;
            var component = view.WebComponents.Single();
            var document = XDocument.Parse(definition);
            var root = document.Descendants().Single(element => element.Name.LocalName == "View");
            var controls = document.Descendants().Where(element => element.Name.LocalName == "Control" &&
                string.Equals((string)element.Attribute("Type"), component.ControlType, StringComparison.OrdinalIgnoreCase)).ToList();
            if (controls.Count != 1)
                throw new CliException("View '" + view.Name + "' must contain exactly one '" + component.ControlType + "' Web Component.");
            var control = controls[0];
            var id = (string)control.Attribute("ID");
            var body = document.Descendants().Single(element => element.Name.LocalName == "Section" &&
                string.Equals((string)element.Attribute("Type"), "Body", StringComparison.OrdinalIgnoreCase));
            if (body.Descendants().Count(element => element.Name.LocalName == "Control" &&
                string.Equals((string)element.Attribute("ID"), id, StringComparison.OrdinalIgnoreCase)) != 1)
                throw new CliException("View '" + view.Name + "' Web Component is not placed once in its body.");
            foreach (var property in component.Properties)
            {
                var actual = ControlProperty(control, property.Key);
                var actualValue = actual == null ? null : Child(actual, "Value") ?? string.Empty;
                if (actual == null || !string.Equals(actualValue, property.Value ?? string.Empty, StringComparison.Ordinal))
                    throw new CliException("View '" + view.Name + "' Web Component property is missing or mismatched: " + property.Key);
            }

            if (component.DataBinding != null)
                VerifyDataBinding(root, control, component, view);
            foreach (var ruleEvent in component.Events)
                VerifyRuleEvent(root, control, ruleEvent, view);
        }

        private static void ConfigureDataBinding(XElement root, XElement control,
            ViewWebComponentDefinition component, ViewDefinition view)
        {
            var ns = root.Name.Namespace;
            var sources = EnsureChild(root, "Sources");
            var primary = sources.Elements().FirstOrDefault(x => x.Name.LocalName == "Source" &&
                string.Equals((string)x.Attribute("ContextType"), "Primary", StringComparison.OrdinalIgnoreCase));
            if (primary == null)
                throw new CliException("Generated View '" + view.Name + "' has no primary SmartObject source for Web Component binding.");

            var controlId = (string)control.Attribute("ID");
            foreach (var existing in sources.Elements().Where(x => x.Name.LocalName == "Source" &&
                string.Equals((string)x.Attribute("ContextType"), "Association", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("ContextID"), controlId, StringComparison.OrdinalIgnoreCase)).ToList())
                existing.Remove();

            var association = CloneWithFreshIds(primary);
            association.SetAttributeValue("ID", NewId());
            association.SetAttributeValue("ContextType", "Association");
            association.SetAttributeValue("ContextID", controlId);
            sources.Add(association);

            var sourceId = (string)primary.Attribute("SourceID");
            var sourceName = (string)primary.Attribute("SourceName") ?? view.SmartObject;
            var sourceDisplayName = (string)primary.Attribute("SourceDisplayName") ?? sourceName;
            var properties = EnsureChild(control, "Properties");
            SetProperty(properties, "DataSourceType", "SmartObject");
            SetProperty(properties, "AssociationSO", sourceId, sourceDisplayName, sourceName);
            SetProperty(properties, "AssociationMethod", component.DataBinding.Method);
            SetProperty(properties, "ListDataProperty", component.DataBinding.Property);

            var events = EnsureChild(root, "Events");
            foreach (var existing in events.Elements().Where(x => x.Name.LocalName == "Event" &&
                string.Equals((string)x.Attribute("SourceID"), controlId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Child(x, "Name"), "Initializing", StringComparison.OrdinalIgnoreCase)).ToList())
                existing.Remove();

            var template = FindListAction(root, component.DataBinding.Method) ??
                BuildListActionTemplate(ns, root, component.DataBinding.Method);
            if (template.Descendants().Any(x => x.Name.LocalName == "Parameter" &&
                x.Descendants().Any(source => source.Name.LocalName == "Source" &&
                    !string.Equals((string)source.Attribute("SourceType"), "Value", StringComparison.OrdinalIgnoreCase))))
                throw new CliException("View '" + view.Name + "' Web Component list method exposes client-supplied inputs. Use a parameterless method with server-side Current User FQN mapping.");

            var action = new XElement(template);
            action.SetAttributeValue("ID", NewId());
            action.SetAttributeValue("DefinitionID", NewId());
            var actionProperties = EnsureChild(action, "Properties");
            SetProperty(actionProperties, "Location", "Control");
            SetProperty(actionProperties, "ControlID", controlId, component.Name, component.Name);
            SetProperty(actionProperties, "Method", component.DataBinding.Method);
            var results = action.Elements().FirstOrDefault(x => x.Name.LocalName == "Results");
            if (results != null) results.Remove();
            action.Add(new XElement(ns + "Results",
                new XElement(ns + "Result",
                    new XAttribute("SourceID", sourceId),
                    new XAttribute("SourceName", sourceName),
                    new XAttribute("SourceDisplayName", sourceDisplayName),
                    new XAttribute("SourceType", "Result"),
                    new XAttribute("TargetID", controlId),
                    new XAttribute("TargetName", component.Name),
                    new XAttribute("TargetDisplayName", component.Name),
                    new XAttribute("TargetType", "Control"))));

            var init = EnsureViewInitEvent(root, view);
            foreach (var existing in init.Descendants().Where(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ActionProperty(x, "ControlID"), controlId, StringComparison.OrdinalIgnoreCase)).ToList())
                existing.Remove();
            var initActions = EnsureUnconditionalActions(init);
            initActions.AddFirst(action);
        }

        private static void ConfigureRuleEvent(XElement root, XElement control,
            ViewWebComponentDefinition component, ViewWebComponentEventDefinition ruleEvent)
        {
            var ns = root.Name.Namespace;
            var events = EnsureChild(root, "Events");
            var controlId = (string)control.Attribute("ID");
            foreach (var existing in events.Elements().Where(x => x.Name.LocalName == "Event" &&
                string.Equals((string)x.Attribute("SourceID"), controlId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Child(x, "Name"), ruleEvent.Name, StringComparison.OrdinalIgnoreCase)).ToList())
                existing.Remove();

            var viewId = (string)root.Attribute("ID");
            var source = new XElement(ns + "Source",
                new XAttribute("SourceID", ruleEvent.SourceProperty),
                new XAttribute("SourceName", ruleEvent.SourceProperty),
                new XAttribute("SourceDisplayName", ruleEvent.SourceProperty),
                new XAttribute("SourceType", "ControlProperty"),
                new XAttribute("SourcePath", controlId),
                new XAttribute("SourcePathName", component.Name),
                new XAttribute("ValidationStatus", "Auto"),
                new XAttribute("ValidationMessages", "PropertyExpressionSource,ControlTypeProperty,Auto,," +
                    ruleEvent.SourceProperty + "," + ruleEvent.SourceProperty));
            var navigate = new XElement(ns + "Action",
                new XAttribute("ID", NewId()),
                new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "Navigate"),
                new XAttribute("ExecutionType", "Synchronous"),
                new XElement(ns + "Properties",
                    Property(ns, "Url", "baseURL"),
                    Property(ns, "Target", ruleEvent.Target),
                    Property(ns, "Location", "View")),
                new XElement(ns + "Parameters",
                    NavigateParameter(ns, "BaseURL", source),
                    NavigateParameter(ns, "BrowserNavigateDialogResizable", ValueSource(ns, "yes")),
                    NavigateParameter(ns, "BrowserNavigateDialogCenter", ValueSource(ns, "yes")),
                    NavigateParameter(ns, "BrowserNavigateDialogStatus", ValueSource(ns, "yes"))));

            events.Add(new XElement(ns + "Event",
                new XAttribute("ID", NewId()),
                new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "User"),
                new XAttribute("SourceID", controlId),
                new XAttribute("SourceType", "Control"),
                new XAttribute("SourceName", component.Name),
                new XAttribute("SourceDisplayName", component.Name),
                new XElement(ns + "Name", ruleEvent.Name),
                new XElement(ns + "DisplayName", ruleEvent.Name),
                new XElement(ns + "Properties",
                    Property(ns, "ViewID", viewId),
                    Property(ns, "RuleFriendlyName", "When " + component.Name + " raises " + ruleEvent.Name),
                    Property(ns, "Location", viewId)),
                new XElement(ns + "Handlers",
                    new XElement(ns + "Handler",
                        new XAttribute("ID", NewId()),
                        new XAttribute("DefinitionID", NewId()),
                        new XElement(ns + "Properties",
                            Property(ns, "HandlerName", "IfLogicalHandler"),
                            Property(ns, "Location", "view")),
                        new XElement(ns + "Actions", navigate)))));
        }

        private static void VerifyDataBinding(XElement root, XElement control,
            ViewWebComponentDefinition component, ViewDefinition view)
        {
            var controlId = (string)control.Attribute("ID");
            var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
            AssertProperty(properties, "DataSourceType", "SmartObject", view);
            AssertProperty(properties, "AssociationMethod", component.DataBinding.Method, view);
            AssertProperty(properties, "ListDataProperty", component.DataBinding.Property, view);

            var sources = root.Elements().FirstOrDefault(x => x.Name.LocalName == "Sources");
            var associations = sources == null ? new List<XElement>() : sources.Elements().Where(x =>
                x.Name.LocalName == "Source" &&
                string.Equals((string)x.Attribute("ContextType"), "Association", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("ContextID"), controlId, StringComparison.OrdinalIgnoreCase)).ToList();
            if (associations.Count != 1)
                throw new CliException("View '" + view.Name + "' Web Component must have exactly one SmartObject association source.");

            var events = root.Elements().FirstOrDefault(x => x.Name.LocalName == "Events");
            var initializing = events == null ? new List<XElement>() : events.Elements().Where(x =>
                x.Name.LocalName == "Event" &&
                string.Equals((string)x.Attribute("SourceID"), controlId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Child(x, "Name"), "Initializing", StringComparison.OrdinalIgnoreCase)).ToList();
            if (initializing.Count != 0)
                throw new CliException("View '" + view.Name + "' Web Component must not depend on a synthetic control Initializing event.");
            var init = events == null ? null : events.Elements().FirstOrDefault(x =>
                x.Name.LocalName == "Event" &&
                string.Equals((string)x.Attribute("SourceType"), "View", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Child(x, "Name"), "Init", StringComparison.OrdinalIgnoreCase));
            var actions = init == null ? new List<XElement>() : init.Descendants().Where(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ActionProperty(x, "Method"), component.DataBinding.Method, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ActionProperty(x, "ControlID"), controlId, StringComparison.OrdinalIgnoreCase)).ToList();
            if (actions.Count != 1 || actions[0].Descendants().Count(x => x.Name.LocalName == "Result" &&
                string.Equals((string)x.Attribute("TargetID"), controlId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("TargetType"), "Control", StringComparison.OrdinalIgnoreCase)) != 1)
                throw new CliException("View '" + view.Name + "' Web Component View Init data rule does not map one SmartObject result to the control.");
        }

        private static XElement EnsureViewInitEvent(XElement root, ViewDefinition view)
        {
            var events = EnsureChild(root, "Events");
            var existing = events.Elements().FirstOrDefault(x =>
                x.Name.LocalName == "Event" &&
                string.Equals(Child(x, "Name"), "Init", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace((string)x.Attribute("SourceType")) ||
                 string.Equals((string)x.Attribute("SourceType"), "View", StringComparison.OrdinalIgnoreCase)));
            if (existing != null) return existing;
            var ns = root.Name.Namespace;
            var viewId = (string)root.Attribute("ID");
            if (string.IsNullOrWhiteSpace(viewId))
                throw new CliException("Generated View '" + view.Name + "' has no View ID for Web Component data initialization.");
            var result = new XElement(ns + "Event",
                new XAttribute("ID", NewId()),
                new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "User"),
                new XAttribute("SourceID", viewId),
                new XAttribute("SourceType", "View"),
                new XAttribute("SourceName", view.Name),
                new XAttribute("SourceDisplayName", view.Name),
                new XElement(ns + "Name", "Init"),
                new XElement(ns + "DisplayName", "Init"),
                new XElement(ns + "Handlers"));
            events.AddFirst(result);
            return result;
        }

        private static XElement EnsureUnconditionalActions(XElement init)
        {
            var ns = init.Name.Namespace;
            var handlers = EnsureChild(init, "Handlers");
            var handler = handlers.Elements().FirstOrDefault(x =>
                x.Name.LocalName == "Handler" &&
                !x.Elements().Any(child => child.Name.LocalName == "Conditions" && child.Elements().Any()));
            if (handler == null)
            {
                handler = new XElement(ns + "Handler",
                    new XAttribute("ID", NewId()),
                    new XAttribute("DefinitionID", NewId()),
                    new XElement(ns + "Properties",
                        Property(ns, "HandlerName", "IfLogicalHandler"),
                        Property(ns, "Location", "view")));
                handlers.AddFirst(handler);
            }
            return EnsureChild(handler, "Actions");
        }

        private static void VerifyRuleEvent(XElement root, XElement control,
            ViewWebComponentEventDefinition ruleEvent, ViewDefinition view)
        {
            var controlId = (string)control.Attribute("ID");
            var events = root.Elements().FirstOrDefault(x => x.Name.LocalName == "Events");
            var matches = events == null ? new List<XElement>() : events.Elements().Where(x =>
                x.Name.LocalName == "Event" &&
                string.Equals((string)x.Attribute("SourceID"), controlId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Child(x, "Name"), ruleEvent.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1)
                throw new CliException("View '" + view.Name + "' Web Component event '" + ruleEvent.Name + "' must have exactly one View rule.");
            var navigate = matches[0].Descendants().SingleOrDefault(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Navigate", StringComparison.OrdinalIgnoreCase));
            if (navigate == null || !string.Equals(ActionProperty(navigate, "Target"), ruleEvent.Target, StringComparison.OrdinalIgnoreCase) ||
                !navigate.Descendants().Any(x => x.Name.LocalName == "Source" &&
                    string.Equals((string)x.Attribute("SourceType"), "ControlProperty", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)x.Attribute("SourcePath"), controlId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)x.Attribute("SourceID"), ruleEvent.SourceProperty, StringComparison.OrdinalIgnoreCase)))
                throw new CliException("View '" + view.Name + "' Web Component event '" + ruleEvent.Name + "' has no native Navigate action sourced from " + ruleEvent.SourceProperty + ".");
        }

        private static XElement FindListAction(XElement root, string method)
        {
            var events = root.Elements().FirstOrDefault(x => x.Name.LocalName == "Events");
            return events == null ? null : events.Descendants().FirstOrDefault(x =>
                x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ActionProperty(x, "Method"), method, StringComparison.OrdinalIgnoreCase));
        }

        private static XElement BuildListActionTemplate(XNamespace ns, XElement root, string method)
        {
            var sources = root.Elements().FirstOrDefault(x => x.Name.LocalName == "Sources");
            var source = sources == null ? null : sources.Elements().FirstOrDefault(x => x.Name.LocalName == "Source" &&
                string.Equals((string)x.Attribute("ContextType"), "Primary", StringComparison.OrdinalIgnoreCase));
            if (source == null) throw new CliException("Generated View has no primary source for Web Component list binding.");
            var objectId = (string)source.Attribute("SourceID");
            var sourceName = (string)source.Attribute("SourceName") ?? string.Empty;
            var display = (string)source.Attribute("SourceDisplayName") ?? sourceName;
            var viewId = (string)root.Attribute("ID");
            var viewName = Child(root, "Name") ?? string.Empty;
            return new XElement(ns + "Action",
                new XAttribute("ID", NewId()),
                new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "Execute"),
                new XAttribute("ExecutionType", "Synchronous"),
                new XElement(ns + "Properties",
                    Property(ns, "Location", "Control"),
                    Property(ns, "Method", method),
                    Property(ns, "ViewID", viewId, viewName, viewName),
                    Property(ns, "ObjectID", objectId, display, sourceName)),
                new XElement(ns + "Results",
                    new XElement(ns + "Result",
                        new XAttribute("SourceID", objectId),
                        new XAttribute("SourceName", sourceName),
                        new XAttribute("SourceDisplayName", display),
                        new XAttribute("SourceType", "Result"))));
        }

        private static XElement NavigateParameter(XNamespace ns, string target, XElement source)
        {
            return new XElement(ns + "Parameter",
                new XAttribute("SourceID", "Sources"),
                new XAttribute("SourceType", "Value"),
                new XAttribute("TargetID", target),
                new XAttribute("TargetName", target),
                new XAttribute("TargetType", "Value"),
                new XElement(ns + "SourceValue", new XAttribute(XNamespace.Xml + "space", "preserve"), source));
        }

        private static XElement ValueSource(XNamespace ns, string value)
        {
            return new XElement(ns + "Source", new XAttribute("SourceType", "Value"), value);
        }

        private static XElement EnsureChild(XElement parent, string name)
        {
            var child = parent.Elements().FirstOrDefault(x => x.Name.LocalName == name);
            if (child != null) return child;
            child = new XElement(parent.Name.Namespace + name);
            parent.Add(child);
            return child;
        }

        private static XElement LayoutControl(XNamespace ns, string id, string name, string type)
        {
            return new XElement(ns + "Control", new XAttribute("ID", id), new XAttribute("Type", type),
                new XElement(ns + "Name", name), new XElement(ns + "DisplayName", name),
                new XElement(ns + "Properties", Property(ns, "ControlName", name, true)));
        }

        private static XElement ComponentControl(XNamespace ns, string id, ViewWebComponentDefinition component)
        {
            var properties = new XElement(ns + "Properties", Property(ns, "ControlName", component.Name, true));
            foreach (var property in component.Properties.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                properties.Add(Property(ns, property.Key, property.Value ?? string.Empty));
            return new XElement(ns + "Control", new XAttribute("ID", id), new XAttribute("Type", component.ControlType),
                new XElement(ns + "Name", component.Name), new XElement(ns + "DisplayName", component.Name), properties);
        }

        private static XElement Property(XNamespace ns, string name, string value,
            string displayValue = null, string nameValue = null)
        {
            var property = new XElement(ns + "Property", new XElement(ns + "Name", name));
            if (displayValue != null || nameValue != null)
            {
                property.Add(new XElement(ns + "DisplayValue", displayValue ?? value ?? string.Empty));
                property.Add(new XElement(ns + "NameValue", nameValue ?? value ?? string.Empty));
            }
            property.Add(new XElement(ns + "Value", value ?? string.Empty));
            return property;
        }

        private static XElement Property(XNamespace ns, string name, string value, bool identity)
        {
            return identity ? Property(ns, name, value, value, value) : Property(ns, name, value);
        }

        private static void SetProperty(XElement properties, string name, string value,
            string displayValue = null, string nameValue = null)
        {
            var property = properties.Elements().FirstOrDefault(x => x.Name.LocalName == "Property" &&
                string.Equals(Child(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
            if (property == null)
            {
                property = Property(properties.Name.Namespace, name, value, displayValue, nameValue);
                properties.Add(property);
                return;
            }
            SetChild(property, "Value", value);
            if (displayValue != null) SetChild(property, "DisplayValue", displayValue);
            if (nameValue != null) SetChild(property, "NameValue", nameValue);
        }

        private static void AssertProperty(XElement properties, string name, string expected, ViewDefinition view)
        {
            var property = properties == null ? null : properties.Elements().FirstOrDefault(x =>
                x.Name.LocalName == "Property" &&
                string.Equals(Child(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
            if (property == null || !string.Equals(Child(property, "Value"), expected, StringComparison.OrdinalIgnoreCase))
                throw new CliException("View '" + view.Name + "' Web Component property is missing or mismatched: " + name);
        }

        private static XElement ControlProperty(XElement control, string name)
        {
            var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
            return properties == null ? null : properties.Elements().FirstOrDefault(x =>
                x.Name.LocalName == "Property" &&
                string.Equals(Child(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
        }

        private static string ActionProperty(XElement action, string name)
        {
            var properties = action.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
            var property = properties == null ? null : properties.Elements().FirstOrDefault(x =>
                x.Name.LocalName == "Property" &&
                string.Equals(Child(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
            return property == null ? null : Child(property, "Value");
        }

        private static string Child(XElement element, string name)
        {
            if (element == null) return null;
            var child = element.Elements().FirstOrDefault(x => x.Name.LocalName == name);
            return child == null ? null : child.Value;
        }

        private static void SetChild(XElement element, string name, string value)
        {
            var child = element.Elements().FirstOrDefault(x => x.Name.LocalName == name);
            if (child == null)
            {
                child = new XElement(element.Name.Namespace + name);
                element.Add(child);
            }
            child.Value = value ?? string.Empty;
        }

        private static string NewId()
        {
            return Guid.NewGuid().ToString();
        }

        private static XElement CloneWithFreshIds(XElement source)
        {
            var clone = new XElement(source);
            foreach (var element in clone.DescendantsAndSelf().Where(x => x.Attribute("ID") != null))
                element.SetAttributeValue("ID", NewId());
            return clone;
        }
    }
}
