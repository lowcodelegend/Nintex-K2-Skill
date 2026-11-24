using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class FormRuleReferenceDefinition
    {
        public static void ScopeValidationSources(XElement member, string instance)
        {
            foreach (var source in member.Descendants().Where(IsViewSource))
                if (string.IsNullOrWhiteSpace((string)source.Attribute("SourceInstanceID")))
                    source.SetAttributeValue("SourceInstanceID", instance);
        }

        public static void Verify(string definition, string name)
        {
            var form = XDocument.Parse(definition).Descendants().Single(x => x.Name.LocalName == "Form");
            var instances = form.Descendants().Where(x => x.Name.LocalName == "Item" && x.Attribute("ViewID") != null)
                .GroupBy(x => (string)x.Attribute("ID"), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => (string)x.First().Attribute("ViewID"), StringComparer.OrdinalIgnoreCase);
            var localControls = new HashSet<string>(form.Elements().Where(x => x.Name.LocalName == "Controls")
                .SelectMany(x => x.Elements()).Select(x => (string)x.Attribute("ID")), StringComparer.OrdinalIgnoreCase);
            foreach (var action in form.Descendants().Where(x => x.Name.LocalName == "Action" &&
                x.Attribute("SubFormID") == null &&
                (!string.Equals((string)x.Attribute("IsInherited"), "True", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals((string)x.Attribute("IsExtended"), "True", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(Property(x, "DesignTemplate"), "SetControlProperties", StringComparison.OrdinalIgnoreCase)))
            {
                var scopedInstances = action.Descendants().Where(x => x.Name.LocalName == "Parameter")
                    .Select(x => (string)x.Attribute("TargetInstanceID"))
                    .Concat(new[] { (string)action.Attribute("InstanceID") })
                    .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (scopedInstances.Count == 0) continue; // A Form-owned Panel/control has no View context.
                string view;
                if (scopedInstances.Count != 1 || !instances.TryGetValue(scopedInstances[0], out view))
                    throw new CliException("K2 Form '" + name + "' property action has an unknown or conflicting View instance.");
                if (!string.Equals(Property(action, "ViewID"), view, StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrWhiteSpace(Property(action, "FormID")))
                    throw new CliException("K2 Form '" + name + "' SetControlProperties action for a View control must identify its owning ViewID, not FormID.");
            }
            foreach (var member in form.Elements().Where(x => x.Name.LocalName == "ValidationGroups")
                .SelectMany(x => x.Descendants()).Where(x => x.Name.LocalName == "ValidationGroupControl" &&
                    x.Attribute("InstanceID") != null && x.Attribute("SubFormID") == null))
            {
                var instance = (string)member.Attribute("InstanceID");
                if (!instances.ContainsKey(instance))
                    throw new CliException("K2 Form '" + name + "' validation member has an unknown View instance.");
                foreach (var source in member.Descendants().Where(IsViewSource))
                {
                    var sourceInstance = (string)source.Attribute("SourceInstanceID");
                    var sourceControl = (string)source.Attribute(
                        string.Equals((string)source.Attribute("SourceType"), "ControlProperty", StringComparison.OrdinalIgnoreCase)
                            ? "SourcePath" : "SourceID");
                    if (string.IsNullOrWhiteSpace(sourceInstance) && localControls.Contains(sourceControl)) continue;
                    if (string.IsNullOrWhiteSpace(sourceInstance) || !instances.ContainsKey(sourceInstance))
                        throw new CliException("K2 Form '" + name + "' validation expression requires an explicit valid SourceInstanceID for its View source. The validation member InstanceID is not inherited by expressions.");
                }
            }
        }

        private static bool IsViewSource(XElement element)
        {
            return element.Name.LocalName == "Item" && new[] { "Control", "ControlProperty", "ViewField" }
                .Contains((string)element.Attribute("SourceType"), StringComparer.OrdinalIgnoreCase);
        }

        private static string Property(XElement element, string name)
        {
            var property = element.Elements().Where(x => x.Name.LocalName == "Properties").SelectMany(x => x.Elements())
                .SingleOrDefault(x => x.Elements().Any(y => y.Name.LocalName == "Name" && y.Value == name));
            return property == null ? null : (string)property.Elements().FirstOrDefault(x => x.Name.LocalName == "Value");
        }
    }
}
