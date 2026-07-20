using System;
using System.Collections.Generic;
using System.Linq;
using SourceCode.Hosting.Client.BaseAPI;
using SourceCode.SmartObjects.Client;

namespace K2WorkflowCli
{
    internal sealed class SmartObjectDescriptor
    {
        public string SystemName { get; set; }
        public string DisplayName { get; set; }
        public string MethodSystemName { get; set; }
        public string MethodDisplayName { get; set; }
        public string MethodType { get; set; }
        public string DefaultLoadMethod { get; set; }
        public List<SmartObjectInputDescriptor> Inputs { get; set; }
        public SmartObjectInputDescriptor Identifier { get; set; }
        public SmartObjectInputDescriptor Status { get; set; }
        public string ReadMethodSystemName { get; set; }
        public string ReadMethodDisplayName { get; set; }
        public List<SmartObjectInputDescriptor> ReadReturns { get; set; }
    }

    internal sealed class SmartObjectInputDescriptor
    {
        public int InternalId { get; set; }
        public string SystemName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public bool IsRequired { get; set; }
    }

    internal static class SmartObjectMetadata
    {
        public static SmartObjectDescriptor Load(K2Settings k2, RequestStatusUpdateSettings settings)
        {
            var server = new SmartObjectClientServer();
            try
            {
                server.CreateConnection();
                var connection = new SCConnectionStringBuilder
                {
                    Authenticate = true,
                    Host = k2.Host,
                    Port = (uint)k2.Port,
                    Integrated = true,
                    IsPrimaryLogin = true,
                    SecurityLabelName = k2.SecurityLabel
                };
                server.Connection.Open(connection.ConnectionString);
                SmartObject smartObject;
                try { smartObject = server.GetSmartObject(settings.SmartObject); }
                catch (Exception ex) { throw new CliException("Unable to load request SmartObject '" + settings.SmartObject + "': " + ex.Message); }
                var method = smartObject.AllMethods.FirstOrDefault(x => string.Equals(x.Name, settings.Method, StringComparison.OrdinalIgnoreCase));
                if (method == null) throw new CliException("SmartObject method was not found: " + settings.SmartObject + "." + settings.Method);
                if (!string.Equals(method.Type.ToString(), "update", StringComparison.OrdinalIgnoreCase))
                    throw new CliException("Request status method must be an Update method; found " + method.Type + ".");

                var required = new HashSet<string>(method.RequiredProperties.Cast<SmartProperty>().Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
                var inputs = method.InputProperties.Cast<SmartProperty>().Select((property, index) => new SmartObjectInputDescriptor
                {
                    InternalId = index + 1,
                    SystemName = property.Name,
                    DisplayName = string.IsNullOrWhiteSpace(property.Metadata.DisplayName) ? property.Name : property.Metadata.DisplayName,
                    Description = property.Metadata.Description,
                    Type = WorkflowType(property.Type),
                    IsRequired = required.Contains(property.Name)
                }).ToList();
                var identifier = inputs.FirstOrDefault(x => string.Equals(x.SystemName, settings.IdentifierProperty, StringComparison.OrdinalIgnoreCase));
                var status = inputs.FirstOrDefault(x => string.Equals(x.SystemName, settings.StatusProperty, StringComparison.OrdinalIgnoreCase));
                if (identifier == null) throw new CliException("Identifier property is not an input of " + settings.Method + ": " + settings.IdentifierProperty);
                if (status == null) throw new CliException("Status property is not an input of " + settings.Method + ": " + settings.StatusProperty);
                var readMethod = smartObject.AllMethods.FirstOrDefault(x => string.Equals(x.Type.ToString(), "read", StringComparison.OrdinalIgnoreCase));
                var readReturns = readMethod == null ? new List<SmartObjectInputDescriptor>() :
                    readMethod.ReturnProperties.Cast<SmartProperty>().Select((property, index) => Describe(property, index + 1, false)).ToList();
                return new SmartObjectDescriptor
                {
                    SystemName = smartObject.Name,
                    DisplayName = string.IsNullOrWhiteSpace(smartObject.Metadata.DisplayName) ? smartObject.Name : smartObject.Metadata.DisplayName,
                    MethodSystemName = method.Name,
                    MethodDisplayName = string.IsNullOrWhiteSpace(method.Metadata.DisplayName) ? method.Name : method.Metadata.DisplayName,
                    MethodType = method.Type.ToString().ToLowerInvariant(),
                    DefaultLoadMethod = readMethod == null ? string.Empty : readMethod.Name,
                    ReadMethodSystemName = readMethod == null ? string.Empty : readMethod.Name,
                    ReadMethodDisplayName = readMethod == null || string.IsNullOrWhiteSpace(readMethod.Metadata.DisplayName) ? (readMethod == null ? string.Empty : readMethod.Name) : readMethod.Metadata.DisplayName,
                    ReadReturns = readReturns,
                    Inputs = inputs,
                    Identifier = identifier,
                    Status = status
                };
            }
            finally
            {
                if (server.Connection != null) { server.Connection.Close(); server.DeleteConnection(); }
            }
        }

        private static SmartObjectInputDescriptor Describe(SmartProperty property, int internalId, bool required)
        {
            return new SmartObjectInputDescriptor
            {
                InternalId = internalId,
                SystemName = property.Name,
                DisplayName = string.IsNullOrWhiteSpace(property.Metadata.DisplayName) ? property.Name : property.Metadata.DisplayName,
                Description = property.Metadata.Description,
                Type = WorkflowType(property.Type),
                IsRequired = required
            };
        }

        private static string WorkflowType(PropertyType type)
        {
            switch (type.ToString().ToLowerInvariant())
            {
                case "autonumber": return "autoNumber";
                case "datetime": return "dateTime";
                case "decimal": return "decimal";
                case "number": return "number";
                case "yesno": return "yesNo";
                case "guid": return "guid";
                case "memo": return "memo";
                default: return "text";
            }
        }
    }
}
