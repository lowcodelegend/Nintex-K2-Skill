using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class ViewLookupDefinition
    {
        private static readonly string[] LookupPropertyNames =
        {
            "DataSourceType", "AssociationSO", "AssociationMethod", "OriginalProperty", "ValueProperty",
            "DisplayTemplate", "AllowEmptySelection", "FixedListItems", "FilterProperty", "WaterMarkText",
            "ParentControl", "ParentJoinProperty", "ChildJoinProperty"
        };

        public static string Apply(string xml, ViewDefinition view, IDictionary<string, LookupRuntimeSource> sources)
        {
            if (view.LookupControls.Count == 0 && view.ReadOnlyProperties.Count == 0 && view.DefaultValues.Count == 0) return xml;
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            foreach (var binding in view.LookupControls)
            {
                LookupRuntimeSource source;
                if (!sources.TryGetValue(binding.Lookup, out source))
                    throw new CliException("Lookup runtime metadata is missing: " + binding.Lookup);

                var control = FindEditableControl(document, view, binding.Property);
                control.SetAttributeValue("Type", "DropDown");
                var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
                if (properties == null)
                {
                    properties = new XElement(control.Name.Namespace + "Properties");
                    control.Add(properties);
                }
                foreach (var property in properties.Elements().Where(x => x.Name.LocalName == "Property" && LookupPropertyNames.Contains(GetPropertyName(x), StringComparer.OrdinalIgnoreCase)).ToList())
                    property.Remove();

                SetProperty(properties, "WaterMarkText", "Select an item", "Select an item", null);
                SetProperty(properties, "DataSourceType", "SmartObject", "SmartObject", null);
                SetProperty(properties, "AssociationSO", source.SmartObjectGuid.ToString(), source.SmartObjectDisplayName, source.SmartObjectSystemName);
                SetProperty(properties, "AssociationMethod", source.MethodName, source.MethodDisplayName, source.MethodName);
                SetProperty(properties, "OriginalProperty", binding.Property, binding.Property, binding.Property);
                SetProperty(properties, "ValueProperty", source.ValuePropertyName, source.ValuePropertyDisplayName, source.ValuePropertyName);
                SetProperty(properties, "DisplayTemplate", BuildTemplate(source), "[" + source.DisplayPropertyDisplayName + "]", null);
                SetProperty(properties, "AllowEmptySelection", binding.AllowEmptySelection ? "true" : "false", binding.AllowEmptySelection ? "true" : "false", null);
                if (binding.Cascade != null)
                {
                    var parent = FindEditableControl(document, view, binding.Cascade.ParentProperty);
                    var parentName = ChildValue(parent, "Name") ?? binding.Cascade.ParentProperty;
                    SetProperty(properties, "ParentControl", (string)parent.Attribute("ID"), parentName, parentName);
                    SetProperty(properties, "ParentJoinProperty", binding.Cascade.ParentJoinProperty, binding.Cascade.ParentJoinProperty, binding.Cascade.ParentJoinProperty);
                    SetProperty(properties, "ChildJoinProperty", binding.Cascade.ChildJoinProperty, binding.Cascade.ChildJoinProperty, binding.Cascade.ChildJoinProperty);
                }
                EnsureLookupSource(document, control, source);
                EnsurePopulationAction(document, view, control, source);
            }
            ApplyDefaultValues(document, view);
            ApplyReadOnly(document, view);
            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static void Verify(string xml, ViewDefinition view, IDictionary<string, LookupRuntimeSource> sources)
        {
            if (view.LookupControls.Count == 0 && view.ReadOnlyProperties.Count == 0 && view.DefaultValues.Count == 0) return;
            var document = XDocument.Parse(xml);
            foreach (var binding in view.LookupControls)
            {
                LookupRuntimeSource source;
                if (!sources.TryGetValue(binding.Lookup, out source))
                    throw new CliException("Lookup runtime metadata is missing during verification: " + binding.Lookup);
                var control = FindEditableControl(document, view, binding.Property);
                if (!string.Equals((string)control.Attribute("Type"), "DropDown", StringComparison.OrdinalIgnoreCase))
                    throw new CliException("View '" + view.Name + "' property '" + binding.Property + "' is not a DropDown control.");
                VerifyControlPlacement(document, control, view, binding.Property);
                var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
                AssertProperty(properties, "DataSourceType", "SmartObject", view, binding);
                AssertProperty(properties, "AssociationSO", source.SmartObjectGuid.ToString(), view, binding);
                AssertProperty(properties, "AssociationMethod", source.MethodName, view, binding);
                AssertProperty(properties, "OriginalProperty", binding.Property, view, binding);
                AssertProperty(properties, "ValueProperty", source.ValuePropertyName, view, binding);
                AssertProperty(properties, "AllowEmptySelection", binding.AllowEmptySelection ? "true" : "false", view, binding);
                var displayTemplate = GetPropertyValue(properties, "DisplayTemplate");
                if (displayTemplate == null || displayTemplate.IndexOf("SourceID=\"" + source.DisplayPropertyName + "\"", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new CliException("View '" + view.Name + "' lookup for '" + binding.Property + "' has the wrong display template.");
                if (binding.Cascade != null)
                {
                    var parent = FindEditableControl(document, view, binding.Cascade.ParentProperty);
                    AssertProperty(properties, "ParentControl", (string)parent.Attribute("ID"), view, binding);
                    AssertProperty(properties, "ParentJoinProperty", binding.Cascade.ParentJoinProperty, view, binding);
                    AssertProperty(properties, "ChildJoinProperty", binding.Cascade.ChildJoinProperty, view, binding);
                }
                VerifyLookupSource(document, view, control, source);
                VerifyPopulationAction(document, view, control, source);
            }
            VerifyDefaultValues(document, view);
            VerifyReadOnly(document, view);
        }

        private static void EnsureLookupSource(XDocument document, XElement control, LookupRuntimeSource source)
        {
            var root = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "View");
            if (root == null) throw new CliException("Generated View has no View root for lookup source registration.");
            var ns = root.Name.Namespace;
            var sources = root.Elements().FirstOrDefault(x => x.Name.LocalName == "Sources");
            if (sources == null)
            {
                sources = new XElement(ns + "Sources");
                var fields = root.Elements().FirstOrDefault(x => x.Name.LocalName == "Fields");
                if (fields == null) root.Add(sources);
                else fields.AddBeforeSelf(sources);
            }

            var controlId = (string)control.Attribute("ID");
            var contextualSources = LookupSources(sources, controlId).ToList();
            if (contextualSources.Count == 1 && IsExpectedLookupSource(contextualSources[0], source))
                return;
            foreach (var existing in contextualSources) existing.Remove();

            var sourceElement = new XElement(ns + "Source",
                new XAttribute("ID", Guid.NewGuid()),
                new XAttribute("SourceType", "Object"),
                new XAttribute("SourceID", source.SmartObjectGuid),
                new XAttribute("SourceName", source.SmartObjectSystemName),
                new XAttribute("SourceDisplayName", source.SmartObjectDisplayName),
                new XAttribute("ContextType", "Association"),
                new XAttribute("ContextID", controlId),
                new XElement(ns + "Name", source.SmartObjectDisplayName),
                new XElement(ns + "Fields"));
            var sourceFields = sourceElement.Elements().Single(x => x.Name.LocalName == "Fields");
            sourceFields.Add(BuildLookupField(ns, source, source.ValuePropertyName,
                source.ValuePropertyDisplayName, source.ValuePropertyType));
            if (!string.Equals(source.ValuePropertyName, source.DisplayPropertyName, StringComparison.OrdinalIgnoreCase))
                sourceFields.Add(BuildLookupField(ns, source, source.DisplayPropertyName,
                    source.DisplayPropertyDisplayName, source.DisplayPropertyType));
            sources.Add(sourceElement);
        }

        private static void VerifyLookupSource(XDocument document, ViewDefinition view, XElement control, LookupRuntimeSource source)
        {
            var root = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "View");
            var sources = root == null ? null : root.Elements().FirstOrDefault(x => x.Name.LocalName == "Sources");
            var controlId = (string)control.Attribute("ID");
            var matches = sources == null
                ? new List<XElement>()
                : LookupSources(sources, controlId).ToList();
            if (matches.Count != 1)
                throw new CliException("View '" + view.Name + "' lookup for '" + ChildValue(control, "Name") +
                    "' has " + matches.Count + " association sources; expected exactly one.");
            if (!IsExpectedLookupSource(matches[0], source))
                throw new CliException("View '" + view.Name + "' lookup for '" + ChildValue(control, "Name") +
                    "' has an invalid SmartObject source declaration for " + source.SmartObjectSystemName + ".");
        }

        private static IEnumerable<XElement> LookupSources(XElement sources, string controlId)
        {
            return sources.Elements().Where(x =>
                x.Name.LocalName == "Source" &&
                string.Equals((string)x.Attribute("SourceType"), "Object", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("ContextType"), "Association", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("ContextID"), controlId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsExpectedLookupSource(XElement element, LookupRuntimeSource source)
        {
            if (!string.Equals((string)element.Attribute("SourceID"), source.SmartObjectGuid.ToString(), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals((string)element.Attribute("SourceName"), source.SmartObjectSystemName, StringComparison.OrdinalIgnoreCase))
                return false;
            var fields = element.Elements().FirstOrDefault(x => x.Name.LocalName == "Fields");
            return HasExpectedLookupField(fields, source.ValuePropertyName, source.ValuePropertyType) &&
                HasExpectedLookupField(fields, source.DisplayPropertyName, source.DisplayPropertyType);
        }

        private static bool HasExpectedLookupField(XElement fields, string propertyName, string propertyType)
        {
            if (fields == null) return false;
            return fields.Elements().Count(x =>
                x.Name.LocalName == "Field" &&
                string.Equals((string)x.Attribute("Type"), "ObjectProperty", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("DataType"), propertyType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ChildValue(x, "FieldName"), propertyName, StringComparison.OrdinalIgnoreCase)) == 1;
        }

        private static XElement BuildLookupField(XNamespace ns, LookupRuntimeSource source,
            string propertyName, string propertyDisplayName, string propertyType)
        {
            return new XElement(ns + "Field",
                new XAttribute("ID", Guid.NewGuid()),
                new XAttribute("Type", "ObjectProperty"),
                new XAttribute("DataType", propertyType),
                new XElement(ns + "Name", source.SmartObjectSystemName.Replace("_", " ") + "." + propertyName),
                new XElement(ns + "FieldName", propertyName),
                new XElement(ns + "FieldDisplayName", propertyDisplayName));
        }

        private static void EnsurePopulationAction(XDocument document, ViewDefinition view, XElement control, LookupRuntimeSource source)
        {
            var controlId = (string)control.Attribute("ID");
            var initEvent = FindInitEvent(document);
            if (initEvent != null)
            {
                var existing = PopulationActions(initEvent, controlId).ToList();
                if (existing.Count == 1 && IsExpectedPopulationAction(existing[0], document, control, source))
                    return;
                foreach (var action in existing) action.Remove();
            }

            initEvent = EnsureInitEvent(document, view);
            var actions = EnsureUnconditionalActions(initEvent);
            var population = BuildPopulationAction(document, control, source);
            var firstNonPopulation = actions.Elements().FirstOrDefault(x =>
                x.Name.LocalName == "Action" && !IsPopulationAction(x));
            if (firstNonPopulation == null) actions.Add(population);
            else firstNonPopulation.AddBeforeSelf(population);
        }

        private static void VerifyPopulationAction(XDocument document, ViewDefinition view, XElement control, LookupRuntimeSource source)
        {
            var initEvent = FindInitEvent(document);
            var controlId = (string)control.Attribute("ID");
            var actions = initEvent == null
                ? new List<XElement>()
                : PopulationActions(initEvent, controlId).ToList();
            if (actions.Count != 1)
                throw new CliException("View '" + view.Name + "' lookup for '" + ChildValue(control, "Name") +
                    "' has " + actions.Count + " View Init population actions; expected exactly one.");
            if (!IsExpectedPopulationAction(actions[0], document, control, source))
                throw new CliException("View '" + view.Name + "' lookup for '" + ChildValue(control, "Name") +
                    "' does not populate from " + source.SmartObjectSystemName + "." + source.MethodName + " during View Init.");
        }

        private static XElement FindInitEvent(XDocument document)
        {
            return document.Descendants().FirstOrDefault(x =>
                x.Name.LocalName == "Event" &&
                string.Equals(ChildValue(x, "Name"), "Init", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace((string)x.Attribute("SourceType")) ||
                 string.Equals((string)x.Attribute("SourceType"), "View", StringComparison.OrdinalIgnoreCase)));
        }

        private static XElement EnsureInitEvent(XDocument document, ViewDefinition view)
        {
            var existing = FindInitEvent(document);
            if (existing != null) return existing;
            var root = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "View");
            if (root == null) throw new CliException("Generated view '" + view.Name + "' has no View root.");
            var ns = root.Name.Namespace;
            var events = root.Elements().FirstOrDefault(x => x.Name.LocalName == "Events");
            if (events == null)
            {
                events = new XElement(ns + "Events");
                root.Add(events);
            }
            var viewId = (string)root.Attribute("ID");
            if (string.IsNullOrWhiteSpace(viewId))
                throw new CliException("Generated view '" + view.Name + "' has no View ID for lookup initialization.");
            var displayName = ChildValue(root, "DisplayName") ?? ChildValue(root, "Name") ?? view.Name;
            var result = new XElement(ns + "Event",
                new XAttribute("ID", Guid.NewGuid()),
                new XAttribute("DefinitionID", Guid.NewGuid()),
                new XAttribute("Type", "User"),
                new XAttribute("SourceID", viewId),
                new XAttribute("SourceType", "View"),
                new XAttribute("SourceName", view.Name),
                new XAttribute("SourceDisplayName", displayName),
                new XElement(ns + "Name", "Init"),
                new XElement(ns + "Handlers"));
            events.AddFirst(result);
            return result;
        }

        private static XElement EnsureUnconditionalActions(XElement initEvent)
        {
            var ns = initEvent.Name.Namespace;
            var handlers = initEvent.Elements().FirstOrDefault(x => x.Name.LocalName == "Handlers");
            if (handlers == null)
            {
                handlers = new XElement(ns + "Handlers");
                initEvent.Add(handlers);
            }
            var handler = handlers.Elements().FirstOrDefault(x =>
                x.Name.LocalName == "Handler" &&
                !x.Elements().Any(e => e.Name.LocalName == "Conditions" && e.Elements().Any()));
            if (handler == null)
            {
                handler = new XElement(ns + "Handler",
                    new XAttribute("ID", Guid.NewGuid()),
                    new XAttribute("DefinitionID", Guid.NewGuid()));
                handlers.AddFirst(handler);
            }
            var actions = handler.Elements().FirstOrDefault(x => x.Name.LocalName == "Actions");
            if (actions == null)
            {
                actions = new XElement(ns + "Actions");
                handler.Add(actions);
            }
            return actions;
        }

        private static IEnumerable<XElement> PopulationActions(XElement initEvent, string controlId)
        {
            return initEvent.Descendants().Where(x =>
                x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ActionProperty(x, "ControlID"), controlId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsPopulationAction(XElement action)
        {
            return string.Equals((string)action.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(ActionProperty(action, "ControlID"));
        }

        private static bool IsExpectedPopulationAction(XElement action, XDocument document, XElement control, LookupRuntimeSource source)
        {
            var root = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "View");
            var viewId = root == null ? null : (string)root.Attribute("ID");
            var controlId = (string)control.Attribute("ID");
            if (!string.Equals(ActionProperty(action, "Location"), "View", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(ActionProperty(action, "Method"), source.MethodName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(ActionProperty(action, "ViewID"), viewId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(ActionProperty(action, "ControlID"), controlId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(ActionProperty(action, "ObjectID"), source.SmartObjectGuid.ToString(), StringComparison.OrdinalIgnoreCase))
                return false;
            return action.Descendants().Any(x =>
                x.Name.LocalName == "Result" &&
                string.Equals((string)x.Attribute("SourceID"), source.SmartObjectGuid.ToString(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("SourceType"), "Result", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("TargetID"), controlId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("TargetType"), "Control", StringComparison.OrdinalIgnoreCase));
        }

        private static XElement BuildPopulationAction(XDocument document, XElement control, LookupRuntimeSource source)
        {
            var root = document.Descendants().First(x => x.Name.LocalName == "View");
            var ns = root.Name.Namespace;
            var viewId = (string)root.Attribute("ID");
            var viewName = ChildValue(root, "Name") ?? string.Empty;
            var viewDisplayName = ChildValue(root, "DisplayName") ?? viewName;
            var controlId = (string)control.Attribute("ID");
            var controlName = ChildValue(control, "Name") ?? controlId;
            return new XElement(ns + "Action",
                new XAttribute("ID", Guid.NewGuid()),
                new XAttribute("DefinitionID", Guid.NewGuid()),
                new XAttribute("Type", "Execute"),
                new XAttribute("ExecutionType", "Synchronous"),
                new XElement(ns + "Properties",
                    ActionPropertyElement(ns, "Location", "View", null, null),
                    ActionPropertyElement(ns, "Method", source.MethodName, source.MethodDisplayName, source.MethodName),
                    ActionPropertyElement(ns, "ViewID", viewId, viewDisplayName, viewName),
                    ActionPropertyElement(ns, "ControlID", controlId, controlName, controlName),
                    ActionPropertyElement(ns, "ObjectID", source.SmartObjectGuid.ToString(), source.SmartObjectDisplayName, source.SmartObjectSystemName)),
                new XElement(ns + "Results",
                    new XElement(ns + "Result",
                        new XAttribute("SourceID", source.SmartObjectGuid),
                        new XAttribute("SourceName", source.SmartObjectSystemName),
                        new XAttribute("SourceDisplayName", source.SmartObjectDisplayName),
                        new XAttribute("SourceType", "Result"),
                        new XAttribute("TargetID", controlId),
                        new XAttribute("TargetName", controlName),
                        new XAttribute("TargetDisplayName", controlName),
                        new XAttribute("TargetType", "Control"))));
        }

        private static XElement ActionPropertyElement(XNamespace ns, string name, string value, string displayValue, string nameValue)
        {
            var property = new XElement(ns + "Property", new XElement(ns + "Name", name));
            if (displayValue != null) property.Add(new XElement(ns + "DisplayValue", displayValue));
            if (nameValue != null) property.Add(new XElement(ns + "NameValue", nameValue));
            property.Add(new XElement(ns + "Value", value));
            return property;
        }

        private static string ActionProperty(XElement action, string name)
        {
            var properties = action.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
            return GetPropertyValue(properties, name);
        }

        private static void ApplyDefaultValues(XDocument document, ViewDefinition view)
        {
            foreach (var value in view.DefaultValues)
            {
                var control = FindEditableControl(document, view, value.Key);
                var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
                if (properties == null) { properties = new XElement(control.Name.Namespace + "Properties"); control.Add(properties); }
                foreach (var old in properties.Elements().Where(x => x.Name.LocalName == "Property" && string.Equals(GetPropertyName(x), "Text", StringComparison.OrdinalIgnoreCase)).ToList()) old.Remove();
                SetProperty(properties, "Text", value.Value, value.Value, value.Value);
                foreach (var action in document.Descendants().Where(x => x.Name.LocalName == "Action" &&
                    string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ReadActionProperty(x, "Method"), "Create", StringComparison.OrdinalIgnoreCase)))
                {
                    var parameter = action.Descendants().SingleOrDefault(x => x.Name.LocalName == "Parameter" &&
                        string.Equals((string)x.Attribute("TargetID"), value.Key, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals((string)x.Attribute("TargetType"), "ObjectProperty", StringComparison.OrdinalIgnoreCase));
                    if (parameter == null) continue;
                    parameter.SetAttributeValue("SourceID", "Sources");
                    parameter.SetAttributeValue("SourceType", "Value");
                    parameter.Attributes("SourceName").Remove();
                    parameter.Attributes("SourceDisplayName").Remove();
                    parameter.Elements().Where(x => x.Name.LocalName == "SourceValue").Remove();
                    parameter.Add(new XElement(parameter.Name.Namespace + "SourceValue",
                        new XAttribute(XNamespace.Xml + "space", "preserve"), value.Value));
                }
            }
        }

        private static void VerifyDefaultValues(XDocument document, ViewDefinition view)
        {
            foreach (var value in view.DefaultValues)
            {
                var control = FindEditableControl(document, view, value.Key);
                VerifyControlPlacement(document, control, view, value.Key);
                var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
                if (!string.Equals(GetPropertyValue(properties, "Text"), value.Value, StringComparison.Ordinal))
                    throw new CliException("View '" + view.Name + "' property '" + value.Key + "' does not have the configured default value.");
                var createParameters = document.Descendants().Where(x => x.Name.LocalName == "Action" &&
                    string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ReadActionProperty(x, "Method"), "Create", StringComparison.OrdinalIgnoreCase))
                    .SelectMany(x => x.Descendants().Where(p => p.Name.LocalName == "Parameter" &&
                        string.Equals((string)p.Attribute("TargetID"), value.Key, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals((string)p.Attribute("TargetType"), "ObjectProperty", StringComparison.OrdinalIgnoreCase))).ToList();
                if (createParameters.Count == 0 || createParameters.Any(x =>
                    !string.Equals((string)x.Attribute("SourceID"), "Sources", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals((string)x.Attribute("SourceType"), "Value", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(x.Elements().FirstOrDefault(e => e.Name.LocalName == "SourceValue") == null ? null :
                        x.Elements().First(e => e.Name.LocalName == "SourceValue").Value, value.Value, StringComparison.Ordinal)))
                    throw new CliException("View '" + view.Name + "' Create rule does not supply literal default '" + value.Key + "=" + value.Value + "'.");
            }
        }

        private static string ReadActionProperty(XElement action, string name)
        {
            var property = action.Descendants().FirstOrDefault(x => x.Name.LocalName == "Property" &&
                string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
            return property == null ? null : ChildValue(property, "Value");
        }

        private static void ApplyReadOnly(XDocument document, ViewDefinition view)
        {
            foreach (var property in view.ReadOnlyProperties)
            {
                var control = FindEditableControl(document, view, property);
                var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
                if (properties == null) { properties = new XElement(control.Name.Namespace + "Properties"); control.Add(properties); }
                foreach (var old in properties.Elements().Where(x => x.Name.LocalName == "Property" && string.Equals(GetPropertyName(x), "IsReadOnly", StringComparison.OrdinalIgnoreCase)).ToList()) old.Remove();
                SetProperty(properties, "IsReadOnly", "true", "true", null);
            }
        }

        private static void VerifyReadOnly(XDocument document, ViewDefinition view)
        {
            foreach (var property in view.ReadOnlyProperties)
            {
                var control = FindEditableControl(document, view, property);
                var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
                if (!string.Equals(GetPropertyValue(properties, "IsReadOnly"), "true", StringComparison.OrdinalIgnoreCase))
                    throw new CliException("View '" + view.Name + "' property '" + property + "' is not read-only.");
            }
        }

        private static XElement FindEditableControl(XDocument document, ViewDefinition view, string propertyName)
        {
            var controls = document.Descendants().Where(x =>
                x.Name.LocalName == "Control" &&
                x.Attribute("Type") != null &&
                !string.Equals((string)x.Attribute("Type"), "Label", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals((string)x.Attribute("Type"), "DataLabel", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals((string)x.Attribute("Type"), "ListDisplay", StringComparison.OrdinalIgnoreCase)).ToList();
            var field = document.Descendants().Where(x =>
                x.Name.LocalName == "Field" &&
                string.Equals(ChildValue(x, "FieldName"), propertyName, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(candidate =>
                {
                    var candidateId = (string)candidate.Attribute("ID");
                    return !string.IsNullOrWhiteSpace(candidateId) && controls.Any(control =>
                        string.Equals((string)control.Attribute("FieldID"), candidateId, StringComparison.OrdinalIgnoreCase));
                });
            if (field == null) throw new CliException("Generated view '" + view.Name + "' has no field for lookup property '" + propertyName + "'.");
            var fieldId = (string)field.Attribute("ID");
            var candidates = controls.Where(x =>
                string.Equals((string)x.Attribute("FieldID"), fieldId, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace((string)x.Attribute("ID"))).ToList();
            if (candidates.Count != 1)
                throw new CliException("Generated view '" + view.Name + "' has " + candidates.Count + " editable controls for lookup property '" + propertyName + "'; expected one. Candidates: " + string.Join("; ", candidates.Select(x => ((string)x.Attribute("Type") ?? "<type>") + "/" + (ChildValue(x, "Name") ?? (string)x.Attribute("ID"))).ToArray()));
            return candidates[0];
        }

        private static void VerifyControlPlacement(XDocument document, XElement control, ViewDefinition view, string propertyName)
        {
            if (view.HiddenProperties.Contains(propertyName, StringComparer.OrdinalIgnoreCase)) return;
            var id = (string)control.Attribute("ID");
            var placed = !string.IsNullOrWhiteSpace(id) && document.Descendants().Any(x =>
                x.Name.LocalName == "Control" &&
                !object.ReferenceEquals(x, control) &&
                string.Equals((string)x.Attribute("ID"), id, StringComparison.OrdinalIgnoreCase));
            if (!placed)
                throw new CliException("View '" + view.Name + "' property '" + propertyName + "' has a configured control that is not placed in the live View layout.");
        }

        private static string ChildValue(XElement parent, string name)
        {
            var child = parent.Elements().FirstOrDefault(x => x.Name.LocalName == name);
            return child == null ? null : child.Value;
        }

        private static string BuildTemplate(LookupRuntimeSource source)
        {
            return new XElement("Template",
                new XElement("Item",
                    new XAttribute("SourceType", "ObjectProperty"),
                    new XAttribute("SourceID", source.DisplayPropertyName),
                    new XAttribute("SourceName", source.DisplayPropertyName),
                    new XAttribute("SourceDisplayName", source.DisplayPropertyDisplayName),
                    new XAttribute("DataType", source.DisplayPropertyType))).ToString(SaveOptions.DisableFormatting);
        }

        private static void SetProperty(XElement properties, string name, string value, string displayValue, string nameValue)
        {
            var ns = properties.Name.Namespace;
            var property = new XElement(ns + "Property", new XElement(ns + "Name", name));
            if (displayValue != null) property.Add(new XElement(ns + "DisplayValue", displayValue));
            if (nameValue != null) property.Add(new XElement(ns + "NameValue", nameValue));
            property.Add(new XElement(ns + "Value", value));
            properties.Add(property);
        }

        private static string GetPropertyName(XElement property)
        {
            var name = property.Elements().FirstOrDefault(x => x.Name.LocalName == "Name");
            return name == null ? null : name.Value;
        }

        private static string GetPropertyValue(XElement properties, string name)
        {
            if (properties == null) return null;
            var property = properties.Elements().FirstOrDefault(x => x.Name.LocalName == "Property" && string.Equals(GetPropertyName(x), name, StringComparison.OrdinalIgnoreCase));
            if (property == null) return null;
            var value = property.Elements().FirstOrDefault(x => x.Name.LocalName == "Value");
            return value == null ? null : value.Value;
        }

        private static void AssertProperty(XElement properties, string name, string expected, ViewDefinition view, LookupControlDefinition binding)
        {
            var actual = GetPropertyValue(properties, name);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new CliException("View '" + view.Name + "' lookup for '" + binding.Property + "' has " + name + "='" + actual + "', expected '" + expected + "'.");
        }
    }

    internal sealed class LookupRuntimeSource
    {
        public string Name { get; set; }
        public Guid SmartObjectGuid { get; set; }
        public string SmartObjectSystemName { get; set; }
        public string SmartObjectDisplayName { get; set; }
        public string MethodName { get; set; }
        public string MethodDisplayName { get; set; }
        public string ValuePropertyName { get; set; }
        public string ValuePropertyDisplayName { get; set; }
        public string ValuePropertyType { get; set; }
        public string DisplayPropertyName { get; set; }
        public string DisplayPropertyDisplayName { get; set; }
        public string DisplayPropertyType { get; set; }
        public HashSet<string> PropertyNames { get; set; }
    }
}
