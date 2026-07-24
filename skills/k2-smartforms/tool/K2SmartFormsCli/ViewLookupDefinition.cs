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
            "DataSourceType", "AssociationSO", "AssociationMethod", "ValueProperty",
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
            }
            VerifyDefaultValues(document, view);
            VerifyReadOnly(document, view);
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
            var field = document.Descendants()
                .Where(x => x.Name.LocalName == "Field")
                .FirstOrDefault(x => string.Equals(
                    (string)x.Elements().FirstOrDefault(e => e.Name.LocalName == "FieldName"),
                    propertyName,
                    StringComparison.OrdinalIgnoreCase));
            if (field == null) throw new CliException("Generated view '" + view.Name + "' has no field for lookup property '" + propertyName + "'.");
            var fieldId = (string)field.Attribute("ID");
            var candidates = document.Descendants().Where(x =>
                x.Name.LocalName == "Control" &&
                string.Equals((string)x.Attribute("FieldID"), fieldId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals((string)x.Attribute("Type"), "Label", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals((string)x.Attribute("Type"), "DataLabel", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals((string)x.Attribute("Type"), "ListDisplay", StringComparison.OrdinalIgnoreCase)).ToList();
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
