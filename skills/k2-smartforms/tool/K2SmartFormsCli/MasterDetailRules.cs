using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SourceCode.Forms.Management;

namespace K2SmartFormsCli
{
    internal sealed class ResolvedMasterDetailRules
    {
        public MasterDetailFormDefinition Definition { get; set; }
        public Guid MasterViewGuid { get; set; }
        public string MasterViewName { get; set; }
        public ResolvedViewField MasterKey { get; set; }
        public string MasterCreateAction { get; set; }
        public string MasterUpdateAction { get; set; }
        public ResolvedViewEvent MasterCreateEvent { get; set; }
        public ResolvedViewEvent MasterUpdateEvent { get; set; }
        public List<ResolvedMasterDetailChild> Details { get; set; }
        public Guid ReviewViewGuid { get; set; }
        public string ReviewViewName { get; set; }
        public ResolvedViewField ReviewKey { get; set; }
        public string ReviewReadAction { get; set; }
        public ResolvedViewEvent ReviewReadEvent { get; set; }
        public string ReviewParameterName { get; set; }
        public List<ResolvedRequiredControl> RequiredControls { get; set; }

        public static ResolvedMasterDetailRules Resolve(FormsManager manager, FormDefinition form, IEnumerable<ViewDefinition> views)
        {
            if (form.MasterDetail == null) return null;
            var masterInfo = manager.GetView(form.MasterDetail.MasterView);
            var masterDocument = XDocument.Parse(manager.GetViewDefinition(masterInfo.Guid));
            var masterDefinition = views.Single(x => string.Equals(x.Name, form.MasterDetail.MasterView, StringComparison.OrdinalIgnoreCase));
            var result = new ResolvedMasterDetailRules
            {
                Definition = form.MasterDetail,
                MasterViewGuid = masterInfo.Guid,
                MasterViewName = masterInfo.Name,
                MasterKey = ResolveField(masterDocument, form.MasterDetail.MasterKeyProperty, form.MasterDetail.MasterView),
                MasterCreateAction = ResolveAction(masterDocument, form.MasterDetail.MasterCreateMethod, null, form.MasterDetail.MasterView),
                MasterUpdateAction = ResolveAction(masterDocument, form.MasterDetail.MasterUpdateMethod, null, form.MasterDetail.MasterView),
                MasterCreateEvent = ResolveNamedEvent(masterDocument, MasterCreateRuleName(form.MasterDetail.MasterKeyProperty), form.MasterDetail.MasterView),
                MasterUpdateEvent = ResolveNamedEvent(masterDocument, MasterUpdateRuleName(form.MasterDetail.MasterKeyProperty), form.MasterDetail.MasterView),
                Details = new List<ResolvedMasterDetailChild>(),
                RequiredControls = masterDefinition.RequiredProperties.Select(x => ResolveRequiredControl(masterDocument, x, form.MasterDetail.MasterView)).ToList()
            };
            foreach (var child in form.MasterDetail.Details)
            {
                var info = manager.GetView(child.View);
                var document = XDocument.Parse(manager.GetViewDefinition(info.Guid));
                result.Details.Add(new ResolvedMasterDetailChild
                {
                    Definition = child,
                    ViewGuid = info.Guid,
                    ViewName = info.Name,
                    ViewDisplayName = info.DisplayName,
                    CreateAction = ResolveAction(document, child.CreateMethod, "Added", child.View),
                    UpdateAction = ResolveAction(document, child.UpdateMethod, "Changed", child.View),
                    DeleteAction = ResolveAction(document, child.DeleteMethod, "Removed", child.View),
                    ListAction = ResolveOptionalAction(document, child.ListMethod, null),
                    SaveEvent = ResolveCommonOwningEvent(document, child, child.View),
                    LoadEvent = ResolveNamedEvent(document, LoadRuleName(child.ForeignKeyProperty), child.View),
                    KeyParameterName = child.ForeignKeyProperty
                });
            }
            if (form.MasterDetail.Review != null)
            {
                var reviewInfo = manager.GetView(form.MasterDetail.Review.View);
                var reviewDocument = XDocument.Parse(manager.GetViewDefinition(reviewInfo.Guid));
                result.ReviewViewGuid = reviewInfo.Guid;
                result.ReviewViewName = reviewInfo.Name;
                result.ReviewKey = ResolveField(reviewDocument, form.MasterDetail.Review.KeyProperty, form.MasterDetail.Review.View);
                result.ReviewReadAction = ResolveAction(reviewDocument, form.MasterDetail.Review.ReadMethod, null, form.MasterDetail.Review.View);
                result.ReviewReadEvent = ResolveNamedEvent(reviewDocument, ReviewRuleName(form.MasterDetail.Review.KeyProperty), form.MasterDetail.Review.View);
                result.ReviewParameterName = form.MasterDetail.Review.KeyProperty;
            }
            return result;
        }

        private static ResolvedViewEvent ResolveOwningEvent(XDocument document, string method, string state, string viewName)
        {
            var action = ResolveOptionalActionElement(document, method, state);
            if (action == null)
                throw new CliException("View '" + viewName + "' has no generated " + method + " action" +
                    (state == null ? "." : " for items in state " + state + "."));
            var owner = action.Ancestors().FirstOrDefault(x => x.Name.LocalName == "Event");
            return ResolveEvent(owner, viewName, method);
        }

        private static ResolvedViewEvent ResolveCommonOwningEvent(XDocument document, MasterDetailChildDefinition child, string viewName)
        {
            var events = new[]
            {
                ResolveOwningEvent(document, child.CreateMethod, "Added", viewName),
                ResolveOwningEvent(document, child.UpdateMethod, "Changed", viewName),
                ResolveOwningEvent(document, child.DeleteMethod, "Removed", viewName)
            };
            if (events.Select(x => x.DefinitionId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1)
                throw new CliException("Editable detail View '" + viewName +
                    "' does not have one View-owned Save rule containing Added, Changed, and Removed persistence actions.");
            return events[0];
        }

        private static ResolvedViewEvent ResolveNamedEvent(XDocument document, string ruleName, string viewName)
        {
            var matches = document.Descendants().Where(x => x.Name.LocalName == "Event" &&
                string.Equals(ReadEventRuleName(x), ruleName, StringComparison.Ordinal)).ToList();
            if (matches.Count != 1)
                throw new CliException("View '" + viewName + "' must have exactly one generated rule '" + ruleName +
                    "'; found " + matches.Count + ".");
            if (!string.Equals((string)matches[0].Attribute("SourceType"), "Rule", StringComparison.OrdinalIgnoreCase))
                throw new CliException("View '" + viewName + "' generated event '" + ruleName +
                    "' is not a callable View-owned custom rule.");
            return ResolveEvent(matches[0], viewName, ruleName);
        }

        private static ResolvedViewEvent ResolveEvent(XElement owner, string viewName, string purpose)
        {
            if (owner == null) throw new CliException("View '" + viewName + "' action '" + purpose + "' is not owned by a View event.");
            var definitionId = RequiredAttribute(owner, "DefinitionID", viewName + "." + purpose + " event");
            Guid parsed;
            if (!Guid.TryParse(definitionId, out parsed))
                throw new CliException("View '" + viewName + "' event '" + purpose + "' has an invalid DefinitionID.");
            return new ResolvedViewEvent
            {
                DefinitionId = definitionId,
                DisplayName = ReadEventRuleName(owner) ?? purpose
            };
        }

        private static string ReadEventRuleName(XElement owner)
        {
            var properties = owner.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
            if (properties == null) return null;
            var property = properties.Elements().FirstOrDefault(x => x.Name.LocalName == "Property" &&
                (string.Equals(ChildValue(x, "Name"), "RuleName", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(ChildValue(x, "Name"), "RuleFriendlyName", StringComparison.OrdinalIgnoreCase)));
            return property == null ? null : ChildValue(property, "Value");
        }

        private static ResolvedRequiredControl ResolveRequiredControl(XDocument document, string property, string viewName)
        {
            var field = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Field" &&
                string.Equals(ChildValue(x, "FieldName"), property, StringComparison.OrdinalIgnoreCase));
            if (field == null) throw new CliException("Master View '" + viewName + "' has no field for required property '" + property + "'.");
            var fieldId = (string)field.Attribute("ID");
            var controls = document.Descendants().Where(x => x.Name.LocalName == "Control" &&
                string.Equals((string)x.Attribute("FieldID"), fieldId, StringComparison.OrdinalIgnoreCase) &&
                !new[] { "Label", "DataLabel", "ListDisplay" }.Contains((string)x.Attribute("Type"), StringComparer.OrdinalIgnoreCase)).ToList();
            if (controls.Count != 1) throw new CliException("Master View '" + viewName + "' required property '" + property + "' must have exactly one editable control.");
            return new ResolvedRequiredControl
            {
                Property = property,
                ControlId = (string)controls[0].Attribute("ID"),
                ControlName = ChildValue(controls[0], "Name") ?? property,
                ControlDisplayName = ChildValue(controls[0], "DisplayName") ?? property
            };
        }

        private static ResolvedViewField ResolveField(XDocument document, string property, string viewName)
        {
            var field = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Field" &&
                (string.Equals(ChildValue(x, "FieldName"), property, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(ChildValue(x, "Name"), property, StringComparison.OrdinalIgnoreCase)));
            if (field == null) throw new CliException("Master-detail view '" + viewName + "' has no generated field for property '" + property + "'.");
            return new ResolvedViewField
            {
                Id = RequiredAttribute(field, "ID", viewName + "." + property),
                Name = ChildValue(field, "Name") ?? property,
                DisplayName = ChildValue(field, "FieldDisplayName") ?? property,
                DataType = NormalizeConditionDataType((string)field.Attribute("DataType") ?? "Text")
            };
        }

        internal static string NormalizeConditionDataType(string value)
        {
            if (string.Equals(value, "AutoNumber", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Autonumber", StringComparison.OrdinalIgnoreCase)) return "Number";
            if (string.Equals(value, "AutoGuid", StringComparison.OrdinalIgnoreCase)) return "Guid";
            return value;
        }

        private static string ResolveAction(XDocument document, string method, string state, string viewName)
        {
            var result = ResolveOptionalAction(document, method, state);
            if (result == null) throw new CliException("Editable detail view '" + viewName + "' has no generated " + method + " action for items in state " + state + ".");
            return result;
        }

        private static string ResolveOptionalAction(XDocument document, string method, string state)
        {
            var action = ResolveOptionalActionElement(document, method, state);
            return action == null ? null : action.ToString(SaveOptions.DisableFormatting);
        }

        private static XElement ResolveOptionalActionElement(XDocument document, string method, string state)
        {
            return document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadProperty(x, "Method"), method, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(ReadProperty(x, "ControlID")) &&
                (state == null ? x.Attribute("ItemState") == null : string.Equals((string)x.Attribute("ItemState"), state, StringComparison.OrdinalIgnoreCase)));
        }

        internal static string LoadRuleName(string property) { return "K2Skills.MasterDetail.Load." + property; }
        internal static string ReviewRuleName(string property) { return "K2Skills.MasterDetail.Read." + property; }
        internal static string MasterCreateRuleName(string property) { return "K2Skills.MasterDetail.Create." + property; }
        internal static string MasterUpdateRuleName(string property) { return "K2Skills.MasterDetail.Update." + property; }

        private static string ReadProperty(XElement action, string name)
        {
            var property = action.Descendants().FirstOrDefault(x => x.Name.LocalName == "Property" && string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
            return property == null ? null : ChildValue(property, "Value");
        }

        private static string ChildValue(XElement parent, string name)
        {
            var child = parent.Elements().FirstOrDefault(x => x.Name.LocalName == name);
            return child == null ? null : child.Value;
        }

        private static string RequiredAttribute(XElement element, string name, string owner)
        {
            var value = (string)element.Attribute(name);
            if (string.IsNullOrWhiteSpace(value)) throw new CliException("Generated definition is missing " + name + " for " + owner + ".");
            return value;
        }
    }

    internal sealed class ResolvedMasterDetailChild
    {
        public MasterDetailChildDefinition Definition { get; set; }
        public Guid ViewGuid { get; set; }
        public string ViewName { get; set; }
        public string ViewDisplayName { get; set; }
        public string CreateAction { get; set; }
        public string UpdateAction { get; set; }
        public string DeleteAction { get; set; }
        public string ListAction { get; set; }
        public ResolvedViewEvent SaveEvent { get; set; }
        public ResolvedViewEvent LoadEvent { get; set; }
        public string KeyParameterName { get; set; }
    }

    internal sealed class ResolvedViewEvent
    {
        public string DefinitionId { get; set; }
        public string DisplayName { get; set; }
    }

    internal sealed class ResolvedViewField
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string DataType { get; set; }
    }

    internal sealed class ResolvedRequiredControl
    {
        public string Property { get; set; }
        public string ControlId { get; set; }
        public string ControlName { get; set; }
        public string ControlDisplayName { get; set; }
    }

    internal static class MasterDetailRules
    {
        public static string ConfigureViewRuleSeams(string xml, string viewName,
            IEnumerable<MasterDetailFormDefinition> masterRelationships,
            IEnumerable<MasterDetailChildDefinition> detailRelationships,
            IEnumerable<MasterDetailReviewDefinition> reviewRelationships)
        {
            var masters = (masterRelationships ?? Enumerable.Empty<MasterDetailFormDefinition>())
                .GroupBy(x => string.Join("|", new[] { x.MasterKeyProperty, x.MasterCreateMethod, x.MasterUpdateMethod }),
                    StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
            var details = (detailRelationships ?? Enumerable.Empty<MasterDetailChildDefinition>())
                .GroupBy(x => string.Join("|", new[] { x.ForeignKeyProperty, x.CreateMethod, x.UpdateMethod, x.DeleteMethod, x.ListMethod }),
                    StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
            var reviews = (reviewRelationships ?? Enumerable.Empty<MasterDetailReviewDefinition>())
                .GroupBy(x => string.Join("|", new[] { x.KeyProperty, x.ReadMethod }),
                    StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
            if (masters.Count == 0 && details.Count == 0 && reviews.Count == 0) return xml;

            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var view = document.Descendants().Single(x => x.Name.LocalName == "View");
            foreach (var master in masters)
            {
                var create = new XElement(FindOwnedMethodAction(view, master.MasterCreateMethod, null, viewName));
                var update = new XElement(FindOwnedMethodAction(view, master.MasterUpdateMethod, null, viewName));
                AddOwnedMethodRule(view, ResolvedMasterDetailRules.MasterCreateRuleName(master.MasterKeyProperty),
                    PrepareOwnedAction(create));
                AddOwnedMethodRule(view, ResolvedMasterDetailRules.MasterUpdateRuleName(master.MasterKeyProperty),
                    PrepareOwnedAction(update));
            }
            foreach (var detail in details)
            {
                var list = new XElement(FindOwnedMethodAction(view, detail.ListMethod, null, viewName));
                EnsureViewParameter(view, detail.ForeignKeyProperty, viewName);
                RewriteForeignKeyInput(FindOwnedMethodAction(view, detail.CreateMethod, "Added", viewName),
                    detail.ForeignKeyProperty, true, viewName);
                RewriteForeignKeyInput(FindOwnedMethodAction(view, detail.UpdateMethod, "Changed", viewName),
                    detail.ForeignKeyProperty, true, viewName);
                RewriteForeignKeyInput(FindOwnedMethodAction(view, detail.DeleteMethod, "Removed", viewName),
                    detail.ForeignKeyProperty, false, viewName);
                RemoveUnfilteredMethodActions(view, detail.ListMethod);
                AddOwnedMethodRule(view, ResolvedMasterDetailRules.LoadRuleName(detail.ForeignKeyProperty),
                    PrepareFilteredOwnedAction(list, detail.ForeignKeyProperty));
            }
            foreach (var review in reviews)
            {
                var read = new XElement(FindOwnedMethodAction(view, review.ReadMethod, null, viewName));
                EnsureViewParameter(view, review.KeyProperty, viewName);
                AddOwnedMethodRule(view, ResolvedMasterDetailRules.ReviewRuleName(review.KeyProperty),
                    PrepareFilteredOwnedAction(read, review.KeyProperty));
            }
            return document.ToString(SaveOptions.DisableFormatting);
        }

        private static XElement PrepareOwnedAction(XElement prototype)
        {
            prototype.SetAttributeValue("ID", NewId());
            prototype.SetAttributeValue("DefinitionID", NewId());
            prototype.SetAttributeValue("ExecutionType", "Synchronous");
            prototype.Attributes("InstanceID").Remove();
            foreach (var element in prototype.DescendantsAndSelf())
            {
                element.Attributes("IsReference").Remove();
                element.Attributes("IsInherited").Remove();
            }
            return prototype;
        }

        private static XElement FindOwnedMethodAction(XElement view, string method, string state, string viewName)
        {
            var matches = view.Descendants().Where(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadProperty(x, "Method"), method, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(ReadProperty(x, "ControlID")) &&
                (state == null ? x.Attribute("ItemState") == null :
                    string.Equals((string)x.Attribute("ItemState"), state, StringComparison.OrdinalIgnoreCase))).ToList();
            if (matches.Count == 0)
                throw new CliException("View '" + viewName + "' has no generated " + method +
                    (state == null ? " action." : " action for items in state " + state + "."));
            return matches[0];
        }

        private static XElement EnsureViewParameter(XElement view, string property, string viewName)
        {
            var ns = view.Name.Namespace;
            var parameters = view.Elements().FirstOrDefault(x => x.Name.LocalName == "Parameters");
            if (parameters == null)
            {
                parameters = new XElement(ns + "Parameters");
                var events = view.Elements().FirstOrDefault(x => x.Name.LocalName == "Events");
                if (events == null) view.Add(parameters); else events.AddBeforeSelf(parameters);
            }
            var existing = parameters.Elements().Where(x => x.Name.LocalName == "Parameter" &&
                string.Equals(ChildValue(x, "Name"), property, StringComparison.OrdinalIgnoreCase)).ToList();
            if (existing.Count > 1)
                throw new CliException("View '" + viewName + "' has duplicate View parameters named '" + property + "'.");
            if (existing.Count == 1) return existing[0];

            var field = view.Descendants().FirstOrDefault(x => x.Name.LocalName == "Field" &&
                (string.Equals(ChildValue(x, "FieldName"), property, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(ChildValue(x, "Name"), property, StringComparison.OrdinalIgnoreCase)));
            if (field == null)
                throw new CliException("View '" + viewName + "' has no field for master-detail key '" + property + "'.");
            var parameter = new XElement(ns + "Parameter", new XAttribute("ID", NewId()),
                new XAttribute("DataType", ResolvedMasterDetailRules.NormalizeConditionDataType(
                    (string)field.Attribute("DataType") ?? "Text")),
                new XElement(ns + "Name", property));
            parameters.Add(parameter);
            return parameter;
        }

        private static void RewriteForeignKeyInput(XElement action, string property, bool required, string viewName)
        {
            var mappings = action.Descendants().Where(x => x.Name.LocalName == "Parameter" &&
                string.Equals((string)x.Attribute("TargetID"), property, StringComparison.OrdinalIgnoreCase)).ToList();
            if (required && mappings.Count != 1)
                throw new CliException("View '" + viewName + "' persistence action '" + ReadProperty(action, "Method") +
                    "' must have exactly one input for foreign key '" + property + "'.");
            foreach (var mapping in mappings)
            {
                mapping.SetAttributeValue("SourceID", property);
                mapping.SetAttributeValue("SourceName", property);
                mapping.SetAttributeValue("SourceDisplayName", property);
                mapping.SetAttributeValue("SourceType", "ViewParameter");
                mapping.Attributes("SourceInstanceID").Remove();
            }
        }

        private static XElement PrepareFilteredOwnedAction(XElement prototype, string property)
        {
            var ns = prototype.Name.Namespace;
            prototype.SetAttributeValue("ID", NewId());
            prototype.SetAttributeValue("DefinitionID", NewId());
            prototype.SetAttributeValue("ExecutionType", "Synchronous");
            prototype.Attributes("ItemState").Remove();
            prototype.Attributes("InstanceID").Remove();
            prototype.Attributes("IsReference").Remove();
            prototype.Attributes("IsInherited").Remove();
            var parameters = prototype.Elements().FirstOrDefault(x => x.Name.LocalName == "Parameters");
            if (parameters == null)
            {
                parameters = new XElement(ns + "Parameters");
                prototype.Add(parameters);
            }
            parameters.RemoveNodes();
            parameters.Add(new XElement(ns + "Parameter",
                new XAttribute("SourceID", property), new XAttribute("SourceName", property),
                new XAttribute("SourceDisplayName", property), new XAttribute("SourceType", "ViewParameter"),
                new XAttribute("TargetID", property), new XAttribute("TargetName", property),
                new XAttribute("TargetDisplayName", property), new XAttribute("TargetType", "ObjectProperty")));
            return prototype;
        }

        private static void RemoveUnfilteredMethodActions(XElement view, string method)
        {
            foreach (var action in view.Descendants().Where(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                x.Attribute("ItemState") == null &&
                string.IsNullOrWhiteSpace(ReadProperty(x, "ControlID")) &&
                string.Equals(ReadProperty(x, "Method"), method, StringComparison.OrdinalIgnoreCase)).ToList())
                RemoveActionAndEmptyHandler(action);
        }

        private static void AddOwnedMethodRule(XElement view, string ruleName, XElement action)
        {
            var ns = view.Name.Namespace;
            var events = view.Elements().FirstOrDefault(x => x.Name.LocalName == "Events");
            if (events == null)
            {
                events = new XElement(ns + "Events");
                view.Add(events);
            }
            foreach (var existing in events.Elements().Where(x => x.Name.LocalName == "Event" &&
                string.Equals(ReadRuleName(x), ruleName, StringComparison.Ordinal)).ToList())
                existing.Remove();
            var viewId = (string)view.Attribute("ID");
            var viewName = ChildValue(view, "Name") ?? ChildValue(view, "DisplayName") ?? "View";
            events.Add(new XElement(ns + "Event", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "User"), new XAttribute("SourceID", viewId),
                new XAttribute("SourceType", "Rule"), new XAttribute("SourceName", "Rule"),
                new XAttribute("SourceDisplayName", ruleName),
                new XElement(ns + "Name", NewId()),
                new XElement(ns + "Properties",
                    Property(ns, "ViewID", viewId, viewName, viewName),
                    Property(ns, "IsCustomName", "true", null, null),
                    Property(ns, "RuleName", ruleName, null, null),
                    Property(ns, "RuleFriendlyName", ruleName, null, null),
                    Property(ns, "Location", viewName, null, null)),
                new XElement(ns + "Handlers",
                    new XElement(ns + "Handler", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                        new XElement(ns + "Properties",
                            Property(ns, "HandlerName", "IfLogicalHandler", null, null),
                            Property(ns, "Location", "view", null, null)),
                        new XElement(ns + "Actions", action)))));
        }

        private static string ReadRuleName(XElement owner)
        {
            var property = owner.Elements().Where(x => x.Name.LocalName == "Properties")
                .SelectMany(x => x.Elements()).FirstOrDefault(x => x.Name.LocalName == "Property" &&
                    string.Equals(ChildValue(x, "Name"), "RuleName", StringComparison.OrdinalIgnoreCase));
            return property == null ? null : ChildValue(property, "Value");
        }

        public static string SuppressUnfilteredDetailLoads(string xml, string viewName, IEnumerable<MasterDetailChildDefinition> relationships)
        {
            var methods = new HashSet<string>(relationships.Select(x => x.ListMethod), StringComparer.OrdinalIgnoreCase);
            if (methods.Count == 0) return xml;
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var actions = document.Descendants().Where(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                x.Attribute("ItemState") == null &&
                string.IsNullOrWhiteSpace(ReadProperty(x, "ControlID")) &&
                methods.Contains(ReadProperty(x, "Method"))).ToList();
            foreach (var action in actions) RemoveActionAndEmptyHandler(action);
            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static void VerifyDetailViewLoads(string xml, string viewName, IEnumerable<MasterDetailChildDefinition> relationships)
        {
            var document = XDocument.Parse(xml);
            foreach (var relationship in relationships)
            {
                var ruleName = ResolvedMasterDetailRules.LoadRuleName(relationship.ForeignKeyProperty);
                if (!HasDeclaredViewParameter(document, relationship.ForeignKeyProperty))
                    throw new CliException("Master-detail View '" + viewName + "' has no declared View parameter '" +
                        relationship.ForeignKeyProperty + "'.");
                var events = document.Descendants().Where(x => x.Name.LocalName == "Event" &&
                    string.Equals(ReadRuleName(x), ruleName, StringComparison.Ordinal)).ToList();
                if (events.Count != 1)
                    throw new CliException("Master-detail View '" + viewName + "' must contain exactly one View-owned filtered load rule '" + ruleName + "'.");
                VerifyCustomRuleHandlerContext(events[0], viewName, ruleName);
                var actions = document.Descendants().Where(x => x.Name.LocalName == "Action" &&
                    string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                    x.Attribute("ItemState") == null &&
                    string.IsNullOrWhiteSpace(ReadProperty(x, "ControlID")) &&
                    string.Equals(ReadProperty(x, "Method"), relationship.ListMethod, StringComparison.OrdinalIgnoreCase)).ToList();
                if (actions.Count != 1 || !actions[0].Ancestors().Contains(events[0]) ||
                    !HasViewParameterInput(actions[0], relationship.ForeignKeyProperty, relationship.ForeignKeyProperty))
                    throw new CliException("Master-detail View '" + viewName + "' List method '" + relationship.ListMethod +
                        "' must exist only inside '" + ruleName + "' and receive the foreign key from its View parameter.");
                foreach (var persistence in new[]
                {
                    new { Method = relationship.CreateMethod, State = "Added" },
                    new { Method = relationship.UpdateMethod, State = "Changed" }
                })
                {
                    var matches = document.Descendants().Where(x => x.Name.LocalName == "Action" &&
                        string.Equals(ReadProperty(x, "Method"), persistence.Method, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals((string)x.Attribute("ItemState"), persistence.State, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (matches.Count == 0 || matches.Any(x =>
                        !HasViewParameterInput(x, relationship.ForeignKeyProperty, relationship.ForeignKeyProperty)))
                        throw new CliException("Master-detail View '" + viewName + "' " + persistence.State +
                            " persistence must receive '" + relationship.ForeignKeyProperty + "' from a View parameter.");
                }
            }
        }

        public static void VerifyReviewViewRules(string xml, string viewName,
            IEnumerable<MasterDetailReviewDefinition> relationships)
        {
            var document = XDocument.Parse(xml);
            foreach (var relationship in relationships.GroupBy(x =>
                string.Join("|", new[] { x.KeyProperty, x.ReadMethod }), StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First()))
            {
                var ruleName = ResolvedMasterDetailRules.ReviewRuleName(relationship.KeyProperty);
                if (!HasDeclaredViewParameter(document, relationship.KeyProperty))
                    throw new CliException("Review View '" + viewName + "' has no declared View parameter '" +
                        relationship.KeyProperty + "'.");
                var events = document.Descendants().Where(x => x.Name.LocalName == "Event" &&
                    string.Equals(ReadRuleName(x), ruleName, StringComparison.Ordinal)).ToList();
                if (events.Count != 1)
                    throw new CliException("Review View '" + viewName + "' must contain exactly one View-owned Read rule '" + ruleName + "'.");
                VerifyCustomRuleHandlerContext(events[0], viewName, ruleName);
                var actions = events[0].Descendants().Where(x => x.Name.LocalName == "Action" &&
                    string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ReadProperty(x, "Method"), relationship.ReadMethod, StringComparison.OrdinalIgnoreCase)).ToList();
                if (actions.Count != 1 ||
                    !HasViewParameterInput(actions[0], relationship.KeyProperty, relationship.KeyProperty))
                    throw new CliException("Review View '" + viewName + "' rule '" + ruleName +
                        "' must contain one Read action supplied by View parameter '" + relationship.KeyProperty + "'.");
            }
        }

        public static void VerifyMasterViewRules(string xml, string viewName,
            IEnumerable<MasterDetailFormDefinition> relationships)
        {
            var document = XDocument.Parse(xml);
            foreach (var relationship in relationships.GroupBy(x =>
                string.Join("|", new[] { x.MasterKeyProperty, x.MasterCreateMethod, x.MasterUpdateMethod }),
                StringComparer.OrdinalIgnoreCase).Select(x => x.First()))
            {
                VerifyMasterOwnedMethodRule(document, viewName,
                    ResolvedMasterDetailRules.MasterCreateRuleName(relationship.MasterKeyProperty),
                    relationship.MasterCreateMethod);
                VerifyMasterOwnedMethodRule(document, viewName,
                    ResolvedMasterDetailRules.MasterUpdateRuleName(relationship.MasterKeyProperty),
                    relationship.MasterUpdateMethod);
            }
        }

        private static void VerifyMasterOwnedMethodRule(XDocument document, string viewName,
            string ruleName, string method)
        {
            var events = document.Descendants().Where(x => x.Name.LocalName == "Event" &&
                string.Equals((string)x.Attribute("SourceType"), "Rule", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadRuleName(x), ruleName, StringComparison.Ordinal)).ToList();
            if (events.Count != 1)
                throw new CliException("Master View '" + viewName + "' must contain exactly one View-owned persistence rule '" +
                    ruleName + "'.");
            VerifyCustomRuleHandlerContext(events[0], viewName, ruleName);
            var actions = events[0].Descendants().Where(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadProperty(x, "Method"), method, StringComparison.OrdinalIgnoreCase)).ToList();
            if (actions.Count != 1 || actions.Any(x =>
                !string.Equals((string)x.Attribute("ExecutionType"), "Synchronous", StringComparison.OrdinalIgnoreCase) ||
                x.DescendantsAndSelf().Any(y =>
                    string.Equals((string)y.Attribute("IsReference"), "True", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals((string)y.Attribute("IsInherited"), "True", StringComparison.OrdinalIgnoreCase))))
                throw new CliException("Master View '" + viewName + "' persistence rule '" + ruleName +
                    "' must contain one local synchronous '" + method + "' method action.");
        }

        private static void VerifyCustomRuleHandlerContext(XElement customEvent, string viewName, string ruleName)
        {
            var handlers = customEvent.Elements().Where(x => x.Name.LocalName == "Handlers")
                .SelectMany(x => x.Elements()).Where(x => x.Name.LocalName == "Handler").ToList();
            if (handlers.Count == 0 || handlers.Any(x =>
            {
                var properties = x.Elements().FirstOrDefault(y => y.Name.LocalName == "Properties");
                var location = properties == null ? null : properties.Elements().FirstOrDefault(y =>
                    y.Name.LocalName == "Property" &&
                    string.Equals(ChildValue(y, "Name"), "Location", StringComparison.OrdinalIgnoreCase));
                return location == null ||
                    !string.Equals(ChildValue(location, "Value"), "view", StringComparison.OrdinalIgnoreCase);
            }))
                throw new CliException("View '" + viewName + "' custom rule '" + ruleName +
                    "' must use canonical Handler Location 'view' so the Rule Designer can hydrate it.");
        }

        private static bool HasViewParameterInput(XElement action, string source, string target)
        {
            return action.Descendants().Any(x => x.Name.LocalName == "Parameter" &&
                string.Equals((string)x.Attribute("SourceType"), "ViewParameter", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("SourceID"), source, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("TargetID"), target, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasDeclaredViewParameter(XDocument document, string name)
        {
            var view = document.Descendants().Single(x => x.Name.LocalName == "View");
            return view.Elements().Where(x => x.Name.LocalName == "Parameters").SelectMany(x => x.Elements())
                .Count(x => x.Name.LocalName == "Parameter" &&
                    string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase)) == 1;
        }

        public static string Apply(string xml, FormDefinition formDefinition, ResolvedMasterDetailRules relationship)
        {
            if (relationship == null) return xml;
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var form = document.Descendants().First(x => x.Name.LocalName == "Form");
            var masterInstance = FindInstance(form, relationship.MasterViewGuid, relationship.MasterViewName, formDefinition.Name);
            var validationGroupId = AddRequiredValidationGroup(form, relationship, masterInstance);
            HideReviewUntilSaved(form, relationship);

            AddFormSaveButton(form, formDefinition, relationship, masterInstance, validationGroupId);

            foreach (var child in relationship.Details)
            {
                var detailInstance = FindInstance(form, child.ViewGuid, child.ViewName, formDefinition.Name);
                RemoveDetailListActions(form, child, detailInstance);
            }

            var masterReads = FindMethodActions(form, masterInstance, relationship.Definition.MasterReadMethod, null).ToList();
            if (masterReads.Count == 0)
                throw new CliException("Generated form '" + formDefinition.Name + "' has no master Read action for '" + relationship.MasterViewName + "'.");
            var readHandlers = masterReads.Select(x => x.Ancestors().FirstOrDefault(y => y.Name.LocalName == "Handler"))
                .Where(x => x != null).Distinct().ToList();
            if (readHandlers.Count == 0)
                throw new CliException("Generated form '" + formDefinition.Name + "' has no rule handler for master Read action '" + relationship.Definition.MasterReadMethod + "'.");
            foreach (var handler in readHandlers)
            {
                handler.AddAfterSelf(BuildFilteredListHandler(form, relationship, masterInstance, formDefinition.Name));
            }
            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static string ReconcileDetailLoads(string xml, FormDefinition formDefinition, ResolvedMasterDetailRules relationship, out bool changed)
        {
            changed = false;
            if (relationship == null) return xml;
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var form = document.Descendants().First(x => x.Name.LocalName == "Form");
            var baseState = form.Elements().First(x => x.Name.LocalName == "States").Elements().First(x => x.Name.LocalName == "State");
            var masterInstance = FindInstance(form, relationship.MasterViewGuid, relationship.MasterViewName, formDefinition.Name);
            var masterReads = FindMethodActions(baseState, masterInstance, relationship.Definition.MasterReadMethod, null).ToList();
            if (masterReads.Count == 0)
                throw new CliException("K2 Form '" + formDefinition.Name + "' has no master Read action for '" + relationship.MasterViewName + "'.");
            var readHandlers = masterReads.Select(x => x.Ancestors().FirstOrDefault(y => y.Name.LocalName == "Handler"))
                .Where(x => x != null).Distinct().ToList();
            if (!DetailLoadsNeedReconciliation(form, baseState, relationship, masterInstance, readHandlers, formDefinition.Name)) return xml;

            var before = CaptureNonDetailContract(form, relationship, formDefinition.Name);
            RemoveGeneratedDetailLoadHandlers(form, baseState, relationship, formDefinition.Name);
            foreach (var handler in readHandlers)
                handler.AddAfterSelf(BuildFilteredListHandler(form, relationship, masterInstance, formDefinition.Name));

            var after = CaptureNonDetailContract(form, relationship, formDefinition.Name);
            if (!before.SequenceEqual(after, StringComparer.Ordinal))
                throw new CliException("Master-detail reconciliation changed Form states or non-detail actions for '" + formDefinition.Name + "'.");
            VerifyDetailLoads(form, baseState, formDefinition, relationship, masterInstance, readHandlers);
            changed = true;
            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static void Verify(string xml, FormDefinition formDefinition, ResolvedMasterDetailRules relationship)
        {
            if (relationship == null) return;
            var document = XDocument.Parse(xml);
            var form = document.Descendants().First(x => x.Name.LocalName == "Form");
            var baseState = form.Elements().First(x => x.Name.LocalName == "States").Elements().First(x => x.Name.LocalName == "State");
            var masterInstance = FindInstance(form, relationship.MasterViewGuid, relationship.MasterViewName, formDefinition.Name);
            var saveEvent = FindFormSaveEvent(form, relationship.Definition.SaveButtonText);
            if (saveEvent == null) throw new CliException("K2 Form '" + formDefinition.Name + "' has no Form-level master-detail Save button rule.");
            if (saveEvent.Descendants().Any(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                (!string.IsNullOrWhiteSpace(ReadProperty(x, "Method")) || !string.IsNullOrWhiteSpace(ReadProperty(x, "ViewID")))))
                throw new CliException("K2 Form '" + formDefinition.Name +
                    "' Save rule embeds a View method action. Form button rules must invoke View-owned events so the Rule Designer can hydrate them.");
            VerifyEventSaveBranch(form, saveEvent, formDefinition, relationship, masterInstance, true);
            VerifyEventSaveBranch(form, saveEvent, formDefinition, relationship, masterInstance, false);
            var masterReads = FindMethodActions(baseState, masterInstance, relationship.Definition.MasterReadMethod, null).ToList();
            if (masterReads.Count == 0)
                throw new CliException("K2 Form '" + formDefinition.Name + "' has no master Read action for '" + relationship.MasterViewName + "'.");
            var readHandlers = masterReads.Select(x => x.Ancestors().FirstOrDefault(y => y.Name.LocalName == "Handler"))
                .Where(x => x != null).Distinct().ToList();
            VerifyDetailPersistence(form, baseState, formDefinition, relationship, masterInstance);
            VerifyDetailLoads(form, baseState, formDefinition, relationship, masterInstance, readHandlers);
            Console.WriteLine("Master-detail form rules: OK (" + formDefinition.Name + ", master=" + relationship.MasterViewName + ", details=" + relationship.Details.Count + ", read paths=" + readHandlers.Count + ")");
        }

        private static void VerifyEventSaveBranch(XElement form, XElement saveEvent, FormDefinition formDefinition,
            ResolvedMasterDetailRules relationship, string masterInstance, bool create)
        {
            var target = create ? relationship.MasterCreateEvent : relationship.MasterUpdateEvent;
            var calls = FindEventCalls(saveEvent, masterInstance, target).ToList();
            if (calls.Count != 1)
                throw new CliException("K2 Form '" + formDefinition.Name + "' must invoke master View event '" +
                    target.DisplayName + "' exactly once in its " + (create ? "Create" : "Update") + " branch.");
            var call = calls[0];
            var handler = call.Ancestors().First(x => x.Name.LocalName == "Handler");
            if (create ? !HasMasterKeyBlankCondition(call, masterInstance, relationship.MasterKey.Id) :
                !HasMasterKeyNotBlankCondition(call, masterInstance, relationship.MasterKey.Id))
                throw new CliException("K2 Form '" + formDefinition.Name + "' has an invalid master-key condition on its " +
                    (create ? "Create" : "Update") + " branch.");
            var actions = handler.Elements().First(x => x.Name.LocalName == "Actions")
                .Elements().Where(x => x.Name.LocalName == "Action").ToList();
            var callIndex = actions.IndexOf(call);
            if (callIndex < 0 || (relationship.RequiredControls.Count > 0 &&
                (callIndex == 0 || !string.Equals((string)actions[callIndex - 1].Attribute("Type"), "Validate", StringComparison.OrdinalIgnoreCase))))
                throw new CliException("K2 Form '" + formDefinition.Name + "' must validate required fields immediately before the master " +
                    (create ? "Create" : "Update") + " event.");
            if (relationship.RequiredControls.Count > 0)
                VerifyValidationGroup(form, actions[callIndex - 1], masterInstance, relationship, formDefinition.Name);
            var transfer = actions.Skip(callIndex + 1).FirstOrDefault(x =>
                string.Equals((string)x.Attribute("Type"), "Transfer", StringComparison.OrdinalIgnoreCase) &&
                relationship.Details.All(child =>
                {
                    var instance = FindInstance(form, child.ViewGuid, child.ViewName, formDefinition.Name);
                    return HasViewParameterTransfer(x, masterInstance, relationship.MasterKey.Id, instance, child.KeyParameterName);
                }));
            if (transfer == null)
                throw new CliException("K2 Form '" + formDefinition.Name +
                    "' must transfer the saved master key into every detail View parameter before invoking detail Save events.");
            var transferIndex = actions.IndexOf(transfer);
            foreach (var child in relationship.Details)
            {
                var instance = FindInstance(form, child.ViewGuid, child.ViewName, formDefinition.Name);
                var childCall = actions.SingleOrDefault(x => IsEventCall(x, instance, child.SaveEvent));
                if (childCall == null || actions.IndexOf(childCall) <= transferIndex)
                    throw new CliException("K2 Form '" + formDefinition.Name + "' must invoke View-owned Save event '" +
                        child.SaveEvent.DisplayName + "' for detail View '" + child.ViewName + "' after transferring the master key.");
            }
            if (relationship.Definition.Review != null)
            {
                var reviewInstance = FindInstance(form, relationship.ReviewViewGuid, relationship.ReviewViewName, formDefinition.Name);
                if (!HasViewParameterTransfer(transfer, masterInstance, relationship.MasterKey.Id,
                    reviewInstance, relationship.ReviewParameterName))
                    throw new CliException("K2 Form '" + formDefinition.Name + "' does not transfer the saved master key into review View '" +
                        relationship.ReviewViewName + "'.");
                var reviewCall = actions.SingleOrDefault(x => IsEventCall(x, reviewInstance, relationship.ReviewReadEvent));
                if (reviewCall == null || actions.IndexOf(reviewCall) <= transferIndex)
                    throw new CliException("K2 Form '" + formDefinition.Name + "' must invoke the View-owned review Read event after key transfer.");
                VerifyReviewUiActions(form, actions, reviewCall, relationship, formDefinition.Name);
            }
            var message = actions.LastOrDefault();
            if (message == null || !string.Equals((string)message.Attribute("Type"), "ShowMessage", StringComparison.OrdinalIgnoreCase) ||
                !HasMessageValue(message, "Title", relationship.Definition.SuccessMessageTitle) ||
                !HasMessageValue(message, "Body", relationship.Definition.SuccessMessageBody))
                throw new CliException("K2 Form '" + formDefinition.Name + "' " + (create ? "Create" : "Update") +
                    " branch does not finish with the configured success popup.");
        }

        private static void VerifyValidationGroup(XElement form, XElement validate, string masterInstance,
            ResolvedMasterDetailRules relationship, string formName)
        {
            var groupId = ReadProperty(validate, "GroupID");
            var group = form.Elements().Where(x => x.Name.LocalName == "ValidationGroups").SelectMany(x => x.Elements())
                .SingleOrDefault(x => x.Name.LocalName == "ValidationGroup" &&
                    string.Equals((string)x.Attribute("ID"), groupId, StringComparison.OrdinalIgnoreCase));
            if (group == null) throw new CliException("K2 Form '" + formName + "' has no required-field validation group.");
            foreach (var required in relationship.RequiredControls)
                if (!group.Descendants().Any(x => x.Name.LocalName == "ValidationGroupControl" &&
                    string.Equals((string)x.Attribute("ControlID"), required.ControlId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)x.Attribute("InstanceID"), masterInstance, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)x.Attribute("IsRequired"), "True", StringComparison.OrdinalIgnoreCase)))
                    throw new CliException("K2 Form '" + formName + "' validation group omits required property '" + required.Property + "'.");
        }

        private static void VerifyReviewUiActions(XElement form, IList<XElement> actions, XElement read,
            ResolvedMasterDetailRules relationship, string formName)
        {
            var panel = form.Descendants().First(x => x.Name.LocalName == "Panel" &&
                string.Equals(ChildValue(x, "Name"), relationship.Definition.Review.Tab, StringComparison.OrdinalIgnoreCase));
            var panelId = (string)panel.Attribute("ID");
            var panelControl = form.Descendants().Single(x => x.Name.LocalName == "Control" &&
                string.Equals((string)x.Attribute("Type"), "Panel", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("ID"), panelId, StringComparison.OrdinalIgnoreCase));
            if (relationship.Definition.Review.HiddenUntilSaved &&
                !string.Equals(ReadElementProperty(panelControl, "IsVisible"), "false", StringComparison.OrdinalIgnoreCase))
                throw new CliException("K2 Form '" + formName + "' review tab must be hidden initially.");
            var predecessor = read;
            if (relationship.Definition.Review.HiddenUntilSaved)
            {
                var show = actions.SingleOrDefault(x => string.Equals((string)x.Attribute("Type"), "Transfer", StringComparison.OrdinalIgnoreCase) &&
                    x.Descendants().Any(p => p.Name.LocalName == "Parameter" &&
                        string.Equals((string)p.Attribute("TargetID"), "IsVisible", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals((string)p.Attribute("TargetPath"), panelId, StringComparison.OrdinalIgnoreCase)));
                if (show == null || actions.IndexOf(show) <= actions.IndexOf(read))
                    throw new CliException("K2 Form '" + formName + "' must reveal the review tab after its View-owned Read event.");
                predecessor = show;
            }
            var focus = actions.SingleOrDefault(x => string.Equals((string)x.Attribute("Type"), "Focus", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadProperty(x, "PanelID"), panelId, StringComparison.OrdinalIgnoreCase));
            if (focus == null || actions.IndexOf(focus) <= actions.IndexOf(predecessor))
                throw new CliException("K2 Form '" + formName + "' must focus the review tab only after it is loaded and visible.");
        }

        private static void VerifyReviewNavigation(XElement form, XElement saveEvent, string masterInstance, ResolvedMasterDetailRules relationship, string formName)
        {
            if (relationship.Definition.Review == null) return;
            var reviewInstance = FindInstance(form, relationship.ReviewViewGuid, relationship.ReviewViewName, formName);
            var panel = form.Descendants().First(x => x.Name.LocalName == "Panel" && string.Equals(ChildValue(x, "Name"), relationship.Definition.Review.Tab, StringComparison.OrdinalIgnoreCase));
            var panelId = (string)panel.Attribute("ID");
            var panelControl = form.Descendants().Single(x => x.Name.LocalName == "Control" &&
                string.Equals((string)x.Attribute("Type"), "Panel", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("ID"), panelId, StringComparison.OrdinalIgnoreCase));
            if (relationship.Definition.Review.HiddenUntilSaved &&
                !string.Equals(ReadElementProperty(panelControl, "IsVisible"), "false", StringComparison.OrdinalIgnoreCase))
                throw new CliException("K2 Form '" + formName + "' review tab '" + relationship.Definition.Review.Tab + "' must be hidden initially.");
            foreach (var method in new[] { relationship.Definition.MasterCreateMethod, relationship.Definition.MasterUpdateMethod })
            {
                var masterAction = FindMethodActions(saveEvent, masterInstance, method, null).Single();
                var actions = masterAction.Parent.Elements().Where(x => x.Name.LocalName == "Action").ToList();
                var read = actions.SingleOrDefault(x => ActionMatchesInstance(x, reviewInstance) && string.Equals(ReadProperty(x, "Method"), relationship.Definition.Review.ReadMethod, StringComparison.OrdinalIgnoreCase));
                if (read == null || !HasMasterKeyMapping(read, masterInstance, relationship.MasterKey.Id, relationship.Definition.Review.KeyProperty))
                    throw new CliException("K2 Form '" + formName + "' does not load review View '" + relationship.ReviewViewName + "' from the saved master key after " + method + ".");
                if (!IsDesignerLoadableLocalViewAction(read, relationship.ReviewReadAction, reviewInstance))
                    throw new CliException("K2 Form '" + formName + "' review Read after " + method + " is not a Designer-loadable local View action.");
                var show = actions.SingleOrDefault(x => string.Equals((string)x.Attribute("Type"), "Transfer", StringComparison.OrdinalIgnoreCase) &&
                    x.Descendants().Any(p => p.Name.LocalName == "Parameter" &&
                        string.Equals((string)p.Attribute("TargetID"), "IsVisible", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals((string)p.Attribute("TargetPath"), panelId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(p.Value, "true", StringComparison.OrdinalIgnoreCase)));
                if (relationship.Definition.Review.HiddenUntilSaved && (show == null || actions.IndexOf(show) <= actions.IndexOf(read)))
                    throw new CliException("K2 Form '" + formName + "' must show review tab '" + relationship.Definition.Review.Tab + "' only after loading the submitted record.");
                var focus = actions.SingleOrDefault(x => string.Equals((string)x.Attribute("Type"), "Focus", StringComparison.OrdinalIgnoreCase) && string.Equals(ReadProperty(x, "PanelID"), panelId, StringComparison.OrdinalIgnoreCase));
                var predecessor = relationship.Definition.Review.HiddenUntilSaved ? show : read;
                if (focus == null || actions.IndexOf(focus) <= actions.IndexOf(predecessor)) throw new CliException("K2 Form '" + formName + "' must load and reveal the review before focusing tab '" + relationship.Definition.Review.Tab + "'.");
            }
        }

        private static void VerifyRequiredValidation(XElement form, XElement saveEvent, string masterInstance, ResolvedMasterDetailRules relationship, string formName)
        {
            if (relationship.RequiredControls == null || relationship.RequiredControls.Count == 0) return;
            var groups = form.Elements().FirstOrDefault(x => x.Name.LocalName == "ValidationGroups");
            var group = groups == null ? null : groups.Elements().SingleOrDefault(x => x.Name.LocalName == "ValidationGroup" &&
                string.Equals(ChildValue(x, "Name"), "ValidationGroupForEvent", StringComparison.Ordinal));
            if (group == null) throw new CliException("K2 Form '" + formName + "' has no required-field validation group for the master View.");
            var groupId = (string)group.Attribute("ID");
            foreach (var required in relationship.RequiredControls)
                if (!group.Descendants().Any(x => x.Name.LocalName == "ValidationGroupControl" &&
                    string.Equals((string)x.Attribute("ControlID"), required.ControlId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)x.Attribute("IsRequired"), "True", StringComparison.OrdinalIgnoreCase)))
                    throw new CliException("K2 Form '" + formName + "' validation group omits required property '" + required.Property + "'.");
            foreach (var method in new[] { relationship.Definition.MasterCreateMethod, relationship.Definition.MasterUpdateMethod })
            {
                var master = FindMethodActions(saveEvent, masterInstance, method, null).Single();
                var actions = master.Parent.Elements().Where(x => x.Name.LocalName == "Action").ToList();
                var validate = actions.FirstOrDefault();
                if (validate == null || !string.Equals((string)validate.Attribute("Type"), "Validate", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ReadProperty(validate, "GroupID"), groupId, StringComparison.OrdinalIgnoreCase))
                    throw new CliException("K2 Form '" + formName + "' must validate required fields before " + method + ".");
            }
        }

        private static void VerifyDetailLoads(XElement form, XElement baseState, FormDefinition formDefinition, ResolvedMasterDetailRules relationship, string masterInstance, IList<XElement> readHandlers)
        {
            foreach (var child in relationship.Details)
            {
                var detailInstance = FindInstance(form, child.ViewGuid, child.ViewName, formDefinition.Name);
                var calls = FindEventCalls(baseState, detailInstance, child.LoadEvent).ToList();
                if (calls.Count != readHandlers.Count)
                    throw new CliException("K2 Form '" + formDefinition.Name + "' must invoke filtered View load event '" +
                        child.LoadEvent.DisplayName + "' once on each master Read path; found " + calls.Count + ".");
                if (calls.Any(x => !HasMasterKeyNotBlankCondition(x, masterInstance, relationship.MasterKey.Id) ||
                    !FollowsMasterRead(x, masterInstance, relationship.Definition.MasterReadMethod)))
                    throw new CliException("K2 Form '" + formDefinition.Name + "' has an ungated or misplaced detail load event call for '" + child.ViewName + "'.");
                if (calls.Any(x =>
                {
                    var handler = x.Ancestors().First(y => y.Name.LocalName == "Handler");
                    return !handler.Descendants().Any(a => a.Name.LocalName == "Action" &&
                        string.Equals((string)a.Attribute("Type"), "Transfer", StringComparison.OrdinalIgnoreCase) &&
                        HasViewParameterTransfer(a, masterInstance, relationship.MasterKey.Id,
                            detailInstance, child.KeyParameterName));
                }))
                    throw new CliException("K2 Form '" + formDefinition.Name + "' does not transfer the master key into detail View parameter '" +
                        child.KeyParameterName + "' before loading '" + child.ViewName + "'.");
                foreach (var readHandler in readHandlers)
                {
                    var next = readHandler.ElementsAfterSelf().FirstOrDefault(x => x.Name.LocalName == "Handler");
                    if (next == null || FindEventCalls(next, detailInstance, child.LoadEvent).Count() != 1)
                        throw new CliException("K2 Form '" + formDefinition.Name + "' does not load detail view '" + child.ViewName + "' immediately after every master Read path.");
                }
            }
        }

        private static void VerifyDetailPersistence(XElement form, XElement baseState, FormDefinition formDefinition, ResolvedMasterDetailRules relationship, string masterInstance)
        {
            foreach (var child in relationship.Details)
            {
                var detailInstance = FindInstance(form, child.ViewGuid, child.ViewName, formDefinition.Name);
                var saveEvent = FindFormSaveEvent(form, relationship.Definition.SaveButtonText);
                var calls = FindEventCalls(saveEvent, detailInstance, child.SaveEvent).ToList();
                if (calls.Count != 2)
                    throw new CliException("K2 Form '" + formDefinition.Name + "' must invoke detail View-owned Save event '" +
                        child.SaveEvent.DisplayName + "' once in each master Create/Update branch.");
            }
        }

        private static bool DetailLoadsNeedReconciliation(XElement form, XElement scope, ResolvedMasterDetailRules relationship, string masterInstance, IList<XElement> readHandlers, string formName)
        {
            foreach (var child in relationship.Details)
            {
                var detailInstance = FindInstance(form, child.ViewGuid, child.ViewName, formName);
                var actions = FindEventCalls(scope, detailInstance, child.LoadEvent).ToList();
                if (actions.Count != readHandlers.Count || actions.Any(x =>
                    !HasMasterKeyNotBlankCondition(x, masterInstance, relationship.MasterKey.Id) ||
                    !FollowsMasterRead(x, masterInstance, relationship.Definition.MasterReadMethod))) return true;
                foreach (var readHandler in readHandlers)
                {
                    var next = readHandler.ElementsAfterSelf().FirstOrDefault(x => x.Name.LocalName == "Handler");
                    if (next == null || FindEventCalls(next, detailInstance, child.LoadEvent).Count() != 1 ||
                        !next.Descendants().Any(a => a.Name.LocalName == "Action" &&
                            string.Equals((string)a.Attribute("Type"), "Transfer", StringComparison.OrdinalIgnoreCase) &&
                            HasViewParameterTransfer(a, masterInstance, relationship.MasterKey.Id,
                                detailInstance, child.KeyParameterName))) return true;
                }
            }
            return false;
        }

        private static IList<string> CaptureNonDetailContract(XElement form, ResolvedMasterDetailRules relationship, string formName)
        {
            var detailInstances = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var child in relationship.Details)
            {
                var instance = FindInstance(form, child.ViewGuid, child.ViewName, formName);
                HashSet<string> methods;
                if (!detailInstances.TryGetValue(instance, out methods))
                {
                    methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    detailInstances[instance] = methods;
                }
                methods.Add(child.Definition.ListMethod);
            }
            var result = form.Descendants().Where(x => x.Name.LocalName == "State").Select(x =>
                "STATE|" + (string)x.Attribute("ID") + "|" + (string)x.Attribute("Name") + "|" + (string)x.Attribute("IsDefault") + "|" + ChildValue(x, "Name")).ToList();
            result.AddRange(form.Descendants().Where(x => x.Name.LocalName == "Action" &&
                    !IsDeclaredDetailLoadAction(x, relationship, detailInstances, form, formName))
                .Select(x => "ACTION|" + x.ToString(SaveOptions.DisableFormatting)));
            return result;
        }

        private static bool IsDeclaredDetailLoadAction(XElement action, ResolvedMasterDetailRules relationship,
            IDictionary<string, HashSet<string>> detailInstances, XElement form, string formName)
        {
            if (IsDeclaredDetailListAction(action, detailInstances)) return true;
            var handler = action.Ancestors().FirstOrDefault(x => x.Name.LocalName == "Handler");
            if (handler == null) return false;
            return relationship.Details.Any(child =>
            {
                var instance = FindInstance(form, child.ViewGuid, child.ViewName, formName);
                return FindEventCalls(handler, instance, child.LoadEvent).Any();
            });
        }

        private static void RemoveGeneratedDetailLoadHandlers(XElement form, XElement scope,
            ResolvedMasterDetailRules relationship, string formName)
        {
            var detailInstances = relationship.Details.ToDictionary(
                child => FindInstance(form, child.ViewGuid, child.ViewName, formName),
                child => child, StringComparer.OrdinalIgnoreCase);
            foreach (var handler in scope.Descendants().Where(x => x.Name.LocalName == "Handler").ToList())
            {
                var containsLoad = detailInstances.Any(pair =>
                    FindEventCalls(handler, pair.Key, pair.Value.LoadEvent).Any() ||
                    FindDetailListActions(handler, pair.Key, pair.Value.Definition.ListMethod).Any());
                if (!containsLoad) continue;
                if (handler.Descendants().Any(x => x.Name.LocalName == "Action" &&
                    string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                    detailInstances.All(pair =>
                        !IsEventCall(x, pair.Key, pair.Value.LoadEvent) &&
                        !FindDetailListActions(new XElement("Scope", new XElement(x)), pair.Key,
                            pair.Value.Definition.ListMethod).Any())))
                    throw new CliException("Refusing to reconcile a mixed K2 rule handler that contains detail-load and unrelated Execute actions.");
                handler.Remove();
            }
        }

        private static bool IsDeclaredDetailListAction(XElement action, IDictionary<string, HashSet<string>> detailInstances)
        {
            if (!string.Equals((string)action.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase)) return false;
            return string.IsNullOrWhiteSpace(ReadProperty(action, "ControlID")) &&
                detailInstances.Any(pair => pair.Value.Contains(ReadProperty(action, "Method")) &&
                    ActionMatchesInstance(action, pair.Key));
        }

        private static string AddRequiredValidationGroup(XElement form, ResolvedMasterDetailRules relationship, string masterInstance)
        {
            if (relationship.RequiredControls == null || relationship.RequiredControls.Count == 0) return null;
            var ns = form.Name.Namespace;
            var groups = form.Elements().FirstOrDefault(x => x.Name.LocalName == "ValidationGroups");
            if (groups == null)
            {
                groups = new XElement(ns + "ValidationGroups");
                var states = form.Elements().FirstOrDefault(x => x.Name.LocalName == "States");
                if (states == null) form.Add(groups); else states.AddBeforeSelf(groups);
            }
            var groupId = NewId();
            groups.Add(new XElement(ns + "ValidationGroup", new XAttribute("ID", groupId),
                new XElement(ns + "Name", "ValidationGroupForEvent"),
                new XElement(ns + "ValidationGroupControls", relationship.RequiredControls.Select(x =>
                    new XElement(ns + "ValidationGroupControl", new XAttribute("ID", NewId()),
                        new XAttribute("ControlID", x.ControlId), new XAttribute("InstanceID", masterInstance),
                        new XAttribute("IsRequired", "True"), new XAttribute("ControlName", x.ControlName),
                        new XAttribute("ControlDisplayName", x.ControlDisplayName))))));
            return groupId;
        }

        private static void HideReviewUntilSaved(XElement form, ResolvedMasterDetailRules relationship)
        {
            if (relationship.Definition.Review == null || !relationship.Definition.Review.HiddenUntilSaved) return;
            var panel = form.Descendants().First(x => x.Name.LocalName == "Panel" &&
                string.Equals(ChildValue(x, "Name"), relationship.Definition.Review.Tab, StringComparison.OrdinalIgnoreCase));
            var panelId = (string)panel.Attribute("ID");
            var panelControl = form.Descendants().Single(x => x.Name.LocalName == "Control" &&
                string.Equals((string)x.Attribute("Type"), "Panel", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("ID"), panelId, StringComparison.OrdinalIgnoreCase));
            SetElementProperty(panelControl, "IsVisible", "false");
        }

        private static void AddFormSaveButton(XElement form, FormDefinition formDefinition, ResolvedMasterDetailRules relationship, string masterInstance, string validationGroupId)
        {
            var ns = form.Name.Namespace;
            var controls = form.Elements().First(x => x.Name.LocalName == "Controls");
            var tableId = NewId(); var rowId = NewId(); var cellId = NewId(); var buttonId = NewId();
            var areaId = NewId(); var itemId = NewId();
            var buttonName = "btnSave";
            var buttonText = string.IsNullOrWhiteSpace(relationship.Definition.SaveButtonText) ? "Save" : relationship.Definition.SaveButtonText;

            controls.Add(Control(ns, tableId, "Table", "tblFormActions", Property(ns, "IsResponsive", "true", "true", "true")));
            controls.Add(Control(ns, rowId, "Row", "Form Actions Row"));
            controls.Add(Control(ns, cellId, "Cell", "Form Actions Cell"));
            controls.Add(Control(ns, buttonId, "Button", buttonName,
                Property(ns, "Text", buttonText, buttonText, buttonText),
                Property(ns, "ButtonStyle", "mainaction", "mainaction", "mainaction")));
            controls.Add(Control(ns, areaId, "Area", "Form Actions Area"));
            controls.Add(Control(ns, itemId, "AreaItem", "Form Actions Area Item"));

            var canvas = new XElement(ns + "Canvas",
                new XElement(ns + "Control", new XAttribute("ID", tableId), new XAttribute("LayoutType", "Grid"),
                    new XElement(ns + "Columns", new XElement(ns + "Column", new XAttribute("ID", NewId()), new XAttribute("Size", "100%"))),
                    new XElement(ns + "Rows", new XElement(ns + "Row", new XAttribute("ID", rowId),
                        new XElement(ns + "Cells", new XElement(ns + "Cell", new XAttribute("ID", cellId),
                            new XElement(ns + "Control", new XAttribute("ID", buttonId))))))));
            var area = new XElement(ns + "Area", new XAttribute("ID", areaId),
                new XElement(ns + "Items", new XElement(ns + "Item", new XAttribute("ID", itemId), canvas)));
            var lastDetail = relationship.Details.Last();
            var detailItem = form.Descendants().First(x => x.Name.LocalName == "Item" &&
                (string.Equals((string)x.Attribute("ViewID"), lastDetail.ViewGuid.ToString(), StringComparison.OrdinalIgnoreCase) ||
                 string.Equals((string)x.Attribute("ViewName"), lastDetail.ViewName, StringComparison.OrdinalIgnoreCase)));
            detailItem.Ancestors().First(x => x.Name.LocalName == "Area").AddAfterSelf(area);

            var states = form.Elements().First(x => x.Name.LocalName == "States");
            var state = states.Elements().First(x => x.Name.LocalName == "State");
            var events = state.Elements().FirstOrDefault(x => x.Name.LocalName == "Events");
            if (events == null) { events = new XElement(ns + "Events"); state.Add(events); }
            var saveEvent = new XElement(ns + "Event", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "User"), new XAttribute("SourceID", buttonId), new XAttribute("SourceType", "Control"),
                new XAttribute("SourceName", buttonName), new XAttribute("SourceDisplayName", buttonName),
                new XElement(ns + "Name", "OnClick"),
                new XElement(ns + "Properties",
                    Property(ns, "RuleFriendlyName", "When " + buttonName + " is Clicked", null, null),
                    Property(ns, "Location", formDefinition.Name, null, null)),
                new XElement(ns + "Handlers"));
            var handlers = saveEvent.Elements().First(x => x.Name.LocalName == "Handlers");
            handlers.Add(BuildSaveHandler(form, relationship, masterInstance, true, validationGroupId));
            handlers.Add(BuildSaveHandler(form, relationship, masterInstance, false, validationGroupId));
            events.Add(saveEvent);
        }

        private static XElement BuildSaveHandler(XElement form, ResolvedMasterDetailRules relationship, string masterInstance, bool create, string validationGroupId)
        {
            var ns = form.Name.Namespace;
            var actions = new XElement(ns + "Actions");
            if (!string.IsNullOrWhiteSpace(validationGroupId)) actions.Add(BuildValidateAction(ns, validationGroupId));
            actions.Add(BuildViewEventAction(ns,
                create ? relationship.MasterCreateEvent : relationship.MasterUpdateEvent, masterInstance));
            actions.Add(BuildKeyTransferAction(form, relationship, masterInstance, true));
            foreach (var child in relationship.Details)
            {
                var detailInstance = FindInstance(form, child.ViewGuid, child.ViewName, ChildValue(form, "Name"));
                actions.Add(BuildViewEventAction(ns, child.SaveEvent, detailInstance));
            }
            if (relationship.Definition.Review != null)
            {
                var reviewInstance = FindInstance(form, relationship.ReviewViewGuid, relationship.ReviewViewName, ChildValue(form, "Name"));
                actions.Add(BuildViewEventAction(ns, relationship.ReviewReadEvent, reviewInstance));
                if (relationship.Definition.Review.HiddenUntilSaved)
                    actions.Add(BuildReviewVisibilityAction(form, relationship.Definition.Review.Tab));
                actions.Add(BuildReviewFocusAction(form, relationship.Definition.Review.Tab));
            }
            actions.Add(BuildSuccessMessage(ns, relationship.Definition.SuccessMessageTitle, relationship.Definition.SuccessMessageBody));
            return new XElement(ns + "Handler", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XElement(ns + "Properties", Property(ns, "HandlerName", "IfLogicalHandler", null, null), Property(ns, "Location", "form", null, null)),
                new XElement(ns + "Conditions", BuildMasterKeyCondition(ns, masterInstance, relationship.MasterKey, !create)), actions);
        }

        private static XElement BuildValidateAction(XNamespace ns, string groupId)
        {
            return new XElement(ns + "Action", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "Validate"), new XAttribute("ExecutionType", "Synchronous"),
                new XElement(ns + "Properties",
                    Property(ns, "Location", "Form", null, null),
                    Property(ns, "MessageLocation", "Control", null, null),
                    Property(ns, "GroupID", groupId, null, "ValidationGroupForEvent"),
                    Property(ns, "IgnoreInvisibleControls", "true", null, null),
                    Property(ns, "IgnoreDisabledControls", "true", null, null),
                    Property(ns, "IgnoreReadOnlyControls", "true", null, null)));
        }

        private static XElement BuildReviewReadAction(XNamespace ns, ResolvedMasterDetailRules relationship, string masterInstance, string reviewInstance)
        {
            var action = XElement.Parse(relationship.ReviewReadAction);
            PrepareLocalViewAction(action, reviewInstance, "Synchronous");
            var parameters = action.Elements().FirstOrDefault(x => x.Name.LocalName == "Parameters");
            if (parameters == null) { parameters = new XElement(ns + "Parameters"); action.Add(parameters); }
            parameters.RemoveNodes();
            parameters.Add(BuildMasterKeyParameter(ns, relationship.Definition.Review.KeyProperty, masterInstance, relationship.MasterKey));
            var results = action.Elements().FirstOrDefault(x => x.Name.LocalName == "Results");
            if (results != null) foreach (var result in results.Elements()) result.SetAttributeValue("TargetInstanceID", reviewInstance);
            return action;
        }

        private static XElement BuildReviewFocusAction(XElement form, string tabName)
        {
            var ns = form.Name.Namespace;
            var panel = form.Descendants().First(x => x.Name.LocalName == "Panel" && string.Equals(ChildValue(x, "Name"), tabName, StringComparison.OrdinalIgnoreCase));
            var panelId = (string)panel.Attribute("ID");
            return new XElement(ns + "Action", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()), new XAttribute("Type", "Focus"), new XAttribute("ExecutionType", "Synchronous"),
                new XElement(ns + "Properties", Property(ns, "Location", "Form", null, null), Property(ns, "PanelID", panelId, tabName, tabName)));
        }

        private static XElement BuildReviewVisibilityAction(XElement form, string tabName)
        {
            var ns = form.Name.Namespace;
            var panel = form.Descendants().First(x => x.Name.LocalName == "Panel" &&
                string.Equals(ChildValue(x, "Name"), tabName, StringComparison.OrdinalIgnoreCase));
            var panelId = (string)panel.Attribute("ID");
            return new XElement(ns + "Action", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "Transfer"), new XAttribute("ExecutionType", "Synchronous"),
                new XElement(ns + "Properties",
                    Property(ns, "Location", "Form", null, null),
                    Property(ns, "DesignTemplate", "SetControlProperties", null, null),
                    Property(ns, "ControlID", panelId, tabName, tabName),
                    Property(ns, "FormID", (string)form.Attribute("ID"), ChildValue(form, "Name"), ChildValue(form, "Name"))),
                new XElement(ns + "Parameters",
                    new XElement(ns + "Parameter", new XAttribute("SourceID", "Sources"), new XAttribute("SourceType", "Value"),
                        new XAttribute("TargetID", "IsVisible"), new XAttribute("TargetType", "ControlProperty"),
                        new XAttribute("TargetPath", panelId), new XAttribute("TargetPathType", "Panel"),
                        new XElement(ns + "SourceValue", new XAttribute(XNamespace.Xml + "space", "preserve"), "true"))));
        }

        private static XElement BuildSuccessMessage(XNamespace ns, string title, string body)
        {
            return new XElement(ns + "Action", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "ShowMessage"), new XAttribute("ExecutionType", "Synchronous"),
                new XElement(ns + "Properties",
                    Property(ns, "Location", "Form", null, null),
                    Property(ns, "MessageLocation", "Popup", null, null)),
                new XElement(ns + "Parameters",
                    MessageParameter(ns, "Size", "small"),
                    MessageParameter(ns, "Type", "info"),
                    MessageParameter(ns, "Title", title),
                    MessageParameter(ns, "Body", body)));
        }

        private static XElement MessageParameter(XNamespace ns, string target, string value)
        {
            return new XElement(ns + "Parameter", new XAttribute("SourceID", "Sources"), new XAttribute("SourceType", "Value"),
                new XAttribute("TargetID", target), new XAttribute("TargetName", target), new XAttribute("TargetType", "MessageProperty"),
                new XElement(ns + "SourceValue", new XAttribute(XNamespace.Xml + "space", "preserve"),
                    new XElement(ns + "Source", new XAttribute("SourceType", "Value"), value)));
        }

        private static XElement BuildFilteredListHandler(XElement form, ResolvedMasterDetailRules relationship, string masterInstance, string formName)
        {
            var ns = form.Name.Namespace;
            var actions = new XElement(ns + "Actions");
            actions.Add(BuildKeyTransferAction(form, relationship, masterInstance, false));
            foreach (var child in relationship.Details)
            {
                var detailInstance = FindInstance(form, child.ViewGuid, child.ViewName, formName);
                actions.Add(BuildViewEventAction(ns, child.LoadEvent, detailInstance));
            }
            return new XElement(ns + "Handler", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XElement(ns + "Properties", Property(ns, "HandlerName", "IfLogicalHandler", null, null), Property(ns, "Location", "form", null, null)),
                new XElement(ns + "Conditions", BuildMasterKeyCondition(ns, masterInstance, relationship.MasterKey, true)), actions);
        }

        private static XElement BuildViewEventAction(XNamespace ns, ResolvedViewEvent target, string instanceId)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.DefinitionId))
                throw new CliException("Cannot build a Form View-event call without a resolved target event.");
            return new XElement(ns + "Action", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "Execute"), new XAttribute("ExecutionType", "Synchronous"),
                new XAttribute("InstanceID", instanceId),
                new XElement(ns + "Properties",
                    Property(ns, "Location", "Form", null, null),
                    Property(ns, "EventID", target.DefinitionId, target.DisplayName, target.DisplayName)));
        }

        private static XElement BuildKeyTransferAction(XElement form, ResolvedMasterDetailRules relationship,
            string masterInstance, bool includeReview)
        {
            var ns = form.Name.Namespace;
            var parameters = new XElement(ns + "Parameters");
            foreach (var child in relationship.Details)
            {
                var detailInstance = FindInstance(form, child.ViewGuid, child.ViewName, ChildValue(form, "Name"));
                parameters.Add(BuildViewParameterTransfer(ns, masterInstance, relationship.MasterKey,
                    detailInstance, child.KeyParameterName));
            }
            if (includeReview && relationship.Definition.Review != null)
            {
                var reviewInstance = FindInstance(form, relationship.ReviewViewGuid, relationship.ReviewViewName,
                    ChildValue(form, "Name"));
                parameters.Add(BuildViewParameterTransfer(ns, masterInstance, relationship.MasterKey,
                    reviewInstance, relationship.ReviewParameterName));
            }
            return new XElement(ns + "Action", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "Transfer"), new XAttribute("ExecutionType", "Synchronous"),
                new XElement(ns + "Properties",
                    Property(ns, "Location", "Form", null, null),
                    Property(ns, "FormID", (string)form.Attribute("ID"), ChildValue(form, "Name"), ChildValue(form, "Name"))),
                parameters);
        }

        private static XElement BuildViewParameterTransfer(XNamespace ns, string masterInstance,
            ResolvedViewField masterKey, string targetInstance, string targetParameter)
        {
            return new XElement(ns + "Parameter",
                new XAttribute("SourceID", masterKey.Id), new XAttribute("SourceName", masterKey.Name),
                new XAttribute("SourceDisplayName", masterKey.DisplayName), new XAttribute("SourceType", "ViewField"),
                new XAttribute("SourceInstanceID", masterInstance),
                new XAttribute("TargetID", targetParameter), new XAttribute("TargetName", targetParameter),
                new XAttribute("TargetDisplayName", targetParameter), new XAttribute("TargetType", "ViewParameter"),
                new XAttribute("TargetInstanceID", targetInstance));
        }

        private static XElement BuildMasterKeyCondition(XNamespace ns, string masterInstance, ResolvedViewField masterKey, bool notBlank)
        {
            var conditionName = notBlank ? "SimpleNotBlankViewFieldCondition" : "SimpleBlankViewFieldCondition";
            var expression = notBlank ? "IsNotBlank" : "IsBlank";
            return new XElement(ns + "Condition", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()), new XAttribute("InstanceID", masterInstance),
                new XElement(ns + "Properties", Property(ns, "Location", "Form", null, null), Property(ns, "Name", conditionName, null, null)),
                new XElement(ns + "Expressions", new XElement(ns + expression,
                    new XElement(ns + "Item", new XAttribute("SourceType", "ViewField"), new XAttribute("SourceName", masterKey.Name),
                        new XAttribute("SourceDisplayName", masterKey.DisplayName), new XAttribute("SourceInstanceID", masterInstance),
                        new XAttribute("SourceID", masterKey.Id), new XAttribute("DataType", masterKey.DataType)))));
        }

        private static void RemoveDetailListActions(XElement form, ResolvedMasterDetailChild child, string detailInstance)
        {
            var actions = FindDetailListActions(form, detailInstance, child.Definition.ListMethod).ToList();
            foreach (var action in actions) RemoveActionAndEmptyHandler(action);
        }

        private static void RemoveActionAndEmptyHandler(XElement action)
        {
            var container = action.Parent;
            action.Remove();
            if (container == null || container.Elements().Any(x => x.Name.LocalName == "Action")) return;
            var handler = container.Parent;
            container.Remove();
            if (handler != null && handler.Name.LocalName == "Handler" &&
                !handler.Descendants().Any(x => x.Name.LocalName == "Action"))
                handler.Remove();
        }

        private static XElement Control(XNamespace ns, string id, string type, string name, params XElement[] extraProperties)
        {
            var properties = new XElement(ns + "Properties", Property(ns, "ControlName", name, name, name));
            foreach (var property in extraProperties) properties.Add(property);
            return new XElement(ns + "Control", new XAttribute("ID", id), new XAttribute("Type", type),
                new XElement(ns + "Name", name), new XElement(ns + "DisplayName", name), properties);
        }

        private static XElement FindFormSaveEvent(XElement form, string buttonText)
        {
            var controls = form.Elements().First(x => x.Name.LocalName == "Controls");
            var button = controls.Elements().FirstOrDefault(x => x.Name.LocalName == "Control" &&
                string.Equals((string)x.Attribute("Type"), "Button", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ChildValue(x, "Name"), "btnSave", StringComparison.OrdinalIgnoreCase));
            if (button == null) return null;
            var id = (string)button.Attribute("ID");
            return form.Descendants().FirstOrDefault(x => x.Name.LocalName == "Event" &&
                string.Equals((string)x.Attribute("Type"), "User", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("SourceType"), "Control", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("SourceID"), id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ChildValue(x, "Name"), "OnClick", StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasMasterKeyResult(XElement action, string masterInstance, string masterFieldId)
        {
            return action.Descendants().Any(x => x.Name.LocalName == "Result" &&
                string.Equals((string)x.Attribute("TargetType"), "ViewField", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("TargetInstanceID"), masterInstance, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("TargetID"), masterFieldId, StringComparison.OrdinalIgnoreCase));
        }

        private static XElement BuildMasterAction(string prototypeXml, string masterInstance, ResolvedViewField masterKey)
        {
            var action = XElement.Parse(prototypeXml);
            PrepareLocalViewAction(action, masterInstance, "Parallel");
            foreach (var parameter in action.Descendants().Where(x => x.Name.LocalName == "Parameter" &&
                (string.Equals((string)x.Attribute("SourceType"), "ViewField", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals((string)x.Attribute("SourceType"), "Control", StringComparison.OrdinalIgnoreCase))))
                parameter.SetAttributeValue("SourceInstanceID", masterInstance);
            foreach (var result in action.Descendants().Where(x => x.Name.LocalName == "Result" &&
                (string.Equals((string)x.Attribute("TargetType"), "ViewField", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals((string)x.Attribute("TargetType"), "Control", StringComparison.OrdinalIgnoreCase))))
                result.SetAttributeValue("TargetInstanceID", masterInstance);
            foreach (var result in action.Descendants().Where(x => x.Name.LocalName == "Result" &&
                string.Equals((string)x.Attribute("SourceID"), masterKey.Name, StringComparison.OrdinalIgnoreCase)))
            {
                result.SetAttributeValue("TargetID", masterKey.Id);
                result.SetAttributeValue("TargetName", masterKey.Name);
                result.SetAttributeValue("TargetDisplayName", masterKey.DisplayName);
                result.SetAttributeValue("TargetType", "ViewField");
                result.SetAttributeValue("TargetInstanceID", masterInstance);
            }
            return action;
        }

        private static XElement BuildStateAction(XNamespace ns, ResolvedMasterDetailChild child, string prototypeXml, string state, string masterInstance, ResolvedViewField masterKey, string detailInstance)
        {
            var action = XElement.Parse(prototypeXml);
            PrepareLocalViewAction(action, detailInstance, "Parallel");
            action.SetAttributeValue("ItemState", state);
            var parameters = action.Elements().FirstOrDefault(x => x.Name.LocalName == "Parameters");
            if (parameters == null) { parameters = new XElement(ns + "Parameters"); action.Add(parameters); }
            foreach (var parameter in parameters.Elements().Where(x => x.Name.LocalName == "Parameter").ToList())
            {
                if (string.Equals((string)parameter.Attribute("TargetID"), child.Definition.ForeignKeyProperty, StringComparison.OrdinalIgnoreCase))
                {
                    parameter.Remove();
                    continue;
                }
                if (string.Equals((string)parameter.Attribute("SourceType"), "ViewField", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals((string)parameter.Attribute("SourceType"), "Control", StringComparison.OrdinalIgnoreCase))
                    parameter.SetAttributeValue("SourceInstanceID", detailInstance);
            }
            if (state != "Removed")
                parameters.AddFirst(BuildMasterKeyParameter(ns, child.Definition.ForeignKeyProperty, masterInstance, masterKey));
            var results = action.Elements().FirstOrDefault(x => x.Name.LocalName == "Results");
            if (results != null)
                foreach (var result in results.Elements().Where(x =>
                    string.Equals((string)x.Attribute("TargetType"), "ViewField", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals((string)x.Attribute("TargetType"), "Control", StringComparison.OrdinalIgnoreCase)))
                    result.SetAttributeValue("TargetInstanceID", detailInstance);
            return action;
        }

        private static XElement BuildListAction(XNamespace ns, ResolvedMasterDetailChild child, string masterInstance, ResolvedViewField masterKey, string detailInstance)
        {
            XElement action;
            if (!string.IsNullOrWhiteSpace(child.ListAction))
            {
                action = XElement.Parse(child.ListAction);
                action.Elements().Where(x => x.Name.LocalName == "Parameters" || x.Name.LocalName == "Results").Remove();
            }
            else
            {
                action = new XElement(ns + "Action",
                    new XElement(ns + "Properties",
                        Property(ns, "Location", "View", null, null),
                        Property(ns, "Method", child.Definition.ListMethod, child.Definition.ListMethod, child.Definition.ListMethod),
                        Property(ns, "ViewID", child.ViewGuid.ToString(), child.ViewDisplayName, child.ViewName)));
            }
            PrepareLocalViewAction(action, detailInstance, "Synchronous");
            action.Attributes("ItemState").Remove();
            action.Add(new XElement(ns + "Parameters", BuildMasterKeyParameter(ns, child.Definition.ForeignKeyProperty, masterInstance, masterKey)));
            return action;
        }

        private static void PrepareLocalViewAction(XElement action, string instanceId, string executionType)
        {
            // An action inserted under a local Form event/handler must itself be local. Marking only
            // the leaf action inherited creates a partial reference tree that the Rule Designer cannot hydrate.
            action.SetAttributeValue("ID", NewId());
            action.SetAttributeValue("DefinitionID", NewId());
            action.SetAttributeValue("Type", "Execute");
            action.SetAttributeValue("InstanceID", instanceId);
            action.SetAttributeValue("ExecutionType", executionType);
            action.Attributes("IsReference").Remove();
            action.Attributes("IsInherited").Remove();
        }

        private static XElement BuildMasterKeyParameter(XNamespace ns, string target, string masterInstance, ResolvedViewField masterKey)
        {
            return new XElement(ns + "Parameter",
                new XAttribute("SourceID", masterKey.Id), new XAttribute("SourceName", masterKey.Name), new XAttribute("SourceDisplayName", masterKey.DisplayName),
                new XAttribute("SourceType", "ViewField"), new XAttribute("SourceInstanceID", masterInstance),
                new XAttribute("TargetID", target), new XAttribute("TargetName", target), new XAttribute("TargetDisplayName", target), new XAttribute("TargetType", "ObjectProperty"));
        }

        private static XElement Property(XNamespace ns, string name, string value, string display, string nameValue)
        {
            var result = new XElement(ns + "Property", new XElement(ns + "Name", name));
            if (display != null) result.Add(new XElement(ns + "DisplayValue", display));
            if (nameValue != null) result.Add(new XElement(ns + "NameValue", nameValue));
            result.Add(new XElement(ns + "Value", value));
            return result;
        }

        private static void SetElementProperty(XElement owner, string name, string value)
        {
            var ns = owner.Name.Namespace;
            var properties = owner.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
            if (properties == null) { properties = new XElement(ns + "Properties"); owner.Add(properties); }
            foreach (var old in properties.Elements().Where(x => x.Name.LocalName == "Property" &&
                string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase)).ToList()) old.Remove();
            properties.Add(Property(ns, name, value, value, null));
        }

        private static string ReadElementProperty(XElement owner, string name)
        {
            var property = owner.Elements().Where(x => x.Name.LocalName == "Properties").SelectMany(x => x.Elements())
                .FirstOrDefault(x => x.Name.LocalName == "Property" &&
                    string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
            return property == null ? null : ChildValue(property, "Value");
        }

        private static void VerifyBatch(XElement form, XElement scope, string masterInstance, string masterMethod, string masterPrototype,
            IList<ResolvedMasterDetailChild> children, IEnumerable<string> states, ResolvedViewField masterKey, string formName)
        {
            var master = FindMethodActions(scope, masterInstance, masterMethod, null)
                .FirstOrDefault(x => string.Equals((string)x.Attribute("ExecutionType"), "Parallel", StringComparison.OrdinalIgnoreCase));
            if (master == null)
                throw new CliException("K2 Form '" + formName + "' master method '" + masterMethod + "' is not configured for batch persistence.");
            if (!IsDesignerLoadableLocalViewAction(master, masterPrototype, masterInstance))
                throw new CliException("K2 Form '" + formName + "' master method '" + masterMethod + "' is not a Designer-loadable local View action.");
            var siblingActions = master.Parent.Elements().Where(x => x.Name.LocalName == "Action").ToList();
            foreach (var child in children)
            {
                var detailInstance = FindInstance(form, child.ViewGuid, child.ViewName, formName);
                foreach (var state in states)
                {
                    var match = siblingActions.FirstOrDefault(x => ActionMatchesInstance(x, detailInstance) &&
                        string.Equals((string)x.Attribute("ItemState"), state, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(ReadProperty(x, "Method"), MethodForState(child, state), StringComparison.OrdinalIgnoreCase) &&
                        string.Equals((string)x.Attribute("ExecutionType"), "Parallel", StringComparison.OrdinalIgnoreCase) &&
                        IsDesignerLoadableLocalViewAction(x, PrototypeForState(child, state), detailInstance) &&
                        (state == "Removed" || HasMasterKeyMapping(x, masterInstance, masterKey.Id, child.Definition.ForeignKeyProperty)));
                    if (match == null) throw new CliException("K2 Form '" + formName + "' master method '" + masterMethod + "' is missing a Designer-loadable local batch detail action " + child.ViewName + "/" + state + ". Candidates: " +
                        string.Join("; ", siblingActions.Where(x => string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase))
                            .Select(x => ReadProperty(x, "Method") + "/" + (string)x.Attribute("ItemState") + "/instance=" + (string)x.Attribute("InstanceID") +
                                "/view=" + ReadProperty(x, "ViewID") + "/definition=" + (string)x.Attribute("DefinitionID") +
                                "/execution=" + (string)x.Attribute("ExecutionType") + "/reference=" + (string)x.Attribute("IsReference") +
                                "/inherited=" + (string)x.Attribute("IsInherited") + "/mappings=" + HasCompleteInstanceMappings(x)).ToArray()));
                }
            }
        }

        private static bool IsDesignerLoadableLocalViewAction(XElement action, string prototypeXml, string expectedInstanceId)
        {
            if (action == null || string.IsNullOrWhiteSpace(prototypeXml)) return false;
            var prototype = XElement.Parse(prototypeXml);
            Guid definitionId;
            return Guid.TryParse((string)action.Attribute("DefinitionID"), out definitionId) &&
                !string.Equals((string)action.Attribute("DefinitionID"), (string)prototype.Attribute("DefinitionID"), StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)action.Attribute("InstanceID"), expectedInstanceId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals((string)action.Attribute("IsReference"), "True", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals((string)action.Attribute("IsInherited"), "True", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadProperty(action, "ViewID"), ReadProperty(prototype, "ViewID"), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadProperty(action, "Method"), ReadProperty(prototype, "Method"), StringComparison.OrdinalIgnoreCase) &&
                HasCompleteInstanceMappings(action);
        }

        private static bool HasCompleteInstanceMappings(XElement action)
        {
            return
                action.Descendants().Where(x => x.Name.LocalName == "Parameter" &&
                    (string.Equals((string)x.Attribute("SourceType"), "ViewField", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals((string)x.Attribute("SourceType"), "Control", StringComparison.OrdinalIgnoreCase)))
                    .All(x => !string.IsNullOrWhiteSpace((string)x.Attribute("SourceInstanceID"))) &&
                action.Descendants().Where(x => x.Name.LocalName == "Result" &&
                    (string.Equals((string)x.Attribute("TargetType"), "ViewField", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals((string)x.Attribute("TargetType"), "Control", StringComparison.OrdinalIgnoreCase)))
                    .All(x => !string.IsNullOrWhiteSpace((string)x.Attribute("TargetInstanceID")));
        }

        private static string PrototypeForState(ResolvedMasterDetailChild child, string state)
        {
            if (state == "Added") return child.CreateAction;
            if (state == "Changed") return child.UpdateAction;
            return child.DeleteAction;
        }

        private static void VerifySuccessMessages(XElement saveEvent, string masterInstance, ResolvedMasterDetailRules relationship, string formName)
        {
            foreach (var method in new[] { relationship.Definition.MasterCreateMethod, relationship.Definition.MasterUpdateMethod })
            {
                var master = FindMethodActions(saveEvent, masterInstance, method, null).FirstOrDefault();
                if (master == null || master.Parent == null) throw new CliException("K2 Form '" + formName + "' has no " + method + " persistence branch for success feedback.");
                var actions = master.Parent.Elements().Where(x => x.Name.LocalName == "Action").ToList();
                var message = actions.LastOrDefault();
                if (message == null || !string.Equals((string)message.Attribute("Type"), "ShowMessage", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals((string)message.Attribute("ExecutionType"), "Synchronous", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ReadProperty(message, "Location"), "Form", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ReadProperty(message, "MessageLocation"), "Popup", StringComparison.OrdinalIgnoreCase) ||
                    !HasMessageValue(message, "Type", "info") ||
                    !HasMessageValue(message, "Title", relationship.Definition.SuccessMessageTitle) ||
                    !HasMessageValue(message, "Body", relationship.Definition.SuccessMessageBody))
                    throw new CliException("K2 Form '" + formName + "' " + method + " branch does not finish with the configured success popup.");
            }
        }

        private static bool HasMessageValue(XElement action, string target, string expected)
        {
            var parameter = action.Elements().Where(x => x.Name.LocalName == "Parameters").SelectMany(x => x.Elements())
                .FirstOrDefault(x => x.Name.LocalName == "Parameter" && string.Equals((string)x.Attribute("TargetType"), "MessageProperty", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)x.Attribute("TargetID"), target, StringComparison.OrdinalIgnoreCase));
            if (parameter == null) return false;
            var value = string.Concat(parameter.Descendants().Where(x => x.Name.LocalName == "Source" &&
                string.Equals((string)x.Attribute("SourceType"), "Value", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value).ToArray());
            return string.Equals(value, expected, StringComparison.Ordinal);
        }

        private static bool HasMasterKeyMapping(XElement action, string masterInstance, string masterFieldId, string target)
        {
            return action.Descendants().Any(x => x.Name.LocalName == "Parameter" &&
                string.Equals((string)x.Attribute("SourceInstanceID"), masterInstance, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("SourceID"), masterFieldId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("TargetID"), target, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasMasterKeyNotBlankCondition(XElement action, string masterInstance, string masterFieldId)
        {
            var handler = action.Ancestors().FirstOrDefault(x => x.Name.LocalName == "Handler");
            if (handler == null) return false;
            return handler.Descendants().Any(x => x.Name.LocalName == "IsNotBlank" && x.Descendants().Any(y => y.Name.LocalName == "Item" &&
                string.Equals((string)y.Attribute("SourceType"), "ViewField", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)y.Attribute("SourceInstanceID"), masterInstance, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)y.Attribute("SourceID"), masterFieldId, StringComparison.OrdinalIgnoreCase)));
        }

        private static bool FollowsMasterRead(XElement action, string masterInstance, string masterReadMethod)
        {
            var handler = action.Ancestors().FirstOrDefault(x => x.Name.LocalName == "Handler");
            if (handler == null || handler.Parent == null) return false;
            return handler.ElementsBeforeSelf().Where(x => x.Name.LocalName == "Handler").Any(x =>
                FindMethodActions(x, masterInstance, masterReadMethod, null).Any());
        }

        private static IEnumerable<XElement> FindMethodActions(XElement form, string instanceId, string method, string state)
        {
            return form.Descendants().Where(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                ActionMatchesInstance(x, instanceId) &&
                string.Equals(ReadProperty(x, "Method"), method, StringComparison.OrdinalIgnoreCase) &&
                (state == null ? x.Attribute("ItemState") == null : string.Equals((string)x.Attribute("ItemState"), state, StringComparison.OrdinalIgnoreCase)));
        }

        private static bool HasMasterKeyBlankCondition(XElement action, string masterInstance, string masterFieldId)
        {
            var handler = action.Ancestors().FirstOrDefault(x => x.Name.LocalName == "Handler");
            if (handler == null) return false;
            return handler.Descendants().Any(x => x.Name.LocalName == "IsBlank" && x.Descendants().Any(y =>
                y.Name.LocalName == "Item" &&
                string.Equals((string)y.Attribute("SourceType"), "ViewField", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)y.Attribute("SourceInstanceID"), masterInstance, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)y.Attribute("SourceID"), masterFieldId, StringComparison.OrdinalIgnoreCase)));
        }

        private static IEnumerable<XElement> FindEventCalls(XElement scope, string instanceId, ResolvedViewEvent target)
        {
            if (target == null) return Enumerable.Empty<XElement>();
            return scope.Descendants().Where(x => IsEventCall(x, instanceId, target));
        }

        private static bool IsEventCall(XElement action, string instanceId, ResolvedViewEvent target)
        {
            if (action == null || target == null) return false;
            return string.Equals(action.Name.LocalName, "Action", StringComparison.Ordinal) &&
                string.Equals((string)action.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)action.Attribute("ExecutionType"), "Synchronous", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)action.Attribute("InstanceID"), instanceId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadProperty(action, "Location"), "Form", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadProperty(action, "EventID"), target.DefinitionId, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(ReadProperty(action, "Method")) &&
                string.IsNullOrWhiteSpace(ReadProperty(action, "ViewID")) &&
                !string.Equals((string)action.Attribute("IsReference"), "True", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals((string)action.Attribute("IsInherited"), "True", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasViewParameterTransfer(XElement action, string sourceInstance, string sourceField,
            string targetInstance, string targetParameter)
        {
            return action.Descendants().Any(x => x.Name.LocalName == "Parameter" &&
                string.Equals((string)x.Attribute("SourceType"), "ViewField", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("SourceInstanceID"), sourceInstance, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("SourceID"), sourceField, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("TargetType"), "ViewParameter", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("TargetInstanceID"), targetInstance, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("TargetID"), targetParameter, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<XElement> FindDetailListActions(XElement form, string instanceId, string method)
        {
            return FindMethodActions(form, instanceId, method, null).Where(x =>
                string.IsNullOrWhiteSpace(ReadProperty(x, "ControlID")));
        }

        private static bool ActionMatchesInstance(XElement action, string instanceId)
        {
            return string.Equals((string)action.Attribute("InstanceID"), instanceId, StringComparison.OrdinalIgnoreCase);
        }

        private static string FindInstance(XElement form, Guid viewGuid, string viewName, string formName)
        {
            var item = form.Descendants().FirstOrDefault(x => x.Name.LocalName == "Item" &&
                (string.Equals((string)x.Attribute("ViewID"), viewGuid.ToString(), StringComparison.OrdinalIgnoreCase) ||
                 string.Equals((string)x.Attribute("ViewName"), viewName, StringComparison.OrdinalIgnoreCase)));
            var id = item == null ? null : (string)item.Attribute("ID");
            if (string.IsNullOrWhiteSpace(id)) throw new CliException("Generated form '" + formName + "' has no view instance for '" + viewName + "' [" + viewGuid + "]. Available: " +
                string.Join("; ", form.Descendants().Where(x => x.Name.LocalName == "Item" && x.Attribute("ViewID") != null)
                    .Select(x => ((string)x.Attribute("ViewName") ?? ChildValue(x, "Name") ?? "<unnamed>") + " [" + (string)x.Attribute("ViewID") + "]").Distinct().ToArray()));
            return id;
        }

        private static string MethodForState(ResolvedMasterDetailChild child, string state)
        {
            if (state == "Added") return child.Definition.CreateMethod;
            if (state == "Changed") return child.Definition.UpdateMethod;
            return child.Definition.DeleteMethod;
        }

        private static string ReadProperty(XElement action, string name)
        {
            var property = action.Descendants().FirstOrDefault(x => x.Name.LocalName == "Property" && string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
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
