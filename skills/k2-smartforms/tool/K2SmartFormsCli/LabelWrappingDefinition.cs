using System;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class LabelWrappingDefinition
    {
        public static string Apply(string xml)
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            foreach (var control in document.Descendants().Where(IsLabel))
                MasterDetailRules.SetElementProperty(control, "WrapText", "true");
            var result = document.ToString(SaveOptions.DisableFormatting);
            Verify(result);
            return result;
        }

        public static void Verify(string xml)
        {
            var document = XDocument.Parse(xml);
            foreach (var control in document.Descendants().Where(IsLabel))
            {
                var properties = control.Elements().Where(x => x.Name.LocalName == "Properties")
                    .SelectMany(x => x.Elements()).Where(x => x.Name.LocalName == "Property" &&
                        x.Elements().Any(child => child.Name.LocalName == "Name" && child.Value == "WrapText")).ToList();
                if (properties.Count != 1 || new[] { "DisplayValue", "NameValue", "Value" }.Any(name =>
                    properties[0].Elements().Count(x => x.Name.LocalName == name &&
                        string.Equals(x.Value, "true", StringComparison.OrdinalIgnoreCase)) != 1))
                    throw new CliException("Generated Label must have WrapText=true with a complete Boolean property triple: " +
                        ((string)control.Attribute("ID") ?? "<unknown>"));
            }
        }

        private static bool IsLabel(XElement element)
        {
            return element.Name.LocalName == "Control" &&
                string.Equals((string)element.Attribute("Type"), "Label", StringComparison.OrdinalIgnoreCase);
        }
    }
}
