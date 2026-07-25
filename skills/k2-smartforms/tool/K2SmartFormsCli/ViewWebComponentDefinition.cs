using System;
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

            var columnId = Guid.NewGuid().ToString();
            var rowId = Guid.NewGuid().ToString();
            var cellId = Guid.NewGuid().ToString();
            var controlId = Guid.NewGuid().ToString();
            tableRef.ReplaceNodes(
                new XElement("Columns", new XElement("Column", new XAttribute("ID", columnId), new XAttribute("Size", "100%"))),
                new XElement("Rows", new XElement("Row", new XAttribute("ID", rowId),
                    new XElement("Cells", new XElement("Cell", new XAttribute("ID", cellId),
                        new XElement("Control", new XAttribute("ID", controlId)))))));

            controls.Add(LayoutControl(columnId, component.Name + " Column", "Column"));
            controls.Add(LayoutControl(rowId, component.Name + " Row", "Row"));
            controls.Add(LayoutControl(cellId, component.Name + " Cell", "Cell"));
            controls.Add(ComponentControl(controlId, component));
            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static void Verify(string definition, ViewDefinition view)
        {
            if (view.WebComponents == null || view.WebComponents.Count == 0) return;
            var component = view.WebComponents.Single();
            var document = XDocument.Parse(definition);
            var controls = document.Descendants().Where(element => element.Name.LocalName == "Control" &&
                string.Equals((string)element.Attribute("Type"), component.ControlType, StringComparison.OrdinalIgnoreCase)).ToList();
            if (controls.Count != 1)
                throw new CliException("View '" + view.Name + "' must contain exactly one '" + component.ControlType + "' Web Component.");
            var id = (string)controls[0].Attribute("ID");
            var body = document.Descendants().Single(element => element.Name.LocalName == "Section" &&
                string.Equals((string)element.Attribute("Type"), "Body", StringComparison.OrdinalIgnoreCase));
            if (body.Descendants().Count(element => element.Name.LocalName == "Control" &&
                string.Equals((string)element.Attribute("ID"), id, StringComparison.OrdinalIgnoreCase)) != 1)
                throw new CliException("View '" + view.Name + "' Web Component is not placed once in its body.");
            foreach (var property in component.Properties)
            {
                var actual = controls[0].Descendants().FirstOrDefault(element => element.Name.LocalName == "Property" &&
                    string.Equals((string)element.Element("Name"), property.Key, StringComparison.OrdinalIgnoreCase));
                if (actual == null || !string.Equals((string)actual.Element("Value"), property.Value ?? string.Empty, StringComparison.Ordinal))
                    throw new CliException("View '" + view.Name + "' Web Component property is missing or mismatched: " + property.Key);
            }
        }

        private static XElement LayoutControl(string id, string name, string type)
        {
            return new XElement("Control", new XAttribute("ID", id), new XAttribute("Type", type),
                new XElement("Name", name), new XElement("DisplayName", name),
                new XElement("Properties", Property("ControlName", name, true)));
        }

        private static XElement ComponentControl(string id, ViewWebComponentDefinition component)
        {
            var properties = new XElement("Properties", Property("ControlName", component.Name, true));
            foreach (var property in component.Properties.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                properties.Add(Property(property.Key, property.Value ?? string.Empty, false));
            return new XElement("Control", new XAttribute("ID", id), new XAttribute("Type", component.ControlType),
                new XElement("Name", component.Name), new XElement("DisplayName", component.Name), properties);
        }

        private static XElement Property(string name, string value, bool identity)
        {
            var property = new XElement("Property", new XElement("Name", name));
            if (identity)
            {
                property.Add(new XElement("DisplayValue", value));
                property.Add(new XElement("NameValue", value));
            }
            property.Add(new XElement("Value", value));
            return property;
        }
    }
}
