using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartBoxCli
{
    internal static class SmartBoxDefinitionBuilder
    {
        internal const string ServiceClass = "SourceCode.SmartObjects.Services.SmartBox.SBService";
        private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";
        private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

        public static string Build(
            SmartObjectDefinition definition,
            Guid smartObjectGuid,
            Guid serviceInstanceGuid,
            string serviceInstanceName,
            string serviceObjectName,
            Guid serviceObjectGuid)
        {
            var key = definition.Properties.Single(x => x.Key);
            var root = new XElement("smartobjectroot",
                new XAttribute("name", definition.SystemName),
                new XAttribute("guid", smartObjectGuid),
                new XAttribute("version", "0"),
                new XAttribute("isextendible", "true"),
                new XAttribute("mode", "simple"),
                new XAttribute("createdfromlocal", "false"),
                Metadata(definition.DisplayName, definition.Description,
                    new XElement("key", new XAttribute("name", "serviceinstance"), serviceInstanceGuid),
                    new XElement("key", new XAttribute("name", "serviceobject"), serviceObjectName)),
                new XElement("types", new XElement("type", new XAttribute("name", "user"))),
                new XElement("properties", definition.Properties.Select(RootProperty)),
                new XElement("methods", Methods().Select(method =>
                    RootMethod(method, definition, key, serviceInstanceGuid, serviceInstanceName, serviceObjectName, serviceObjectGuid))),
                new XElement("defaults", new XElement("methods",
                    new XElement("read", new XAttribute("name", "Load")),
                    new XElement("list", new XAttribute("name", "GetList")),
                    new XElement("report", new XAttribute("name", "GetList")))),
                new XElement("associations"),
                new XElement("extendingobject",
                    ExtendingObject(definition, key, serviceInstanceGuid, serviceObjectName, serviceObjectGuid)));
            return new XDocument(root).ToString(SaveOptions.DisableFormatting);
        }

        private static XElement RootProperty(PropertyDefinition property)
        {
            return new XElement("property",
                new XAttribute("name", property.Name),
                new XAttribute("type", property.Type),
                new XAttribute("unique", Bool(property.Key)),
                new XAttribute("system", "false"),
                new XAttribute("required", Bool(property.Required)),
                Metadata(property.DisplayName, property.Description));
        }

        private static XElement RootMethod(
            MethodShape method,
            SmartObjectDefinition definition,
            PropertyDefinition key,
            Guid serviceInstanceGuid,
            string serviceInstanceName,
            string serviceObjectName,
            Guid serviceObjectGuid)
        {
            return new XElement("method",
                new XAttribute("name", method.Name),
                new XAttribute("type", method.Type),
                new XAttribute("transaction", "continue"),
                new XAttribute("execblockno", "0"),
                Metadata(method.DisplayName, method.Description),
                new XElement("serviceinstances",
                    new XElement("serviceinstance",
                        new XAttribute("name", serviceInstanceName),
                        new XAttribute("guid", serviceInstanceGuid),
                        new XAttribute("type", ServiceClass),
                        new XAttribute("execblock", "0"),
                        Metadata("SmartBox Service", null),
                        new XElement("objects",
                            ServiceObject(definition, key, serviceObjectName, serviceObjectGuid, method)))),
                new XElement("parameters"));
        }

        private static XElement ServiceObject(
            SmartObjectDefinition definition,
            PropertyDefinition key,
            string serviceObjectName,
            Guid serviceObjectGuid,
            MethodShape method)
        {
            return new XElement("object",
                new XAttribute("name", serviceObjectName),
                new XAttribute("version", ""),
                new XAttribute("type", "default"),
                ObjectMetadata(definition, key, serviceObjectGuid),
                new XElement("properties", definition.Properties.Select(ServiceProperty)),
                new XElement("methods", ServiceMethod(method, definition.Properties, key)));
        }

        private static XElement ServiceProperty(PropertyDefinition property)
        {
            var shape = PropertyShape.For(property);
            var serviceKeys = new List<XElement>
            {
                BooleanKey("uniqueid", property.Key),
                BooleanKey("autonumber", property.Type == "autonumber")
            };
            if (property.Type == "text")
                serviceKeys.Add(new XElement("key", new XAttribute("name", "maxsize"), property.MaxLength));
            if (property.Key)
                serviceKeys.Add(new XElement("key", new XAttribute("name", "unique"), "true"));
            return new XElement("property",
                new XAttribute("name", property.Name),
                new XAttribute("type", shape.ClrType),
                new XAttribute("extendtype", shape.ExtendType),
                new XAttribute("sotype", property.Type),
                Metadata(property.DisplayName, property.Description, serviceKeys.ToArray()),
                new XElement("mappings",
                    new XElement("mapping", new XAttribute("type", "property"),
                        new XElement("property", new XAttribute("name", property.Name)))));
        }

        private static XElement ServiceMethod(
            MethodShape method,
            IEnumerable<PropertyDefinition> properties,
            PropertyDefinition key)
        {
            var all = properties.ToList();
            var required = method.RequiresKey
                ? new[] { key }
                : all.Where(x => x.Required && x.Type != "autonumber" && x.Type != "autoguid").ToArray();
            IEnumerable<PropertyDefinition> input = method.InputAll ? (IEnumerable<PropertyDefinition>)all :
                method.RequiresKey ? new[] { key } : new PropertyDefinition[0];
            IEnumerable<PropertyDefinition> output = method.ReturnAll ? (IEnumerable<PropertyDefinition>)all :
                method.ReturnGeneratedKey && (key.Type == "autonumber" || key.Type == "autoguid")
                    ? new[] { key }
                    : new PropertyDefinition[0];
            return new XElement("method",
                new XAttribute("name", method.Name),
                new XAttribute("type", method.Type),
                Metadata(method.DisplayName, method.Description),
                new XElement("parameters"),
                new XElement("validation",
                    new XElement("requiredproperties", required.Select(PropertyReference))),
                new XElement("input", input.Select(PropertyReference)),
                new XElement("return", output.Select(PropertyReference)));
        }

        private static XElement ExtendingObject(
            SmartObjectDefinition definition,
            PropertyDefinition key,
            Guid serviceInstanceGuid,
            string serviceObjectName,
            Guid serviceObjectGuid)
        {
            return new XElement("objectdata",
                new XAttribute("name", serviceObjectName),
                new XAttribute("type", "Default"),
                new XAttribute("serviceinstanceguid", serviceInstanceGuid),
                ObjectMetadata(definition, key, serviceObjectGuid),
                new XElement("properties", definition.Properties.Select(ExtendingProperty)),
                new XElement("methods", Methods().Select(ExtendingMethod)));
        }

        private static XElement ExtendingProperty(PropertyDefinition property)
        {
            var shape = PropertyShape.For(property);
            var keys = new List<XElement>
            {
                BooleanKey("uniqueid", property.Key),
                BooleanKey("autonumber", property.Type == "autonumber")
            };
            if (property.Type == "text")
                keys.Add(new XElement("key", new XAttribute("name", "maxsize"), property.MaxLength));
            if (property.Key)
                keys.Add(new XElement("key", new XAttribute("name", "unique"), "true"));
            return new XElement("propertydata",
                new XAttribute("name", property.Name),
                new XAttribute("type", shape.ClrType),
                new XAttribute("extendtype", shape.ExtendType),
                new XAttribute("sotype", property.Type),
                Metadata(property.DisplayName, property.Description, keys.ToArray()));
        }

        private static XElement ExtendingMethod(MethodShape method)
        {
            var requiredExtendTypes = method.RequiresKey
                ? new[] { "uniqueid", "uniqueidauto" }
                : new string[0];
            var inputExtendTypes = method.InputAll
                ? new[] { "uniqueid", "uniqueidauto", "default", "uniqueauto" }
                : requiredExtendTypes;
            var returnExtendTypes = method.ReturnAll
                ? new[] { "uniqueid", "uniqueidauto", "default", "uniqueauto" }
                : method.ReturnGeneratedKey ? new[] { "uniqueidauto", "uniqueauto" } : new string[0];
            return new XElement("methoddata",
                new XAttribute("name", method.Name),
                new XAttribute("type", method.Type),
                new XAttribute("isdefined", "true"),
                Metadata(method.DisplayName, method.Description),
                new XElement("validation",
                    new XElement("requiredproperties",
                        new XElement("properties"),
                        new XElement("extendtypes", requiredExtendTypes.Select(ExtendType)))),
                new XElement("parameters"),
                new XElement("input",
                    new XElement("properties"),
                    new XElement("extendtypes", inputExtendTypes.Select(ExtendType))),
                new XElement("return",
                    new XElement("properties"),
                    new XElement("extendtypes", returnExtendTypes.Select(ExtendType))));
        }

        private static XElement ObjectMetadata(SmartObjectDefinition definition, PropertyDefinition key, Guid serviceObjectGuid)
        {
            return Metadata(definition.DisplayName, definition.Description,
                new XElement("key", new XAttribute("name", "idprops"), StringListObject(key.Name)),
                new XElement("key", new XAttribute("name", "noofuniqueproperties"),
                    new XElement("object", new XAttribute("type", "System.Int32"),
                        new XElement("int", "1"))),
                new XElement("key", new XAttribute("name", "autonumberprop"),
                    key.Type == "autonumber" ? key.Name : string.Empty),
                new XElement("key", new XAttribute("name", "autoguidprop"),
                    key.Type == "autoguid" ? key.Name : string.Empty),
                new XElement("key", new XAttribute("name", "guid"),
                    new XElement("object", new XAttribute("type", "System.Guid"),
                        new XElement("guid", serviceObjectGuid))));
        }

        private static XElement StringListObject(string value)
        {
            return new XElement("object",
                new XAttribute("type", "System.Collections.Generic.List`1[[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]"),
                new XElement("ArrayOfString",
                    new XAttribute(XNamespace.Xmlns + "xsd", Xsd),
                    new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
                    new XElement("string", value)));
        }

        private static XElement BooleanKey(string name, bool value)
        {
            return new XElement("key", new XAttribute("name", name),
                new XElement("object", new XAttribute("type", "System.Boolean"),
                    new XElement("boolean", Bool(value))));
        }

        private static XElement Metadata(string displayName, string description, params XElement[] serviceKeys)
        {
            return new XElement("metadata",
                new XElement("display",
                    new XElement("displayname", displayName ?? string.Empty),
                    new XElement("description", description ?? string.Empty)),
                new XElement("service", serviceKeys));
        }

        private static XElement PropertyReference(PropertyDefinition property)
        {
            return new XElement("property", new XAttribute("name", property.Name));
        }

        private static XElement ExtendType(string name)
        {
            return new XElement("extendtype", new XAttribute("name", name));
        }

        private static IEnumerable<MethodShape> Methods()
        {
            yield return new MethodShape("Create", "create", "Create", "This method creates a new entry", true, false, false, true);
            yield return new MethodShape("Save", "update", "Save", "This method updates an entry, or creates it if it does not exist.", true, false, false, true);
            yield return new MethodShape("Delete", "delete", "Delete", "This method deletes a single entry", false, true, false, false);
            yield return new MethodShape("Load", "read", "Load", "This method loads a single entry", false, true, true, false);
            yield return new MethodShape("GetList", "list", "Get List", "This method gets a list of entries", true, false, true, false);
        }

        private static string Bool(bool value) { return value ? "true" : "false"; }

        private sealed class MethodShape
        {
            public MethodShape(string name, string type, string displayName, string description,
                bool inputAll, bool requiresKey, bool returnAll, bool returnGeneratedKey)
            {
                Name = name; Type = type; DisplayName = displayName; Description = description;
                InputAll = inputAll; RequiresKey = requiresKey; ReturnAll = returnAll;
                ReturnGeneratedKey = returnGeneratedKey;
            }
            public string Name, Type, DisplayName, Description;
            public bool InputAll, RequiresKey, ReturnAll, ReturnGeneratedKey;
        }

        private sealed class PropertyShape
        {
            public string ClrType, ExtendType;
            public static PropertyShape For(PropertyDefinition property)
            {
                switch (property.Type)
                {
                    case "autonumber": return New("System.Int64", "UniqueIdAuto");
                    case "autoguid": return New("", "UniqueIdAuto");
                    case "number": return New("System.Decimal", "Default");
                    case "yesno": return New("System.Boolean", "Default");
                    case "date":
                    case "datetime": return New("System.DateTime", "Default");
                    case "guid": return New("System.Guid", "Default");
                    default: return New("", "Default");
                }
            }
            private static PropertyShape New(string clr, string extend)
            {
                return new PropertyShape { ClrType = clr, ExtendType = extend };
            }
        }
    }
}
