using System;
using System.Linq;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class ViewLifecycleLayoutDefinition
    {
        public static string Apply(string xml, ViewDefinition view)
        {
            if (view.LifecycleTrackers == null || view.LifecycleTrackers.Count == 0) return xml;
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            foreach (var tracker in view.LifecycleTrackers)
            {
                var control = FindBoundControl(document, tracker.Property);
                if (control == null) throw new CliException("Generated View '" + view.Name + "' has no control bound to lifecycle property '" + tracker.Property + "'.");
                control.SetAttributeValue("Type", "Progress");
                SetChild(control, "Name", tracker.Name);
                SetChild(control, "DisplayName", tracker.Name);
                var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
                if (properties == null) { properties = new XElement(control.Name.Namespace + "Properties"); control.Add(properties); }
                properties.RemoveNodes();
                properties.Add(Property(control.Name.Namespace, "ControlName", tracker.Name));
                properties.Add(Property(control.Name.Namespace, "Width", "100%"));
                properties.Add(Property(control.Name.Namespace, "DataSourceType", "Static"));
                var items = tracker.Stages.Select(x => new { value = x.Code, display = x.Label, isDefault = false }).ToArray();
                var display = string.Join("; ", tracker.Stages.Select(x => x.Label).ToArray());
                properties.Add(Property(control.Name.Namespace, "FixedListItems", new JavaScriptSerializer().Serialize(items), display));
                string defaultValue;
                if (view.DefaultValues != null && view.DefaultValues.TryGetValue(tracker.Property, out defaultValue)) properties.Add(Property(control.Name.Namespace, "Text", defaultValue));
                properties.Add(Property(control.Name.Namespace, "IsReadOnly", "true"));
                properties.Add(Property(control.Name.Namespace, "IsEnabled", "false"));
            }
            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static void Verify(string xml, ViewDefinition view)
        {
            if (view.LifecycleTrackers == null || view.LifecycleTrackers.Count == 0) return;
            var document = XDocument.Parse(xml);
            foreach (var tracker in view.LifecycleTrackers)
            {
                var control = FindBoundControl(document, tracker.Property);
                if (control == null || !string.Equals((string)control.Attribute("Type"), "Progress", StringComparison.OrdinalIgnoreCase))
                    throw new CliException("K2 View '" + view.Name + "' is missing lifecycle tracker '" + tracker.Name + "'.");
                var items = PropertyValue(control, "FixedListItems") ?? string.Empty;
                foreach (var stage in tracker.Stages)
                    if (items.IndexOf("\"value\":" + new JavaScriptSerializer().Serialize(stage.Code), StringComparison.Ordinal) < 0 || items.IndexOf("\"display\":" + new JavaScriptSerializer().Serialize(stage.Label), StringComparison.Ordinal) < 0)
                        throw new CliException("K2 View '" + view.Name + "' lifecycle tracker omits stage '" + stage.Code + "'.");
                if (!string.Equals(PropertyValue(control, "IsReadOnly"), "true", StringComparison.OrdinalIgnoreCase) || !string.Equals(PropertyValue(control, "IsEnabled"), "false", StringComparison.OrdinalIgnoreCase))
                    throw new CliException("K2 View '" + view.Name + "' lifecycle tracker must be read-only and disabled.");
            }
        }

        private static XElement FindBoundControl(XDocument document, string property)
        {
            var field = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Field" && string.Equals(Child(x, "FieldName"), property, StringComparison.OrdinalIgnoreCase));
            var fieldId = field == null ? null : (string)field.Attribute("ID");
            return document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Control" && ((!string.IsNullOrEmpty(fieldId) && string.Equals((string)x.Attribute("FieldID"), fieldId, StringComparison.OrdinalIgnoreCase)) || string.Equals(Child(x, "Name"), property, StringComparison.OrdinalIgnoreCase)));
        }

        private static XElement Property(XNamespace ns, string name, string value, string display = null)
        {
            return new XElement(ns + "Property", new XElement(ns + "Name", name), new XElement(ns + "DisplayValue", display ?? value), new XElement(ns + "NameValue", display ?? value), new XElement(ns + "Value", value));
        }
        private static string PropertyValue(XElement control, string name) { var p = control.Descendants().FirstOrDefault(x => x.Name.LocalName == "Property" && string.Equals(Child(x, "Name"), name, StringComparison.OrdinalIgnoreCase)); return p == null ? null : Child(p, "Value"); }
        private static void SetChild(XElement element, string name, string value) { var child = element.Elements().FirstOrDefault(x => x.Name.LocalName == name); if (child == null) { child = new XElement(element.Name.Namespace + name); element.AddFirst(child); } child.Value = value; }
        private static string Child(XElement element, string name) { var child = element.Elements().FirstOrDefault(x => x.Name.LocalName == name); return child == null ? null : child.Value; }
    }
}
