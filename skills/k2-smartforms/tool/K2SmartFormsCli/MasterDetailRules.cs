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
                DisplayName = ChildValue(field, "FieldDisplayName") ?? property
            };
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
    }

    internal static class MasterDetailRules
    {
        public static string Apply(string xml, FormDefinition formDefinition, ResolvedMasterDetailRules relationship)
        {
            if (relationship == null) return xml;
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var form = document.Descendants().First(x => x.Name.LocalName == "Form");
            var masterInstance = FindInstance(form, relationship.MasterViewGuid, relationship.MasterViewName, formDefinition.Name);

            var masterCreate = FindMethodActions(form, masterInstance, relationship.Definition.MasterCreateMethod, null).FirstOrDefault();
            if (masterCreate == null) throw new CliException("Generated form '" + formDefinition.Name + "' has no master Create action for '" + relationship.MasterViewName + "'.");
            var masterUpdate = FindMethodActions(form, masterInstance, relationship.Definition.MasterUpdateMethod, null).FirstOrDefault();
            if (masterUpdate == null) throw new CliException("Generated form '" + formDefinition.Name + "' has no master Update action for '" + relationship.MasterViewName + "'.");

            var customCreate = BuildMasterAction(relationship.MasterCreateAction, masterInstance);
            masterCreate.ReplaceWith(customCreate);
            masterCreate = customCreate;
            var customUpdate = BuildMasterAction(relationship.MasterUpdateAction, masterInstance);
            masterUpdate.ReplaceWith(customUpdate);
            masterUpdate = customUpdate;

            AddBatchActions(masterCreate, relationship.Details.Select(x => BuildStateAction(form.Name.Namespace, x, x.CreateAction, "Added", masterInstance, relationship.MasterKey, FindInstance(form, x.ViewGuid, x.ViewName, formDefinition.Name))).ToList());
            var updateActions = new List<XElement>();
            foreach (var child in relationship.Details)
            {
                var detailInstance = FindInstance(form, child.ViewGuid, child.ViewName, formDefinition.Name);
                updateActions.Add(BuildStateAction(form.Name.Namespace, child, child.UpdateAction, "Changed", masterInstance, relationship.MasterKey, detailInstance));
                updateActions.Add(BuildStateAction(form.Name.Namespace, child, child.CreateAction, "Added", masterInstance, relationship.MasterKey, detailInstance));
                updateActions.Add(BuildStateAction(form.Name.Namespace, child, child.DeleteAction, "Removed", masterInstance, relationship.MasterKey, detailInstance));
            }
            AddBatchActions(masterUpdate, updateActions);

            var masterReads = FindMethodActions(form, masterInstance, relationship.Definition.MasterReadMethod, null).ToList();
            if (masterReads.Count == 0)
                throw new CliException("Generated form '" + formDefinition.Name + "' has no master Read action for '" + relationship.MasterViewName + "'.");
            foreach (var read in masterReads)
            {
                var actions = read.Parent;
                foreach (var child in relationship.Details)
                {
                    var detailInstance = FindInstance(form, child.ViewGuid, child.ViewName, formDefinition.Name);
                    actions.Add(BuildListAction(form.Name.Namespace, child, masterInstance, relationship.MasterKey, detailInstance));
                }
            }
            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static void Verify(string xml, FormDefinition formDefinition, ResolvedMasterDetailRules relationship)
        {
            if (relationship == null) return;
            var document = XDocument.Parse(xml);
            var form = document.Descendants().First(x => x.Name.LocalName == "Form");
            var masterInstance = FindInstance(form, relationship.MasterViewGuid, relationship.MasterViewName, formDefinition.Name);
            VerifyBatch(form, masterInstance, relationship.Definition.MasterCreateMethod, relationship.Details, new[] { "Added" }, relationship.MasterKey, formDefinition.Name);
            VerifyBatch(form, masterInstance, relationship.Definition.MasterUpdateMethod, relationship.Details, new[] { "Changed", "Added", "Removed" }, relationship.MasterKey, formDefinition.Name);
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
                if (!FindMethodActions(form, detailInstance, child.Definition.ListMethod, null)
                    .Any(x => HasMasterKeyMapping(x, masterInstance, relationship.MasterKey.Id, child.Definition.ForeignKeyProperty)))
                    throw new CliException("K2 Form '" + formDefinition.Name + "' loads the master but has no filtered Form-level List action for detail view '" + child.ViewName + "'.");
            }
            Console.WriteLine("Master-detail form rules: OK (" + formDefinition.Name + ", master=" + relationship.MasterViewName + ", details=" + relationship.Details.Count + ")");
        }

        private static void AddBatchActions(XElement masterAction, IList<XElement> childActions)
        {
            masterAction.SetAttributeValue("ExecutionType", "Parallel");
            var actions = masterAction.Parent;
            foreach (var existing in actions.Elements().Where(x => x.Name.LocalName == "Action" && childActions.Any(c => SameGeneratedAction(x, c))).ToList()) existing.Remove();
            foreach (var child in childActions) actions.Add(child);
        }

        private static XElement BuildMasterAction(string prototypeXml, string masterInstance)
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

        private static void VerifyBatch(XElement form, string masterInstance, string masterMethod, IList<ResolvedMasterDetailChild> children, IEnumerable<string> states, ResolvedViewField masterKey, string formName)
        {
            var master = FindMethodActions(form, masterInstance, masterMethod, null)
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

        private static bool HasMasterKeyMapping(XElement action, string masterInstance, string masterFieldId, string target)
        {
            return action.Descendants().Any(x => x.Name.LocalName == "Parameter" &&
                string.Equals((string)x.Attribute("SourceInstanceID"), masterInstance, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("SourceID"), masterFieldId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("TargetID"), target, StringComparison.OrdinalIgnoreCase));
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
            if (string.IsNullOrWhiteSpace(id)) throw new CliException("Generated form '" + formName + "' has no view instance for '" + viewName + "'.");
            return id;
        }

        private static bool SameGeneratedAction(XElement left, XElement right)
        {
            return string.Equals((string)left.Attribute("InstanceID"), (string)right.Attribute("InstanceID"), StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)left.Attribute("ItemState"), (string)right.Attribute("ItemState"), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadProperty(left, "Method"), ReadProperty(right, "Method"), StringComparison.OrdinalIgnoreCase) &&
                left.Descendants().Any(x => x.Name.LocalName == "Parameter" && right.Descendants().Any(y => y.Name.LocalName == "Parameter" &&
                    string.Equals((string)x.Attribute("TargetID"), (string)y.Attribute("TargetID"), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)x.Attribute("SourceInstanceID"), (string)y.Attribute("SourceInstanceID"), StringComparison.OrdinalIgnoreCase)));
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
