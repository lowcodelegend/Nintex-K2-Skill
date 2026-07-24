using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class ViewPresentationDefinition
    {
        public static string Apply(string xml, ViewDefinition view, bool master, bool detail)
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            ApplyEditableListDefaults(document, view);
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
            if (string.Equals(view.Type, "capture-list", StringComparison.OrdinalIgnoreCase))
            {
                VerifyEditableListDefaults(document, view);
                VerifyEditableListStructure(document, view);
            }
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

        private static void ApplyEditableListDefaults(XDocument document, ViewDefinition view)
        {
            if (!string.Equals(view.Type, "capture-list", StringComparison.OrdinalIgnoreCase)) return;

            var viewControl = FindEditableListViewControl(document, view);
            var properties = viewControl.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
            if (properties == null) return;
            foreach (var property in properties.Elements().Where(x => x.Name.LocalName == "Property" &&
                string.Equals(ChildValue(x, "Name"), "ShowAddRow", StringComparison.OrdinalIgnoreCase)).ToList())
                property.Remove();
        }

        private static void VerifyEditableListDefaults(XDocument document, ViewDefinition view)
        {
            var viewControl = FindEditableListViewControl(document, view);
            var hasShowAddRow = viewControl.Elements().Where(x => x.Name.LocalName == "Properties")
                .SelectMany(x => x.Elements()).Any(x => x.Name.LocalName == "Property" &&
                    string.Equals(ChildValue(x, "Name"), "ShowAddRow", StringComparison.OrdinalIgnoreCase));
            if (hasShowAddRow)
                throw new CliException("Capture-list View '" + view.Name +
                    "' must disable the editable-list 'Enable Add new row link' setting by omitting the ShowAddRow property.");
        }

        private static XElement FindEditableListViewControl(XDocument document, ViewDefinition view)
        {
            var controls = document.Descendants().Where(x => x.Name.LocalName == "Control" &&
                string.Equals((string)x.Attribute("Type"), "View", StringComparison.OrdinalIgnoreCase)).ToList();
            if (controls.Count != 1)
                throw new CliException("Generated capture-list View '" + view.Name +
                    "' must contain exactly one root View control; found " + controls.Count + ".");
            return controls[0];
        }

        private static void ApplyHiddenProperties(XDocument document, ViewDefinition view)
        {
            if (string.Equals(view.Type, "capture-list", StringComparison.OrdinalIgnoreCase))
            {
                ApplyEditableListHiddenProperties(document, view);
                VerifyEditableListStructure(document, view);
                return;
            }

            foreach (var property in view.HiddenProperties)
            {
                var controlIds = FindFieldControlIds(document, view, property, "hidden");
                foreach (var row in document.Descendants().Where(x => x.Name.LocalName == "Row" &&
                    x.Descendants().Any(reference => reference.Name.LocalName == "Control" && controlIds.Contains((string)reference.Attribute("ID"), StringComparer.OrdinalIgnoreCase))).ToList()) row.Remove();
            }
        }

        private static void VerifyHiddenProperties(XDocument document, ViewDefinition view)
        {
            foreach (var property in view.HiddenProperties)
            {
                var controlIds = FindFieldControlIds(document, view, property, "hidden");
                if (document.Descendants().Where(x => x.Name.LocalName == "Row").Any(row => row.Descendants().Any(reference => reference.Name.LocalName == "Control" && controlIds.Contains((string)reference.Attribute("ID"), StringComparer.OrdinalIgnoreCase))))
                    throw new CliException("View '" + view.Name + "' hidden property '" + property + "' remains placed in the visible layout.");
            }
        }

        private static void ApplyEditableListHiddenProperties(XDocument document, ViewDefinition view)
        {
            if (view.HiddenProperties.Count == 0) return;

            var layout = GetEditableListLayout(document, view);
            foreach (var property in view.HiddenProperties)
            {
                var controlIds = FindFieldControlIds(document, view, property, "hidden");
                var headerOrdinals = FindCellOrdinals(layout.Header, controlIds);
                var displayOrdinals = FindCellOrdinals(layout.Display, controlIds);
                var editOrdinals = FindCellOrdinals(layout.Edit, controlIds);
                var placedCount = headerOrdinals.Count + displayOrdinals.Count + editOrdinals.Count;

                if (placedCount == 0) continue;
                if (headerOrdinals.Count != 1 || displayOrdinals.Count != 1 || editOrdinals.Count != 1)
                    throw new CliException("Generated capture-list View '" + view.Name + "' hidden property '" + property +
                        "' must have exactly one Header, Display, and Edit placement before it can be hidden.");

                var ordinal = headerOrdinals[0];
                if (displayOrdinals[0] != ordinal || editOrdinals[0] != ordinal)
                    throw new CliException("Generated capture-list View '" + view.Name + "' hidden property '" + property +
                        "' has inconsistent Header, Display, and Edit column ordinals.");

                var headerCells = Cells(layout.Header);
                var displayCells = Cells(layout.Display);
                var footerCells = Cells(layout.Footer);
                var editCells = Cells(layout.Edit);
                var columns = Columns(layout.Grid);
                if (ordinal < 0 || ordinal >= headerCells.Count || ordinal >= displayCells.Count ||
                    ordinal >= footerCells.Count || ordinal >= editCells.Count || ordinal >= columns.Count)
                    throw new CliException("Generated capture-list View '" + view.Name + "' hidden property '" + property +
                        "' resolves outside the shared editable-list column layout.");

                RemovePlacementAndDefinition(headerCells[ordinal], layout.ControlDefinitions);
                RemovePlacementAndDefinition(displayCells[ordinal], layout.ControlDefinitions);
                RemovePlacementAndDefinition(footerCells[ordinal], layout.ControlDefinitions);
                RemovePlacementAndDefinition(editCells[ordinal], layout.ControlDefinitions);
                RemovePlacementAndDefinition(columns[ordinal], layout.ControlDefinitions);
                RecalculateEditableListColumnWidths(document, view, layout);
            }
        }

        private static void VerifyEditableListStructure(XDocument document, ViewDefinition view)
        {
            var layout = GetEditableListLayout(document, view);
            var columns = Columns(layout.Grid);
            var templateRows = new[] { layout.Header, layout.Display, layout.Footer, layout.Edit };
            var cellCounts = templateRows.Select(row => Cells(row).Count).ToList();
            if (cellCounts.Distinct().Count() != 1)
                throw new CliException("Capture-list View '" + view.Name + "' Header, Display, Footer, and Edit rows do not have the same cell count: " +
                    string.Join("/", cellCounts.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray()) + ".");
            if (columns.Count != cellCounts[0])
                throw new CliException("Capture-list View '" + view.Name + "' has " + columns.Count +
                    " column placements but " + cellCounts[0] + " cells in each template row.");

            var selectedProperties = view.Options.Contains("all-properties", StringComparer.OrdinalIgnoreCase)
                ? ResolveHeaderPropertyNames(document, view, layout)
                : view.Properties;
            var visibleProperties = selectedProperties.Where(property =>
                !view.HiddenProperties.Contains(property, StringComparer.OrdinalIgnoreCase)).ToList();
            if (columns.Count != visibleProperties.Count)
                throw new CliException("Capture-list View '" + view.Name + "' has " + columns.Count +
                    " visible columns, expected " + visibleProperties.Count + " selected non-hidden properties.");

            foreach (var row in new[] { layout.Header, layout.Display, layout.Edit })
            {
                foreach (var cell in Cells(row))
                {
                    var references = cell.Elements().Where(x => x.Name.LocalName == "Control").ToList();
                    if (references.Count != 1)
                        throw new CliException("Capture-list View '" + view.Name + "' " + TemplateName(row, layout.ControlDefinitions) +
                            " row contains a cell with " + references.Count + " control placements; exactly one is required.");
                }
            }

            var visibleOrdinals = new HashSet<int>();
            foreach (var property in visibleProperties)
            {
                var controlIds = FindFieldControlIds(document, view, property, "selected");
                var headerOrdinals = FindCellOrdinals(layout.Header, controlIds);
                var displayOrdinals = FindCellOrdinals(layout.Display, controlIds);
                var editOrdinals = FindCellOrdinals(layout.Edit, controlIds);
                if (headerOrdinals.Count != 1 || displayOrdinals.Count != 1 || editOrdinals.Count != 1)
                    throw new CliException("Capture-list View '" + view.Name + "' visible property '" + property +
                        "' must have exactly one Header, Display, and Edit placement.");
                if (headerOrdinals[0] != displayOrdinals[0] || headerOrdinals[0] != editOrdinals[0])
                    throw new CliException("Capture-list View '" + view.Name + "' visible property '" + property +
                        "' does not use the same Header, Display, and Edit column ordinal.");
                if (!visibleOrdinals.Add(headerOrdinals[0]))
                    throw new CliException("Capture-list View '" + view.Name + "' has more than one visible property mapped to column ordinal " +
                        headerOrdinals[0] + ".");
            }

            foreach (var property in view.HiddenProperties)
            {
                var controlIds = FindFieldControlIds(document, view, property, "hidden");
                if (FindCellOrdinals(layout.Header, controlIds).Count > 0 ||
                    FindCellOrdinals(layout.Display, controlIds).Count > 0 ||
                    FindCellOrdinals(layout.Edit, controlIds).Count > 0)
                    throw new CliException("Capture-list View '" + view.Name + "' hidden property '" + property +
                        "' remains placed in an editable-list template row.");
            }

            decimal totalWidth = 0m;
            for (var i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                var columnId = (string)column.Attribute("ID");
                XElement definition;
                if (string.IsNullOrWhiteSpace(columnId) ||
                    !layout.ControlDefinitions.TryGetValue(columnId, out definition) ||
                    !string.Equals((string)definition.Attribute("Type"), "Column", StringComparison.OrdinalIgnoreCase))
                    throw new CliException("Capture-list View '" + view.Name + "' column " + i + " has no matching Column control definition.");

                var placementSize = (string)column.Attribute("Size") ?? string.Empty;
                var definitionSize = PropertyValue(definition, "Size") ?? string.Empty;
                if (!string.Equals(placementSize, definitionSize, StringComparison.OrdinalIgnoreCase))
                    throw new CliException("Capture-list View '" + view.Name + "' column " + i +
                        " placement/definition widths disagree: " + placementSize + "/" + definitionSize + ".");
                totalWidth += ParsePercentage(placementSize, view.Name, i);
            }
            if (totalWidth != 100m)
                throw new CliException("Capture-list View '" + view.Name + "' column widths total " +
                    totalWidth.ToString(CultureInfo.InvariantCulture) + "%, expected 100%.");

            foreach (var row in templateRows)
            {
                foreach (var cell in Cells(row))
                {
                    var cellId = (string)cell.Attribute("ID");
                    XElement definition;
                    if (string.IsNullOrWhiteSpace(cellId) ||
                        !layout.ControlDefinitions.TryGetValue(cellId, out definition) ||
                        !string.Equals((string)definition.Attribute("Type"), "Cell", StringComparison.OrdinalIgnoreCase))
                        throw new CliException("Capture-list View '" + view.Name + "' " + TemplateName(row, layout.ControlDefinitions) +
                            " row has a cell without a matching Cell control definition.");
                }
            }
        }

        private static EditableListLayout GetEditableListLayout(XDocument document, ViewDefinition view)
        {
            var grid = FindBodyGrid(document, view.Name);
            var definitions = ControlDefinitions(document, view.Name);
            var rows = grid.Elements().FirstOrDefault(x => x.Name.LocalName == "Rows");
            if (rows == null) throw new CliException("Capture-list View '" + view.Name + "' has no Body Rows collection.");

            var placedRows = rows.Elements().Where(x => x.Name.LocalName == "Row").ToList();
            var header = RowsForTemplate(placedRows, definitions, "Header");
            var display = RowsForTemplate(placedRows, definitions, "Display");
            var footer = RowsForTemplate(placedRows, definitions, "Footer");
            var edit = RowsForTemplate(placedRows, definitions, "Edit");
            RequireSingleTemplateRow(view.Name, "Header", header);
            RequireSingleTemplateRow(view.Name, "data Display", display);
            RequireSingleTemplateRow(view.Name, "Footer", footer);
            RequireSingleTemplateRow(view.Name, "Edit", edit);
            if (placedRows.Count != 4)
                throw new CliException("Capture-list View '" + view.Name +
                    "' Body list layout must contain only the Header, data Display, Footer, and Edit rows; found " + placedRows.Count + " rows.");

            return new EditableListLayout
            {
                Grid = grid,
                Header = header[0],
                Display = display[0],
                Footer = footer[0],
                Edit = edit[0],
                ControlDefinitions = definitions
            };
        }

        private static IDictionary<string, XElement> ControlDefinitions(XDocument document, string viewName)
        {
            var view = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "View");
            var controls = view == null ? null : view.Elements().FirstOrDefault(x => x.Name.LocalName == "Controls");
            if (controls == null) throw new CliException("Generated View '" + viewName + "' has no root Controls collection.");
            return controls.Elements().Where(x => x.Name.LocalName == "Control" && !string.IsNullOrWhiteSpace((string)x.Attribute("ID")))
                .ToDictionary(x => (string)x.Attribute("ID"), StringComparer.OrdinalIgnoreCase);
        }

        private static List<XElement> RowsForTemplate(IEnumerable<XElement> rows, IDictionary<string, XElement> definitions, string template)
        {
            return rows.Where(row => string.Equals(TemplateName(row, definitions), template, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private static string TemplateName(XElement row, IDictionary<string, XElement> definitions)
        {
            var rowId = (string)row.Attribute("ID");
            XElement definition;
            return !string.IsNullOrWhiteSpace(rowId) && definitions.TryGetValue(rowId, out definition)
                ? PropertyValue(definition, "Template")
                : null;
        }

        private static void RequireSingleTemplateRow(string viewName, string template, IList<XElement> rows)
        {
            if (rows.Count != 1)
                throw new CliException("Capture-list View '" + viewName + "' must contain exactly one " + template +
                    " row in its Body list layout; found " + rows.Count + ".");
        }

        private static List<XElement> Columns(XElement grid)
        {
            var columns = grid.Elements().FirstOrDefault(x => x.Name.LocalName == "Columns");
            return columns == null ? new List<XElement>() : columns.Elements().Where(x => x.Name.LocalName == "Column").ToList();
        }

        private static List<XElement> Cells(XElement row)
        {
            var cells = row.Elements().FirstOrDefault(x => x.Name.LocalName == "Cells");
            return cells == null ? new List<XElement>() : cells.Elements().Where(x => x.Name.LocalName == "Cell").ToList();
        }

        private static List<int> FindCellOrdinals(XElement row, IList<string> controlIds)
        {
            var result = new List<int>();
            var cells = Cells(row);
            for (var i = 0; i < cells.Count; i++)
                if (cells[i].Elements().Any(reference => reference.Name.LocalName == "Control" &&
                    controlIds.Contains((string)reference.Attribute("ID"), StringComparer.OrdinalIgnoreCase)))
                    result.Add(i);
            return result;
        }

        private static List<string> ResolveHeaderPropertyNames(XDocument document, ViewDefinition view, EditableListLayout layout)
        {
            var properties = new List<string>();
            foreach (var cell in Cells(layout.Header))
            {
                var reference = cell.Elements().SingleOrDefault(x => x.Name.LocalName == "Control");
                var controlId = reference == null ? null : (string)reference.Attribute("ID");
                XElement definition;
                if (string.IsNullOrWhiteSpace(controlId) ||
                    !layout.ControlDefinitions.TryGetValue(controlId, out definition) ||
                    string.IsNullOrWhiteSpace((string)definition.Attribute("FieldID")))
                    throw new CliException("Capture-list View '" + view.Name +
                        "' all-properties Header contains a control without a bound View field.");

                var fieldId = (string)definition.Attribute("FieldID");
                var field = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Field" &&
                    string.Equals((string)x.Attribute("ID"), fieldId, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(ChildValue(x, "FieldName")));
                if (field == null)
                    throw new CliException("Capture-list View '" + view.Name + "' all-properties Header field '" +
                        fieldId + "' has no View field definition.");
                properties.Add(ChildValue(field, "FieldName"));
            }
            if (properties.Count != properties.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                throw new CliException("Capture-list View '" + view.Name + "' all-properties Header contains duplicate field placements.");
            return properties;
        }

        private static List<string> FindFieldControlIds(XDocument document, ViewDefinition view, string property, string purpose)
        {
            var controls = document.Descendants().Where(x => x.Name.LocalName == "Control" &&
                x.Attribute("Type") != null && !string.IsNullOrWhiteSpace((string)x.Attribute("ID"))).ToList();
            var field = document.Descendants().Where(x => x.Name.LocalName == "Field" &&
                string.Equals(ChildValue(x, "FieldName"), property, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(candidate =>
                {
                    var candidateId = (string)candidate.Attribute("ID");
                    return !string.IsNullOrWhiteSpace(candidateId) && controls.Any(control =>
                        string.Equals((string)control.Attribute("FieldID"), candidateId, StringComparison.OrdinalIgnoreCase));
                });
            if (field == null)
                throw new CliException("View '" + view.Name + "' has no bound field for " + purpose + " property '" + property + "'.");

            var fieldId = (string)field.Attribute("ID");
            var controlIds = controls.Where(x =>
                string.Equals((string)x.Attribute("FieldID"), fieldId, StringComparison.OrdinalIgnoreCase))
                .Select(x => (string)x.Attribute("ID")).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (controlIds.Count == 0)
                throw new CliException("View '" + view.Name + "' has no control for " + purpose + " property '" + property + "'.");
            return controlIds;
        }

        private static void RemovePlacementAndDefinition(XElement placement, IDictionary<string, XElement> definitions)
        {
            var id = (string)placement.Attribute("ID");
            placement.Remove();
            XElement definition;
            if (!string.IsNullOrWhiteSpace(id) && definitions.TryGetValue(id, out definition))
            {
                definition.Remove();
                definitions.Remove(id);
            }
        }

        private static void RecalculateEditableListColumnWidths(XDocument document, ViewDefinition view, EditableListLayout layout)
        {
            var columns = Columns(layout.Grid);
            if (columns.Count == 0)
                throw new CliException("Capture-list View '" + view.Name + "' cannot hide every selected property.");
            var baseSize = 100 / columns.Count;
            var remainder = 100 % columns.Count;
            for (var i = 0; i < columns.Count; i++)
                SetColumnSize(document, columns[i], (baseSize + (i < remainder ? 1 : 0)).ToString(CultureInfo.InvariantCulture) + "%", i);
        }

        private static decimal ParsePercentage(string value, string viewName, int ordinal)
        {
            decimal parsed;
            var number = (value ?? string.Empty).Trim();
            if (!number.EndsWith("%", StringComparison.Ordinal) ||
                !decimal.TryParse(number.Substring(0, number.Length - 1), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
                throw new CliException("Capture-list View '" + viewName + "' column " + ordinal +
                    " has invalid percentage width '" + value + "'.");
            return parsed;
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

        private sealed class EditableListLayout
        {
            public XElement Grid { get; set; }
            public XElement Header { get; set; }
            public XElement Display { get; set; }
            public XElement Footer { get; set; }
            public XElement Edit { get; set; }
            public IDictionary<string, XElement> ControlDefinitions { get; set; }
        }
    }
}
