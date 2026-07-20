using System;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class FormThemeDefinition
    {
        private const string LegacyThemePropertyName = "UseLegacyTheme";
        private const string StyleProfilePropertyName = "StyleProfile";

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

            var matching = properties.Elements().Where(x => IsProperty(x, LegacyThemePropertyName)).ToList();
            if (matching.Count > 1)
                throw new CliException("Generated K2 form definition contains duplicate " + LegacyThemePropertyName + " properties.");

            var property = matching.SingleOrDefault();
            if (property == null)
            {
                property = new XElement(properties.Name.Namespace + "Property");
                properties.Add(property);
            }

            SetValue(property, "Name", LegacyThemePropertyName);
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

            var matching = properties.Elements().Where(x => IsProperty(x, LegacyThemePropertyName)).ToList();
            if (matching.Count == 0) return null;
            if (matching.Count > 1)
                throw new CliException("K2 form definition contains duplicate " + LegacyThemePropertyName + " properties.");

            var valueElement = matching[0].Elements().FirstOrDefault(x => x.Name.LocalName == "Value");
            bool value;
            if (valueElement == null || !bool.TryParse(valueElement.Value, out value))
                throw new CliException("K2 form definition has an invalid " + LegacyThemePropertyName + " value.");
            return value;
        }

        public static string SetStyleProfile(string definition, Guid guid, string name)
        {
            if (guid == Guid.Empty) throw new CliException("K2 style profile GUID is empty.");
            if (string.IsNullOrWhiteSpace(name)) throw new CliException("K2 style profile name is empty.");
            var document = Parse(definition);
            var control = FindFormControl(document);
            var properties = control.Elements().SingleOrDefault(x => x.Name.LocalName == "Properties");
            if (properties == null)
            {
                properties = new XElement(control.Name.Namespace + "Properties");
                control.Add(properties);
            }
            var matching = properties.Elements().Where(x => IsProperty(x, StyleProfilePropertyName)).ToList();
            if (matching.Count > 1) throw new CliException("Generated K2 form definition contains duplicate " + StyleProfilePropertyName + " properties.");
            var property = matching.SingleOrDefault();
            if (property == null)
            {
                property = new XElement(properties.Name.Namespace + "Property");
                properties.Add(property);
            }
            SetValue(property, "Name", StyleProfilePropertyName);
            SetValue(property, "DisplayValue", name);
            SetValue(property, "NameValue", name);
            SetValue(property, "Value", guid.ToString());
            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static FormStyleProfileReference ReadStyleProfile(string definition)
        {
            var document = Parse(definition);
            var control = FindFormControl(document);
            var properties = control.Elements().SingleOrDefault(x => x.Name.LocalName == "Properties");
            if (properties == null) return null;
            var matching = properties.Elements().Where(x => IsProperty(x, StyleProfilePropertyName)).ToList();
            if (matching.Count == 0) return null;
            if (matching.Count > 1) throw new CliException("K2 form definition contains duplicate " + StyleProfilePropertyName + " properties.");
            var value = matching[0].Elements().FirstOrDefault(x => x.Name.LocalName == "Value");
            var name = matching[0].Elements().FirstOrDefault(x => x.Name.LocalName == "NameValue");
            Guid guid;
            if (value == null || !Guid.TryParse(value.Value, out guid)) throw new CliException("K2 form definition has an invalid StyleProfile GUID.");
            return new FormStyleProfileReference { Guid = guid, Name = name == null ? null : name.Value };
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

        private static bool IsProperty(XElement property, string propertyName)
        {
            if (property.Name.LocalName != "Property") return false;
            var name = property.Elements().FirstOrDefault(x => x.Name.LocalName == "Name");
            return name != null && name.Value.Equals(propertyName, StringComparison.OrdinalIgnoreCase);
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

    internal sealed class FormStyleProfileReference
    {
        public Guid Guid { get; set; }
        public string Name { get; set; }
    }
}
