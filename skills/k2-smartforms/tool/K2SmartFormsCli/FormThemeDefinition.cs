using System;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class FormThemeDefinition
    {
        private const string PropertyName = "UseLegacyTheme";

        public static string SetUseLegacyTheme(string definition, bool useLegacyTheme)
        {
            var document = Parse(definition);
            var control = FindFormControl(document);
            var properties = control.Elements().SingleOrDefault(x => x.Name.LocalName == "Properties");
            if (properties == null)
            {
                properties = new XElement(control.Name.Namespace + "Properties");
                control.Add(properties);
            }

            var matching = properties.Elements().Where(IsLegacyThemeProperty).ToList();
            if (matching.Count > 1)
                throw new CliException("Generated K2 form definition contains duplicate " + PropertyName + " properties.");

            var property = matching.SingleOrDefault();
            if (property == null)
            {
                property = new XElement(properties.Name.Namespace + "Property");
                properties.Add(property);
            }

            SetValue(property, "Name", PropertyName);
            SetValue(property, "DisplayValue", useLegacyTheme.ToString().ToLowerInvariant());
            SetValue(property, "NameValue", useLegacyTheme.ToString().ToLowerInvariant());
            SetValue(property, "Value", useLegacyTheme.ToString().ToLowerInvariant());
            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static bool? ReadUseLegacyTheme(string definition)
        {
            var document = Parse(definition);
            var control = FindFormControl(document);
            var properties = control.Elements().SingleOrDefault(x => x.Name.LocalName == "Properties");
            if (properties == null) return null;

            var matching = properties.Elements().Where(IsLegacyThemeProperty).ToList();
            if (matching.Count == 0) return null;
            if (matching.Count > 1)
                throw new CliException("K2 form definition contains duplicate " + PropertyName + " properties.");

            var valueElement = matching[0].Elements().FirstOrDefault(x => x.Name.LocalName == "Value");
            bool value;
            if (valueElement == null || !bool.TryParse(valueElement.Value, out value))
                throw new CliException("K2 form definition has an invalid " + PropertyName + " value.");
            return value;
        }

        private static XDocument Parse(string definition)
        {
            if (string.IsNullOrWhiteSpace(definition))
                throw new CliException("K2 form definition is empty.");
            try
            {
                return XDocument.Parse(definition, LoadOptions.PreserveWhitespace);
            }
            catch (Exception ex)
            {
                throw new CliException("K2 form definition is invalid XML: " + ex.Message);
            }
        }

        private static XElement FindFormControl(XDocument document)
        {
            var controls = document.Descendants().Where(x =>
                x.Name.LocalName == "Control" &&
                x.Attributes().Any(a => a.Name.LocalName == "Type" && a.Value.Equals("Form", StringComparison.OrdinalIgnoreCase))).ToList();
            if (controls.Count != 1)
                throw new CliException("K2 form definition must contain exactly one form control; found " + controls.Count + ".");
            return controls[0];
        }

        private static bool IsLegacyThemeProperty(XElement property)
        {
            if (property.Name.LocalName != "Property") return false;
            var name = property.Elements().FirstOrDefault(x => x.Name.LocalName == "Name");
            return name != null && name.Value.Equals(PropertyName, StringComparison.OrdinalIgnoreCase);
        }

        private static void SetValue(XElement property, string elementName, string value)
        {
            var element = property.Elements().FirstOrDefault(x => x.Name.LocalName == elementName);
            if (element == null)
            {
                element = new XElement(property.Name.Namespace + elementName);
                property.Add(element);
            }
            element.Value = value;
        }
    }
}
