using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class ViewPresentationDefinition
    {
        public static string Apply(string xml, ViewDefinition view, bool master, bool detail)
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            ApplyHiddenProperties(document, view);
            ApplyPropertyLabels(document, view);
            if (view.Type == "capture" && (view.Charts == null || view.Charts.Count == 0))
            {
                if (view.LayoutColumns == 4) ApplyFourColumnLayout(document, view.Name);
                else ApplyTwoColumnLayout(document, view.Name);
            }
            if (view.HiddenVariables.Count > 0) AddHiddenVariables(document, view);
            foreach (var control in document.Descendants().Where(x => x.Name.LocalName == "Control"))
            {
                var type = (string)control.Attribute("Type") ?? string.Empty;
                if (string.Equals(type, "Label", StringComparison.OrdinalIgnoreCase)) SetBold(control);
                var text = PropertyValue(control, "Text") ?? ChildValue(control, "Name") ?? string.Empty;
                var hide = master && IsButtonControl(type);
                hide = hide || detail && IsButtonControl(type) &&
                       (text.StartsWith("Save", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Refresh", StringComparison.OrdinalIgnoreCase));
                if (!hide) continue;
                var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
                if (properties == null) { properties = new XElement(control.Name.Namespace + "Properties"); control.Add(properties); }
                SetProperty(properties, "IsVisible", "false");
            }
            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static void Verify(string xml, ViewDefinition view, bool master, bool detail)
        {
            var document = XDocument.Parse(xml);
            VerifyHiddenProperties(document, view);
            VerifyPropertyLabels(document, view);
            if (view.Type == "capture" && (view.Charts == null || view.Charts.Count == 0))
            {
                var body = FindBodyGrid(document, view.Name);
                var expected = view.LayoutColumns == 4 ? new[] { "20%", "30%", "20%", "30%" } : new[] { "40%", "60%" };
                var actual = body.Elements().First(x => x.Name.LocalName == "Columns").Elements()
                    .Select(x => (string)x.Attribute("Size") ?? string.Empty).ToArray();
                if (!actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
                    throw new CliException("View '" + view.Name + "' label/control column widths are " + string.Join("/", actual) + ", expected " + string.Join("/", expected) + ".");
            }
            var labels = document.Descendants().Where(x => x.Name.LocalName == "Control" && string.Equals((string)x.Attribute("Type"), "Label", StringComparison.OrdinalIgnoreCase)).ToList();
            if (labels.Any(x => !IsBold(x))) throw new CliException("View '" + view.Name + "' contains a label that is not bold.");
            if (view.HiddenVariables.Count > 0)
            {
                var debug = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Control" && string.Equals(ChildValue(x, "Name"), "tblDebug", StringComparison.OrdinalIgnoreCase));
                if (debug == null || !string.Equals(PropertyValue(debug, "IsVisible"), "false", StringComparison.OrdinalIgnoreCase))
                    throw new CliException("View '" + view.Name + "' has no hidden tblDebug variable table.");
                foreach (var variable in view.HiddenVariables)
                    if (!document.Descendants().Any(x => x.Name.LocalName == "Control" && string.Equals((string)x.Attribute("Type"), "DataLabel", StringComparison.OrdinalIgnoreCase) && string.Equals(ChildValue(x, "Name"), variable.Name, StringComparison.OrdinalIgnoreCase)))
                        throw new CliException("View '" + view.Name + "' has no hidden data label '" + variable.Name + "'.");
            }
            var offenders = new List<string>();
            foreach (var control in document.Descendants().Where(x => x.Name.LocalName == "Control"))
            {
                var type = (string)control.Attribute("Type") ?? string.Empty;
                var text = PropertyValue(control, "Text") ?? ChildValue(control, "Name") ?? string.Empty;
                var targeted = master && IsButtonControl(type);
                targeted = targeted || detail && IsButtonControl(type) &&
                           (text.StartsWith("Save", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Refresh", StringComparison.OrdinalIgnoreCase));
                if (targeted && !string.Equals(PropertyValue(control, "IsVisible"), "false", StringComparison.OrdinalIgnoreCase)) offenders.Add(text);
            }
            if (offenders.Count > 0)
                throw new CliException("Master-detail View '" + view.Name + "' exposes persistence buttons that bypass Form orchestration: " + string.Join(", ", offenders.ToArray()));
        }

        private static bool IsButtonControl(string type)
        {
            return !string.IsNullOrWhiteSpace(type) && type.EndsWith("Button", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyHiddenProperties(XDocument document, ViewDefinition view)
        {
            foreach (var property in view.HiddenProperties)
            {
                var field = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Field" &&
                    string.Equals(ChildValue(x, "FieldName"), property, StringComparison.OrdinalIgnoreCase));
                if (field == null) throw new CliException("Generated View '" + view.Name + "' has no field for hidden property '" + property + "'.");
                var fieldId = (string)field.Attribute("ID");
                var controlIds = document.Descendants().Where(x => x.Name.LocalName == "Control" &&
                    string.Equals((string)x.Attribute("FieldID"), fieldId, StringComparison.OrdinalIgnoreCase))
                    .Select(x => (string)x.Attribute("ID")).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                if (controlIds.Count == 0) throw new CliException("Generated View '" + view.Name + "' has no control for hidden property '" + property + "'.");
                foreach (var row in document.Descendants().Where(x => x.Name.LocalName == "Row" &&
                    x.Descendants().Any(reference => reference.Name.LocalName == "Control" && controlIds.Contains((string)reference.Attribute("ID"), StringComparer.OrdinalIgnoreCase))).ToList()) row.Remove();
            }
        }

        private static void VerifyHiddenProperties(XDocument document, ViewDefinition view)
        {
            foreach (var property in view.HiddenProperties)
            {
                var field = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Field" && string.Equals(ChildValue(x, "FieldName"), property, StringComparison.OrdinalIgnoreCase));
                if (field == null) throw new CliException("View '" + view.Name + "' has no field for hidden property '" + property + "'.");
                var fieldId = (string)field.Attribute("ID");
                var controlIds = document.Descendants().Where(x => x.Name.LocalName == "Control" && string.Equals((string)x.Attribute("FieldID"), fieldId, StringComparison.OrdinalIgnoreCase))
                    .Select(x => (string)x.Attribute("ID")).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                if (document.Descendants().Where(x => x.Name.LocalName == "Row").Any(row => row.Descendants().Any(reference => reference.Name.LocalName == "Control" && controlIds.Contains((string)reference.Attribute("ID"), StringComparer.OrdinalIgnoreCase))))
                    throw new CliException("View '" + view.Name + "' hidden property '" + property + "' remains placed in the visible layout.");
            }
        }

        private static void ApplyPropertyLabels(XDocument document, ViewDefinition view)
        {
            foreach (var label in view.PropertyLabels)
            {
                var row = FindPropertyRow(document, view, label.Key);
                var labelControl = row.Descendants().Where(x => x.Name.LocalName == "Control").Select(reference => FindControlDefinition(document, (string)reference.Attribute("ID")))
                    .FirstOrDefault(control => control != null && string.Equals((string)control.Attribute("Type"), "Label", StringComparison.OrdinalIgnoreCase));
                if (labelControl == null) throw new CliException("Generated View '" + view.Name + "' has no label control for property '" + label.Key + "'.");
                var properties = labelControl.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
                if (properties == null) { properties = new XElement(labelControl.Name.Namespace + "Properties"); labelControl.Add(properties); }
                SetProperty(properties, "Text", label.Value);
            }
        }

        private static void VerifyPropertyLabels(XDocument document, ViewDefinition view)
        {
            foreach (var label in view.PropertyLabels)
            {
                var row = FindPropertyRow(document, view, label.Key);
                var labelControl = row.Descendants().Where(x => x.Name.LocalName == "Control").Select(reference => FindControlDefinition(document, (string)reference.Attribute("ID")))
                    .FirstOrDefault(control => control != null && string.Equals((string)control.Attribute("Type"), "Label", StringComparison.OrdinalIgnoreCase));
                if (labelControl == null || !string.Equals(PropertyValue(labelControl, "Text"), label.Value, StringComparison.Ordinal))
                    throw new CliException("View '" + view.Name + "' property '" + label.Key + "' does not use label '" + label.Value + "'.");
            }
        }

        private static XElement FindPropertyRow(XDocument document, ViewDefinition view, string property)
        {
            var field = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Field" && string.Equals(ChildValue(x, "FieldName"), property, StringComparison.OrdinalIgnoreCase));
            if (field == null) throw new CliException("View '" + view.Name + "' has no field for property label '" + property + "'.");
            var fieldId = (string)field.Attribute("ID");
            var control = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Control" && string.Equals((string)x.Attribute("FieldID"), fieldId, StringComparison.OrdinalIgnoreCase));
            var controlId = control == null ? null : (string)control.Attribute("ID");
            var row = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Row" && x.Descendants().Any(reference => reference.Name.LocalName == "Control" && string.Equals((string)reference.Attribute("ID"), controlId, StringComparison.OrdinalIgnoreCase)));
            if (row == null) throw new CliException("View '" + view.Name + "' property '" + property + "' is not placed in the visible layout for label override.");
            return row;
        }

        private static XElement FindControlDefinition(XDocument document, string id)
        {
            return document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Control" && string.Equals((string)x.Attribute("ID"), id, StringComparison.OrdinalIgnoreCase) && x.Attribute("Type") != null);
        }

        private static void ApplyFourColumnLayout(XDocument document, string viewName)
        {
            var grid = FindBodyGrid(document, viewName);
            var columns = grid.Elements().First(x => x.Name.LocalName == "Columns");
            var ns = grid.Name.Namespace;
            var existingColumns = columns.Elements().Where(x => x.Name.LocalName == "Column").ToList();
            if (existingColumns.Count != 2) throw new CliException("Generated capture View '" + viewName + "' does not have the expected two-column source layout.");
            SetColumnSize(document, existingColumns[0], "20%", 0);
            SetColumnSize(document, existingColumns[1], "30%", 1);
            var third = new XElement(ns + "Column", new XAttribute("ID", NewId()));
            var fourth = new XElement(ns + "Column", new XAttribute("ID", NewId()));
            columns.Add(third);
            columns.Add(fourth);
            SetColumnSize(document, third, "20%", 2);
            SetColumnSize(document, fourth, "30%", 3);
            var controls = document.Descendants().First(x => x.Name.LocalName == "View").Elements().First(x => x.Name.LocalName == "Controls")
                .Elements().Where(x => x.Name.LocalName == "Control").ToDictionary(x => (string)x.Attribute("ID"), StringComparer.OrdinalIgnoreCase);
            var rows = grid.Elements().First(x => x.Name.LocalName == "Rows");
            var candidates = rows.Elements().Where(x => IsFieldPairRow(x, controls) && !IsLongFieldRow(x, controls)).ToList();
            for (var i = 0; i + 1 < candidates.Count; i += 2)
            {
                var first = candidates[i]; var second = candidates[i + 1];
                var firstCells = first.Descendants().First(x => x.Name.LocalName == "Cells");
                foreach (var cell in second.Descendants().First(x => x.Name.LocalName == "Cells").Elements().ToList()) firstCells.Add(cell);
                second.Remove();
            }
            foreach (var row in rows.Elements().Where(x => x.Name.LocalName == "Row"))
            {
                var cells = row.Descendants().First(x => x.Name.LocalName == "Cells").Elements().ToList();
                if (cells.Count == 2) cells[1].SetAttributeValue("ColumnSpan", "3");
            }
        }

        private static void ApplyTwoColumnLayout(XDocument document, string viewName)
        {
            var grid = FindBodyGrid(document, viewName);
            var columns = grid.Elements().First(x => x.Name.LocalName == "Columns").Elements().Where(x => x.Name.LocalName == "Column").ToList();
            if (columns.Count != 2) throw new CliException("Generated capture View '" + viewName + "' does not have the expected two-column layout.");
            SetColumnSize(document, columns[0], "40%", 0);
            SetColumnSize(document, columns[1], "60%", 1);
        }

        private static void SetColumnSize(XDocument document, XElement column, string size, int index)
        {
            column.SetAttributeValue("Size", size);
            var root = document.Descendants().First(x => x.Name.LocalName == "View");
            var controls = root.Elements().First(x => x.Name.LocalName == "Controls");
            var id = (string)column.Attribute("ID");
            var control = controls.Elements().FirstOrDefault(x => x.Name.LocalName == "Control" && string.Equals((string)x.Attribute("ID"), id, StringComparison.OrdinalIgnoreCase));
            if (control == null)
            {
                controls.Add(Control(root.Name.Namespace, id, "Column", "Column" + index + " Column", "Size", size));
                return;
            }
            var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
            if (properties == null) { properties = new XElement(root.Name.Namespace + "Properties"); control.Add(properties); }
            SetProperty(properties, "Size", size);
        }

        private static void SetBold(XElement control)
        {
            var ns = control.Name.Namespace;
            var styles = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Styles");
            if (styles == null)
            {
                styles = new XElement(ns + "Styles");
                var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
                if (properties == null) control.Add(styles); else properties.AddBeforeSelf(styles);
            }
            var style = styles.Elements().FirstOrDefault(x => x.Name.LocalName == "Style" && string.Equals((string)x.Attribute("IsDefault"), "True", StringComparison.OrdinalIgnoreCase));
            if (style == null)
            {
                style = new XElement(ns + "Style", new XAttribute("IsDefault", "True"));
                styles.Add(style);
            }
            var font = style.Elements().FirstOrDefault(x => x.Name.LocalName == "Font");
            if (font == null) { font = new XElement(ns + "Font"); style.AddFirst(font); }
            var weight = font.Elements().FirstOrDefault(x => x.Name.LocalName == "Weight");
            if (weight == null) font.Add(new XElement(ns + "Weight", "Bold")); else weight.Value = "Bold";
        }

        private static bool IsBold(XElement control)
        {
            return control.Elements().Where(x => x.Name.LocalName == "Styles").SelectMany(x => x.Elements())
                .Where(x => x.Name.LocalName == "Style" && string.Equals((string)x.Attribute("IsDefault"), "True", StringComparison.OrdinalIgnoreCase))
                .SelectMany(x => x.Elements().Where(y => y.Name.LocalName == "Font"))
                .SelectMany(x => x.Elements().Where(y => y.Name.LocalName == "Weight"))
                .Any(x => string.Equals(x.Value, "Bold", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsFieldPairRow(XElement row, IDictionary<string, XElement> controls)
        {
            var ids = row.Descendants().Where(x => x.Name.LocalName == "Cell").SelectMany(x => x.Elements().Where(y => y.Name.LocalName == "Control")).Select(x => (string)x.Attribute("ID")).ToList();
            if (ids.Count != 2 || !controls.ContainsKey(ids[0]) || !controls.ContainsKey(ids[1])) return false;
            return string.Equals((string)controls[ids[0]].Attribute("Type"), "Label", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals((string)controls[ids[1]].Attribute("Type"), "Button", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLongFieldRow(XElement row, IDictionary<string, XElement> controls)
        {
            var id = row.Descendants().Where(x => x.Name.LocalName == "Cell").SelectMany(x => x.Elements().Where(y => y.Name.LocalName == "Control")).Select(x => (string)x.Attribute("ID")).LastOrDefault();
            XElement control;
            if (id == null || !controls.TryGetValue(id, out control)) return false;
            var type = (string)control.Attribute("Type") ?? string.Empty;
            var dataType = PropertyValue(control, "DataType") ?? string.Empty;
            return type.IndexOf("Area", StringComparison.OrdinalIgnoreCase) >= 0 || dataType.Equals("Memo", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddHiddenVariables(XDocument document, ViewDefinition view)
        {
            var root = document.Descendants().First(x => x.Name.LocalName == "View");
            var controls = root.Elements().First(x => x.Name.LocalName == "Controls");
            var grid = FindBodyGrid(document, view.Name); var ns = grid.Name.Namespace;
            var tableId = NewId();
            controls.Add(Control(ns, tableId, "Table", "tblDebug", "IsVisible", "false"));
            var nestedRows = new XElement(ns + "Rows");
            foreach (var variable in view.HiddenVariables)
            {
                var rowId = NewId(); var cellId = NewId(); var labelId = NewId();
                controls.Add(Control(ns, rowId, "Row", variable.Name + " Debug Row", null, null));
                controls.Add(Control(ns, cellId, "Cell", variable.Name + " Debug Cell", null, null));
                var label = Control(ns, labelId, "DataLabel", variable.Name, "DataType", variable.DataType);
                var props = label.Elements().First(x => x.Name.LocalName == "Properties");
                props.Add(Property(ns, "LiteralVal", "false"));
                if (variable.DefaultValue != null) props.Add(Property(ns, "Text", variable.DefaultValue));
                controls.Add(label);
                nestedRows.Add(new XElement(ns + "Row", new XAttribute("ID", rowId), new XElement(ns + "Cells",
                    new XElement(ns + "Cell", new XAttribute("ID", cellId), new XElement(ns + "Control", new XAttribute("ID", labelId))))));
            }
            var outerRows = grid.Elements().First(x => x.Name.LocalName == "Rows");
            var outerRowId = NewId(); var outerCellId = NewId();
            controls.Add(Control(ns, outerRowId, "Row", "tblDebug Row", null, null));
            controls.Add(Control(ns, outerCellId, "Cell", "tblDebug Cell", null, null));
            outerRows.Add(new XElement(ns + "Row", new XAttribute("ID", outerRowId), new XElement(ns + "Cells",
                new XElement(ns + "Cell", new XAttribute("ID", outerCellId), new XAttribute("ColumnSpan", view.LayoutColumns == 4 ? "4" : "2"),
                    new XElement(ns + "Control", new XAttribute("ID", tableId), new XAttribute("LayoutType", "Grid"),
                        new XElement(ns + "Columns", new XElement(ns + "Column", new XAttribute("ID", NewId()), new XAttribute("Size", "100%"))), nestedRows)))));
        }

        private static XElement FindBodyGrid(XDocument document, string viewName)
        {
            var body = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Section" && string.Equals((string)x.Attribute("Type"), "Body", StringComparison.OrdinalIgnoreCase));
            var grid = body == null ? null : body.Elements().FirstOrDefault(x => x.Name.LocalName == "Control" && x.Attribute("LayoutType") != null);
            if (grid == null) throw new CliException("Generated capture View '" + viewName + "' has no body layout grid.");
            return grid;
        }

        private static XElement Control(XNamespace ns, string id, string type, string name, string propertyName, string propertyValue)
        {
            var properties = new XElement(ns + "Properties", Property(ns, "ControlName", name));
            if (propertyName != null) properties.Add(Property(ns, propertyName, propertyValue));
            return new XElement(ns + "Control", new XAttribute("ID", id), new XAttribute("Type", type), new XElement(ns + "Name", name), new XElement(ns + "DisplayName", name), properties);
        }

        private static XElement Property(XNamespace ns, string name, string value)
        {
            return new XElement(ns + "Property", new XElement(ns + "Name", name), new XElement(ns + "DisplayValue", value), new XElement(ns + "Value", value));
        }

        private static string NewId() { return Guid.NewGuid().ToString(); }

        private static void SetProperty(XElement properties, string name, string value)
        {
            foreach (var old in properties.Elements().Where(x => x.Name.LocalName == "Property" && string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase)).ToList()) old.Remove();
            var ns = properties.Name.Namespace;
            properties.Add(new XElement(ns + "Property", new XElement(ns + "Name", name), new XElement(ns + "DisplayValue", value), new XElement(ns + "Value", value)));
        }

        private static string PropertyValue(XElement control, string name)
        {
            var property = control.Elements().Where(x => x.Name.LocalName == "Properties").SelectMany(x => x.Elements())
                .FirstOrDefault(x => x.Name.LocalName == "Property" && string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
            return property == null ? null : ChildValue(property, "Value");
        }

        private static string ChildValue(XElement parent, string name)
        {
            var child = parent.Elements().FirstOrDefault(x => x.Name.LocalName == name);
            return child == null ? null : child.Value;
        }
    }
}
