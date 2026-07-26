using System;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class FormActionAlignment
    {
        internal const string Left = "left";
        internal const string Right = "right";

        public static XElement CellControl(XNamespace ns, string id, string name, string alignment)
        {
            RequireAlignment(alignment);
            return new XElement(ns + "Control",
                new XAttribute("ID", id),
                new XAttribute("Type", "Cell"),
                new XElement(ns + "Name", name),
                new XElement(ns + "DisplayName", name),
                new XElement(ns + "Properties", Property(ns, "ControlName", name)),
                Styles(ns, alignment));
        }

        public static void VerifyButtonCell(XElement scope, XElement controls, string buttonId,
            string expectedAlignment, string owner)
        {
            RequireAlignment(expectedAlignment);
            var references = scope.Descendants().Where(x =>
                x.Name.LocalName == "Control" &&
                string.Equals((string)x.Attribute("ID"), buttonId, StringComparison.OrdinalIgnoreCase) &&
                x.Ancestors().Any(y => y.Name.LocalName == "Cell")).ToList();
            if (references.Count != 1)
                throw new CliException(owner + " must occur in exactly one native Table cell; found " +
                    references.Count + ".");
            var cell = references[0].Ancestors().First(x => x.Name.LocalName == "Cell");
            var cellId = (string)cell.Attribute("ID");
            var definitions = controls.Elements().Where(x =>
                x.Name.LocalName == "Control" &&
                string.Equals((string)x.Attribute("Type"), "Cell", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("ID"), cellId, StringComparison.OrdinalIgnoreCase)).ToList();
            if (definitions.Count != 1)
                throw new CliException(owner + " does not resolve one native Cell control definition.");
            var style = definitions[0].Elements().Where(x => x.Name.LocalName == "Styles")
                .SelectMany(x => x.Elements())
                .SingleOrDefault(x => x.Name.LocalName == "Style" &&
                    string.Equals((string)x.Attribute("IsDefault"), "True", StringComparison.OrdinalIgnoreCase));
            var actual = style == null ? null : style.Elements()
                .Where(x => x.Name.LocalName == "Text")
                .SelectMany(x => x.Elements())
                .Where(x => x.Name.LocalName == "Align")
                .Select(x => x.Value)
                .SingleOrDefault();
            if (!string.Equals(actual, expectedAlignment, StringComparison.OrdinalIgnoreCase))
                throw new CliException(owner + " native Table cell alignment is '" +
                    (actual ?? "<missing>") + "', expected '" + expectedAlignment + "'.");
        }

        private static XElement Styles(XNamespace ns, string alignment)
        {
            return new XElement(ns + "Styles",
                new XElement(ns + "Style", new XAttribute("IsDefault", "True"),
                    new XElement(ns + "Text", new XElement(ns + "Align", alignment))));
        }

        private static XElement Property(XNamespace ns, string name, string value)
        {
            return new XElement(ns + "Property",
                new XElement(ns + "Name", name),
                new XElement(ns + "DisplayValue", value),
                new XElement(ns + "NameValue", value),
                new XElement(ns + "Value", value));
        }

        private static void RequireAlignment(string alignment)
        {
            if (!string.Equals(alignment, Left, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(alignment, Right, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Form action alignment must be left or right.", "alignment");
        }
    }
}
