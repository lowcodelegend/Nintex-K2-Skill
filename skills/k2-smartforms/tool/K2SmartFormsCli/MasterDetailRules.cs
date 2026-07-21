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
        public List<ResolvedMasterDetailChild> Details { get; set; }

        public static ResolvedMasterDetailRules Resolve(FormsManager manager, FormDefinition form)
        {
            if (form.MasterDetail == null) return null;
            var masterInfo = manager.GetView(form.MasterDetail.MasterView);
            var masterDocument = XDocument.Parse(manager.GetViewDefinition(masterInfo.Guid));
            var result = new ResolvedMasterDetailRules
            {
                Definition = form.MasterDetail,
                MasterViewGuid = masterInfo.Guid,
                MasterViewName = masterInfo.Name,
                MasterKey = ResolveField(masterDocument, form.MasterDetail.MasterKeyProperty, form.MasterDetail.MasterView),
                MasterCreateAction = ResolveAction(masterDocument, form.MasterDetail.MasterCreateMethod, null, form.MasterDetail.MasterView),
                MasterUpdateAction = ResolveAction(masterDocument, form.MasterDetail.MasterUpdateMethod, null, form.MasterDetail.MasterView),
                Details = new List<ResolvedMasterDetailChild>()
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
                    ListAction = ResolveOptionalAction(document, child.ListMethod, null)
                });
            }
            return result;
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
            var action = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadProperty(x, "Method"), method, StringComparison.OrdinalIgnoreCase) &&
                (state == null ? x.Attribute("ItemState") == null : string.Equals((string)x.Attribute("ItemState"), state, StringComparison.OrdinalIgnoreCase)));
            return action == null ? null : action.ToString(SaveOptions.DisableFormatting);
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
    }

    internal sealed class ResolvedViewField
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string DataType { get; set; }
    }

    internal static class MasterDetailRules
    {
        public static string SuppressUnfilteredDetailLoads(string xml, string viewName, IEnumerable<MasterDetailChildDefinition> relationships)
        {
            var methods = new HashSet<string>(relationships.Select(x => x.ListMethod), StringComparer.OrdinalIgnoreCase);
            if (methods.Count == 0) return xml;
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var actions = document.Descendants().Where(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                x.Attribute("ItemState") == null && methods.Contains(ReadProperty(x, "Method"))).ToList();
            foreach (var action in actions) RemoveActionAndEmptyHandler(action);
            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static void VerifyDetailViewLoads(string xml, string viewName, IEnumerable<MasterDetailChildDefinition> relationships)
        {
            var methods = new HashSet<string>(relationships.Select(x => x.ListMethod), StringComparer.OrdinalIgnoreCase);
            if (methods.Count == 0) return;
            var document = XDocument.Parse(xml);
            var unfiltered = document.Descendants().Where(x => x.Name.LocalName == "Action" &&
                string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                x.Attribute("ItemState") == null && methods.Contains(ReadProperty(x, "Method"))).ToList();
            if (unfiltered.Count > 0)
                throw new CliException("Master-detail View '" + viewName + "' contains an unfiltered List rule. Detail loading must be owned by the Form and supplied with the master key.");
        }

        public static string Apply(string xml, FormDefinition formDefinition, ResolvedMasterDetailRules relationship)
        {
            if (relationship == null) return xml;
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var form = document.Descendants().First(x => x.Name.LocalName == "Form");
            var masterInstance = FindInstance(form, relationship.MasterViewGuid, relationship.MasterViewName, formDefinition.Name);

            AddFormSaveButton(form, formDefinition, relationship, masterInstance);

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

        public static void Verify(string xml, FormDefinition formDefinition, ResolvedMasterDetailRules relationship)
        {
            if (relationship == null) return;
            var document = XDocument.Parse(xml);
            var form = document.Descendants().First(x => x.Name.LocalName == "Form");
            var masterInstance = FindInstance(form, relationship.MasterViewGuid, relationship.MasterViewName, formDefinition.Name);
            var saveEvent = FindFormSaveEvent(form, relationship.Definition.SaveButtonText);
            if (saveEvent == null) throw new CliException("K2 Form '" + formDefinition.Name + "' has no Form-level master-detail Save button rule.");
            VerifyBatch(form, saveEvent, masterInstance, relationship.Definition.MasterCreateMethod, relationship.Details, new[] { "Added" }, relationship.MasterKey, formDefinition.Name);
            VerifyBatch(form, saveEvent, masterInstance, relationship.Definition.MasterUpdateMethod, relationship.Details, new[] { "Changed", "Added", "Removed" }, relationship.MasterKey, formDefinition.Name);
            VerifySuccessMessages(saveEvent, masterInstance, relationship, formDefinition.Name);
            var create = FindMethodActions(saveEvent, masterInstance, relationship.Definition.MasterCreateMethod, null).First();
            if (!HasMasterKeyResult(create, masterInstance, relationship.MasterKey.Id))
                throw new CliException("K2 Form '" + formDefinition.Name + "' Form-level Create does not transfer the generated master key back to the master View field.");
            var masterReads = FindMethodActions(form, masterInstance, relationship.Definition.MasterReadMethod, null).ToList();
            if (masterReads.Count == 0)
                throw new CliException("K2 Form '" + formDefinition.Name + "' has no master Read action for '" + relationship.MasterViewName + "'.");
            foreach (var child in relationship.Details)
            {
                var detailInstance = FindInstance(form, child.ViewGuid, child.ViewName, formDefinition.Name);
                foreach (var state in new[] { "Added", "Changed", "Removed" })
                {
                    var matches = FindMethodActions(form, detailInstance, MethodForState(child, state), state)
                        .Where(x => state == "Removed" || HasMasterKeyMapping(x, masterInstance, relationship.MasterKey.Id, child.Definition.ForeignKeyProperty)).ToList();
                    if (matches.Count == 0)
                        throw new CliException("K2 Form '" + formDefinition.Name + "' has no valid Form-level " + state + " persistence action for detail view '" + child.ViewName + "'.");
                }
                var listActions = FindMethodActions(form, detailInstance, child.Definition.ListMethod, null).ToList();
                if (listActions.Count == 0)
                    throw new CliException("K2 Form '" + formDefinition.Name + "' loads the master but has no Form-level List action for detail view '" + child.ViewName + "'.");
                if (listActions.Any(x => !HasMasterKeyMapping(x, masterInstance, relationship.MasterKey.Id, child.Definition.ForeignKeyProperty)))
                    throw new CliException("K2 Form '" + formDefinition.Name + "' has an unfiltered List action for detail view '" + child.ViewName + "'. Every detail List must receive the master key as the foreign-key input.");
                if (listActions.Any(x => !HasMasterKeyNotBlankCondition(x, masterInstance, relationship.MasterKey.Id)))
                    throw new CliException("K2 Form '" + formDefinition.Name + "' has an ungated List action for detail view '" + child.ViewName + "'. Detail List actions must run only when the master key is not blank.");
                if (listActions.Any(x => !FollowsMasterRead(x, masterInstance, relationship.Definition.MasterReadMethod)))
                    throw new CliException("K2 Form '" + formDefinition.Name + "' has a detail List action that does not follow a master Read for view '" + child.ViewName + "'.");
            }
            Console.WriteLine("Master-detail form rules: OK (" + formDefinition.Name + ", master=" + relationship.MasterViewName + ", details=" + relationship.Details.Count + ")");
        }

        private static void AddFormSaveButton(XElement form, FormDefinition formDefinition, ResolvedMasterDetailRules relationship, string masterInstance)
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
            handlers.Add(BuildSaveHandler(form, relationship, masterInstance, true));
            handlers.Add(BuildSaveHandler(form, relationship, masterInstance, false));
            events.Add(saveEvent);
        }

        private static XElement BuildSaveHandler(XElement form, ResolvedMasterDetailRules relationship, string masterInstance, bool create)
        {
            var ns = form.Name.Namespace;
            var actions = new XElement(ns + "Actions");
            var master = BuildMasterAction(create ? relationship.MasterCreateAction : relationship.MasterUpdateAction, masterInstance, relationship.MasterKey);
            actions.Add(master);
            if (create)
            {
                foreach (var child in relationship.Details)
                    actions.Add(BuildStateAction(ns, child, child.CreateAction, "Added", masterInstance, relationship.MasterKey, FindInstance(form, child.ViewGuid, child.ViewName, ChildValue(form, "Name"))));
            }
            else
            {
                foreach (var child in relationship.Details)
                {
                    var detailInstance = FindInstance(form, child.ViewGuid, child.ViewName, ChildValue(form, "Name"));
                    actions.Add(BuildStateAction(ns, child, child.UpdateAction, "Changed", masterInstance, relationship.MasterKey, detailInstance));
                    actions.Add(BuildStateAction(ns, child, child.CreateAction, "Added", masterInstance, relationship.MasterKey, detailInstance));
                    actions.Add(BuildStateAction(ns, child, child.DeleteAction, "Removed", masterInstance, relationship.MasterKey, detailInstance));
                }
            }
            actions.Add(BuildSuccessMessage(ns, relationship.Definition.SuccessMessageTitle, relationship.Definition.SuccessMessageBody));
            return new XElement(ns + "Handler", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XElement(ns + "Properties", Property(ns, "HandlerName", "IfLogicalHandler", null, null), Property(ns, "Location", "form", null, null)),
                new XElement(ns + "Conditions", BuildMasterKeyCondition(ns, masterInstance, relationship.MasterKey, !create)), actions);
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
            foreach (var child in relationship.Details)
            {
                var detailInstance = FindInstance(form, child.ViewGuid, child.ViewName, formName);
                actions.Add(BuildListAction(ns, child, masterInstance, relationship.MasterKey, detailInstance));
            }
            return new XElement(ns + "Handler", new XAttribute("ID", NewId()), new XAttribute("DefinitionID", NewId()),
                new XElement(ns + "Properties", Property(ns, "HandlerName", "IfLogicalHandler", null, null), Property(ns, "Location", "form", null, null)),
                new XElement(ns + "Conditions", BuildMasterKeyCondition(ns, masterInstance, relationship.MasterKey, true)), actions);
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
            var actions = FindMethodActions(form, detailInstance, child.Definition.ListMethod, null).ToList();
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
            action.SetAttributeValue("ID", NewId());
            action.SetAttributeValue("DefinitionID", NewId());
            action.SetAttributeValue("InstanceID", masterInstance);
            action.SetAttributeValue("ExecutionType", "Parallel");
            action.Attributes("IsReference").Remove();
            action.Attributes("IsInherited").Remove();
            foreach (var parameter in action.Descendants().Where(x => x.Name.LocalName == "Parameter" && string.Equals((string)x.Attribute("SourceType"), "ViewField", StringComparison.OrdinalIgnoreCase)))
                parameter.SetAttributeValue("SourceInstanceID", masterInstance);
            foreach (var result in action.Descendants().Where(x => x.Name.LocalName == "Result" && string.Equals((string)x.Attribute("TargetType"), "ViewField", StringComparison.OrdinalIgnoreCase)))
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
            action.SetAttributeValue("ID", NewId());
            action.SetAttributeValue("DefinitionID", NewId());
            action.SetAttributeValue("InstanceID", detailInstance);
            action.SetAttributeValue("ExecutionType", "Parallel");
            action.SetAttributeValue("ItemState", state);
            action.Attributes("IsReference").Remove();
            action.Attributes("IsInherited").Remove();
            var parameters = action.Elements().FirstOrDefault(x => x.Name.LocalName == "Parameters");
            if (parameters == null) { parameters = new XElement(ns + "Parameters"); action.Add(parameters); }
            foreach (var parameter in parameters.Elements().Where(x => x.Name.LocalName == "Parameter"))
            {
                if (string.Equals((string)parameter.Attribute("TargetID"), child.Definition.ForeignKeyProperty, StringComparison.OrdinalIgnoreCase))
                {
                    parameter.Remove();
                    continue;
                }
                if (string.Equals((string)parameter.Attribute("SourceType"), "ViewField", StringComparison.OrdinalIgnoreCase))
                    parameter.SetAttributeValue("SourceInstanceID", detailInstance);
            }
            if (state != "Removed")
                parameters.AddFirst(BuildMasterKeyParameter(ns, child.Definition.ForeignKeyProperty, masterInstance, masterKey));
            var results = action.Elements().FirstOrDefault(x => x.Name.LocalName == "Results");
            if (results != null)
                foreach (var result in results.Elements().Where(x => string.Equals((string)x.Attribute("TargetType"), "ViewField", StringComparison.OrdinalIgnoreCase)))
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
            action.SetAttributeValue("ID", NewId());
            action.SetAttributeValue("DefinitionID", NewId());
            action.SetAttributeValue("Type", "Execute");
            action.SetAttributeValue("ExecutionType", "Synchronous");
            action.SetAttributeValue("InstanceID", detailInstance);
            action.Attributes("ItemState").Remove();
            action.Attributes("IsReference").Remove();
            action.Attributes("IsInherited").Remove();
            action.Add(new XElement(ns + "Parameters", BuildMasterKeyParameter(ns, child.Definition.ForeignKeyProperty, masterInstance, masterKey)));
            return action;
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

        private static void VerifyBatch(XElement form, XElement scope, string masterInstance, string masterMethod, IList<ResolvedMasterDetailChild> children, IEnumerable<string> states, ResolvedViewField masterKey, string formName)
        {
            var master = FindMethodActions(scope, masterInstance, masterMethod, null)
                .FirstOrDefault(x => string.Equals((string)x.Attribute("ExecutionType"), "Parallel", StringComparison.OrdinalIgnoreCase));
            if (master == null)
                throw new CliException("K2 Form '" + formName + "' master method '" + masterMethod + "' is not configured for batch persistence.");
            var siblingActions = master.Parent.Elements().Where(x => x.Name.LocalName == "Action").ToList();
            foreach (var child in children)
            {
                var detailInstance = FindInstance(form, child.ViewGuid, child.ViewName, formName);
                foreach (var state in states)
                {
                    var match = siblingActions.FirstOrDefault(x => string.Equals((string)x.Attribute("InstanceID"), detailInstance, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals((string)x.Attribute("ItemState"), state, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(ReadProperty(x, "Method"), MethodForState(child, state), StringComparison.OrdinalIgnoreCase) &&
                        string.Equals((string)x.Attribute("ExecutionType"), "Parallel", StringComparison.OrdinalIgnoreCase) &&
                        (state == "Removed" || HasMasterKeyMapping(x, masterInstance, masterKey.Id, child.Definition.ForeignKeyProperty)));
                    if (match == null) throw new CliException("K2 Form '" + formName + "' master method '" + masterMethod + "' is missing batch detail action " + child.ViewName + "/" + state + ".");
                }
            }
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
                string.Equals((string)x.Attribute("InstanceID"), instanceId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadProperty(x, "Method"), method, StringComparison.OrdinalIgnoreCase) &&
                (state == null ? x.Attribute("ItemState") == null : string.Equals((string)x.Attribute("ItemState"), state, StringComparison.OrdinalIgnoreCase)));
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
