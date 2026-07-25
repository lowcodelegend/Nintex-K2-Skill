using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class FieldValidationDefinitionXml
    {
        internal const string GroupName = "K2Skills.FieldValidation";

        internal static bool RequiresValidationPattern(FieldValidationDefinition validation)
        {
            return validation.MinLength.HasValue ||
                   !string.IsNullOrWhiteSpace(validation.Pattern) ||
                   !string.IsNullOrWhiteSpace(validation.Format);
        }

        internal static string BuildPattern(FieldValidationDefinition validation)
        {
            if (!RequiresValidationPattern(validation)) return null;

            var body = FormatPattern(validation.Format);
            if (string.IsNullOrWhiteSpace(body)) body = @"[\s\S]*";
            if (!string.IsNullOrWhiteSpace(validation.Pattern))
                body = string.IsNullOrWhiteSpace(validation.Format)
                    ? validation.Pattern
                    : "(?=(?:" + body + ")$)(?:" + validation.Pattern + ")";

            var minimum = validation.MinLength.HasValue ? validation.MinLength.Value : 0;
            var maximum = validation.MaxLength.HasValue ? validation.MaxLength.Value.ToString() : string.Empty;
            var length = "(?=[\\s\\S]{" + minimum + "," + maximum + "}$)";
            return "^" + length + "(?:" + body + ")$";
        }

        internal static void ValidatePatternSyntax(FieldValidationDefinition validation)
        {
            if (!RequiresValidationPattern(validation)) return;
            try { new Regex(BuildPattern(validation), RegexOptions.ECMAScript); }
            catch (ArgumentException ex)
            {
                throw new CliException("Validation pattern is invalid for property '" + validation.Property + "': " + ex.Message);
            }
        }

        private static string FormatPattern(string format)
        {
            switch ((format ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "email": return @"[^\s@]+@[^\s@]+\.[^\s@]+";
                case "phone": return @"\+?[0-9][0-9 ()-]{5,24}";
                case "url": return @"https?://[^\s]+";
                case "guid": return @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";
                default: return null;
            }
        }

        internal static void Apply(XDocument document, ViewDefinition view)
        {
            foreach (var validation in view.Validations)
            {
                var control = ViewPresentationDefinition.FindEditableFieldControl(document, view, validation.Property);
                var type = (string)control.Attribute("Type") ?? string.Empty;
                var textConstraint = validation.MinLength.HasValue || validation.MaxLength.HasValue ||
                    !string.IsNullOrWhiteSpace(validation.Pattern) || !string.IsNullOrWhiteSpace(validation.Format);
                if (textConstraint && !new[] { "TextBox", "TextArea" }.Contains(type, StringComparer.OrdinalIgnoreCase))
                    throw new CliException("View '" + view.Name + "' text validation property '" + validation.Property +
                        "' uses control type '" + type + "'; expected TextBox or TextArea.");
                if (validation.MustBeTrue && !string.Equals(type, "CheckBox", StringComparison.OrdinalIgnoreCase))
                    throw new CliException("View '" + view.Name + "' mustBeTrue property '" + validation.Property +
                        "' uses control type '" + type + "'; expected CheckBox.");

                var properties = EnsureProperties(control);
                if (validation.MaxLength.HasValue)
                    SetProperty(properties, "MaxLength", validation.MaxLength.Value.ToString(), validation.MaxLength.Value.ToString());
                if (!string.IsNullOrWhiteSpace(validation.Message) &&
                    new[] { "TextBox", "TextArea" }.Contains(type, StringComparer.OrdinalIgnoreCase))
                    SetProperty(properties, "ValidationMessage", validation.Message, validation.Message);
                if (RequiresValidationPattern(validation))
                {
                    if (validation.ValidationPatternGuid == Guid.Empty || string.IsNullOrWhiteSpace(validation.ValidationPatternName))
                        throw new CliException("View '" + view.Name + "' validation pattern was not resolved for property '" + validation.Property + "'.");
                    SetProperty(properties, "ValidationPattern", validation.ValidationPatternGuid.ToString(), validation.ValidationPatternName);
                }
            }

            var members = view.RequiredProperties.Concat(view.Validations.Select(x => x.Property))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var methods = EffectiveValidationMethods(view);
            if (members.Count == 0 || methods.Count == 0) return;

            var root = document.Descendants().Single(x => x.Name.LocalName == "View");
            var ns = root.Name.Namespace;
            var groups = root.Elements().FirstOrDefault(x => x.Name.LocalName == "ValidationGroups");
            if (groups == null)
            {
                groups = new XElement(ns + "ValidationGroups");
                var events = root.Elements().FirstOrDefault(x => x.Name.LocalName == "Events");
                if (events == null) root.Add(groups); else events.AddBeforeSelf(groups);
            }
            foreach (var existing in groups.Elements().Where(x => x.Name.LocalName == "ValidationGroup" &&
                string.Equals(ChildValue(x, "Name"), GroupName, StringComparison.Ordinal)).ToList())
                existing.Remove();

            var groupId = NewId();
            var controls = new XElement(ns + "ValidationGroupControls");
            foreach (var property in members)
            {
                var control = ViewPresentationDefinition.FindEditableFieldControl(document, view, property);
                var validation = view.Validations.SingleOrDefault(x =>
                    string.Equals(x.Property, property, StringComparison.OrdinalIgnoreCase));
                var member = new XElement(ns + "ValidationGroupControl",
                    new XAttribute("ID", NewId()),
                    new XAttribute("ControlID", (string)control.Attribute("ID")),
                    new XAttribute("IsRequired", view.RequiredProperties.Contains(property, StringComparer.OrdinalIgnoreCase) ? "True" : "False"),
                    new XAttribute("ControlName", ChildValue(control, "Name") ?? property),
                    new XAttribute("ControlDisplayName", ChildValue(control, "DisplayName") ?? property));
                if (validation != null && (validation.MustBeTrue ||
                    validation.Minimum.HasValue || validation.Maximum.HasValue))
                    member.Add(BuildCondition(ns, control, validation,
                        view.RequiredProperties.Contains(property, StringComparer.OrdinalIgnoreCase)));
                controls.Add(member);
            }
            groups.Add(new XElement(ns + "ValidationGroup", new XAttribute("ID", groupId),
                new XElement(ns + "Name", GroupName), controls));

            foreach (var action in document.Descendants().Where(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                methods.Contains(ReadProperty(x, "Method"), StringComparer.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(ReadProperty(x, "ControlID"))).ToList())
            {
                var validate = BuildValidateAction(ns, root, groupId);
                var itemState = (string)action.Attribute("ItemState");
                if (!string.IsNullOrWhiteSpace(itemState)) validate.SetAttributeValue("ItemState", itemState);
                action.AddBeforeSelf(validate);
            }
        }

        internal static void Verify(XDocument document, ViewDefinition view)
        {
            foreach (var validation in view.Validations)
            {
                var control = ViewPresentationDefinition.FindEditableFieldControl(document, view, validation.Property);
                if (validation.MaxLength.HasValue &&
                    !string.Equals(PropertyValue(control, "MaxLength"), validation.MaxLength.Value.ToString(), StringComparison.Ordinal))
                    throw new CliException("View '" + view.Name + "' validation maxLength is not applied to property '" + validation.Property + "'.");
                if (RequiresValidationPattern(validation))
                {
                    if (!string.Equals(PropertyValue(control, "ValidationPattern"), validation.ValidationPatternGuid.ToString(), StringComparison.OrdinalIgnoreCase))
                        throw new CliException("View '" + view.Name + "' validation pattern is not applied to property '" + validation.Property + "'.");
                    if (!string.Equals(PropertyValue(control, "ValidationMessage"), validation.Message, StringComparison.Ordinal))
                        throw new CliException("View '" + view.Name + "' validation message is not applied to property '" + validation.Property + "'.");
                }
            }

            var members = view.RequiredProperties.Concat(view.Validations.Select(x => x.Property))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var methods = EffectiveValidationMethods(view);
            if (members.Count == 0 || methods.Count == 0) return;
            var root = document.Descendants().Single(x => x.Name.LocalName == "View");
            var group = root.Elements().Where(x => x.Name.LocalName == "ValidationGroups").SelectMany(x => x.Elements())
                .SingleOrDefault(x => x.Name.LocalName == "ValidationGroup" &&
                    string.Equals(ChildValue(x, "Name"), GroupName, StringComparison.Ordinal));
            if (group == null) throw new CliException("View '" + view.Name + "' has no field-validation group.");
            var groupId = (string)group.Attribute("ID");
            foreach (var property in members)
            {
                var control = ViewPresentationDefinition.FindEditableFieldControl(document, view, property);
                var member = group.Descendants().SingleOrDefault(x => x.Name.LocalName == "ValidationGroupControl" &&
                    string.Equals((string)x.Attribute("ControlID"), (string)control.Attribute("ID"), StringComparison.OrdinalIgnoreCase));
                if (member == null)
                    throw new CliException("View '" + view.Name + "' field-validation group omits property '" + property + "'.");
                var validation = view.Validations.SingleOrDefault(x =>
                    string.Equals(x.Property, property, StringComparison.OrdinalIgnoreCase));
                if (validation != null && validation.MustBeTrue && !HasMustBeTrueCondition(member, control))
                    throw new CliException("View '" + view.Name + "' mustBeTrue condition is missing for property '" + property + "'.");
                if (validation != null && (validation.Minimum.HasValue || validation.Maximum.HasValue) &&
                    !HasNumericCondition(member, control, validation))
                    throw new CliException("View '" + view.Name + "' numeric range condition is missing for property '" + property + "'.");
            }
            foreach (var action in document.Descendants().Where(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                methods.Contains(ReadProperty(x, "Method"), StringComparer.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(ReadProperty(x, "ControlID"))))
            {
                var previous = action.ElementsBeforeSelf().LastOrDefault(x => x.Name.LocalName == "Action");
                if (previous == null || !string.Equals((string)previous.Attribute("Type"), "Validate", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ReadProperty(previous, "GroupID"), groupId, StringComparison.OrdinalIgnoreCase))
                    throw new CliException("View '" + view.Name + "' does not validate field constraints immediately before method '" +
                        ReadProperty(action, "Method") + "'.");
            }
        }

        internal static XElement BuildCondition(XNamespace ns, XElement control, FieldValidationDefinition validation,
            bool required)
        {
            var name = ChildValue(control, "Name") ?? string.Empty;
            var displayName = ChildValue(control, "DisplayName") ?? name;
            if (validation.MustBeTrue)
                return new XElement(ns + "Condition",
                new XElement(ns + "Equals",
                    new XElement(ns + "Item", new XAttribute("SourceType", "Control"),
                        new XAttribute("SourceName", name), new XAttribute("SourceDisplayName", displayName),
                        new XAttribute("SourceID", (string)control.Attribute("ID")), new XAttribute("DataType", "Text")),
                    new XElement(ns + "Item", new XAttribute("SourceType", "Value"),
                        new XAttribute("DataType", "Text"), "true")));

            var comparisons = new List<XElement>();
            if (validation.Minimum.HasValue)
                comparisons.Add(BuildNumericComparison(ns, control, validation.Minimum.Value,
                    validation.ExclusiveMinimum ? "GreaterThan" : "LessThan", !validation.ExclusiveMinimum));
            if (validation.Maximum.HasValue)
                comparisons.Add(BuildNumericComparison(ns, control, validation.Maximum.Value,
                    validation.ExclusiveMaximum ? "LessThan" : "GreaterThan", !validation.ExclusiveMaximum));
            XElement expression = comparisons[0];
            if (comparisons.Count == 2) expression = new XElement(ns + "And", comparisons[0], comparisons[1]);
            if (!required)
                expression = new XElement(ns + "Or",
                    new XElement(ns + "IsBlank",
                        new XElement(ns + "Item", new XAttribute("SourceType", "Control"),
                            new XAttribute("SourceName", name), new XAttribute("SourceDisplayName", displayName),
                            new XAttribute("SourceID", (string)control.Attribute("ID")), new XAttribute("DataType", "Decimal"))),
                    expression);
            return new XElement(ns + "Condition", expression);
        }

        private static XElement BuildNumericComparison(XNamespace ns, XElement control, decimal value,
            string operatorName, bool negate)
        {
            var name = ChildValue(control, "Name") ?? string.Empty;
            var displayName = ChildValue(control, "DisplayName") ?? name;
            var comparison = new XElement(ns + operatorName,
                new XElement(ns + "Item", new XAttribute("SourceType", "Control"),
                    new XAttribute("SourceName", name), new XAttribute("SourceDisplayName", displayName),
                    new XAttribute("SourceID", (string)control.Attribute("ID")), new XAttribute("DataType", "Decimal")),
                new XElement(ns + "Item", new XAttribute("SourceType", "Value"),
                    new XAttribute("DataType", "Decimal"), value.ToString(CultureInfo.InvariantCulture)));
            return negate ? new XElement(ns + "Not", comparison) : comparison;
        }

        private static bool HasMustBeTrueCondition(XElement member, XElement control)
        {
            return member.Descendants().Any(x => x.Name.LocalName == "Equals" &&
                x.Elements().Any(i => i.Name.LocalName == "Item" &&
                    string.Equals((string)i.Attribute("SourceType"), "Control", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)i.Attribute("SourceID"), (string)control.Attribute("ID"), StringComparison.OrdinalIgnoreCase)) &&
                x.Elements().Any(i => i.Name.LocalName == "Item" &&
                    string.Equals((string)i.Attribute("SourceType"), "Value", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(i.Value, "true", StringComparison.OrdinalIgnoreCase)));
        }

        private static bool HasNumericCondition(XElement member, XElement control,
            FieldValidationDefinition validation)
        {
            var condition = member.Elements().SingleOrDefault(x => x.Name.LocalName == "Condition");
            if (condition == null) return false;
            if (!condition.Descendants().Any(x => x.Name.LocalName == "Item" &&
                string.Equals((string)x.Attribute("SourceType"), "Control", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("SourceID"), (string)control.Attribute("ID"), StringComparison.OrdinalIgnoreCase)))
                return false;
            if (validation.Minimum.HasValue && !condition.Descendants().Any(x =>
                x.Name.LocalName == (validation.ExclusiveMinimum ? "GreaterThan" : "LessThan") &&
                x.Elements().Any(i => i.Name.LocalName == "Item" &&
                    string.Equals((string)i.Attribute("SourceType"), "Value", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(i.Value, validation.Minimum.Value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))))
                return false;
            if (validation.Maximum.HasValue && !condition.Descendants().Any(x =>
                x.Name.LocalName == (validation.ExclusiveMaximum ? "LessThan" : "GreaterThan") &&
                x.Elements().Any(i => i.Name.LocalName == "Item" &&
                    string.Equals((string)i.Attribute("SourceType"), "Value", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(i.Value, validation.Maximum.Value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))))
                return false;
            return true;
        }

        private static List<string> EffectiveValidationMethods(ViewDefinition view)
        {
            if (view.ValidationMethods.Count > 0) return view.ValidationMethods;
            return view.Methods.Where(x => new[] { "Create", "Update", "Save", "Submit" }
                .Contains(x, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        private static XElement BuildValidateAction(XNamespace ns, XElement view, string groupId)
        {
            var viewId = (string)view.Attribute("ID");
            var viewName = ChildValue(view, "Name") ?? string.Empty;
            return new XElement(ns + "Action", new XAttribute("ID", NewId()),
                new XAttribute("DefinitionID", NewId()), new XAttribute("Type", "Validate"),
                new XAttribute("ExecutionType", "Synchronous"),
                new XElement(ns + "Properties",
                    EventProperty(ns, "ViewID", viewId, viewName),
                    EventProperty(ns, "GroupID", groupId, GroupName)));
        }

        private static XElement EventProperty(XNamespace ns, string name, string value, string display)
        {
            return new XElement(ns + "Property", new XElement(ns + "Name", name),
                new XElement(ns + "DisplayValue", display), new XElement(ns + "NameValue", display),
                new XElement(ns + "Value", value));
        }

        private static XElement EnsureProperties(XElement control)
        {
            var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
            if (properties != null) return properties;
            properties = new XElement(control.Name.Namespace + "Properties");
            control.Add(properties);
            return properties;
        }

        private static void SetProperty(XElement properties, string name, string value, string displayValue)
        {
            foreach (var old in properties.Elements().Where(x => x.Name.LocalName == "Property" &&
                string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase)).ToList())
                old.Remove();
            var ns = properties.Name.Namespace;
            properties.Add(new XElement(ns + "Property", new XElement(ns + "Name", name),
                new XElement(ns + "DisplayValue", displayValue), new XElement(ns + "Value", value)));
        }

        private static string PropertyValue(XElement control, string name)
        {
            var property = control.Elements().Where(x => x.Name.LocalName == "Properties").SelectMany(x => x.Elements())
                .FirstOrDefault(x => x.Name.LocalName == "Property" &&
                    string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
            return property == null ? null : ChildValue(property, "Value");
        }

        private static string ReadProperty(XElement action, string name)
        {
            var property = action.Elements().Where(x => x.Name.LocalName == "Properties").SelectMany(x => x.Elements())
                .FirstOrDefault(x => x.Name.LocalName == "Property" &&
                    string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
            return property == null ? null : ChildValue(property, "Value");
        }

        private static string ChildValue(XElement parent, string name)
        {
            var child = parent.Elements().FirstOrDefault(x => x.Name.LocalName == name);
            return child == null ? null : child.Value;
        }

        private static string NewId() { return Guid.NewGuid().ToString(); }
    }
}
