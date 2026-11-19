using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SourceCode.Hosting.Client.BaseAPI;
using SourceCode.SmartObjects.Management;
using SourceCode.SmartObjects.Services.Management;

namespace K2EnvironmentCli
{
    internal static class WorkflowImporterSetup
    {
        private const string ServiceType = "SourceCode.SmartObjects.Services.WorkflowImporter.WorkflowImportService";
        private const string InstanceName = "K2Skills_WorkflowImporter_V1";
        private const string ServiceObjectName = "ConvertedWorkflow";

        public static Guid Ensure(K2Settings settings, string assetsDirectory)
        {
            var schemas = Path.GetFullPath(Path.Combine(assetsDirectory, "Schemas"));
            var specs = Path.GetFullPath(Path.Combine(assetsDirectory, "Specs"));
            RequireAsset(schemas, "k2skills-workflow.xsd");
            RequireAsset(schemas, "k2skills-workflow.extract.json");
            RequireAsset(specs, "k2skills-workflow.jsonata");

            var connectionString = Discovery.BuildConnectionString(settings);
            var services = new ServiceManagementServer();
            var objects = new SmartObjectManagementServer();
            try
            {
                Open(services, connectionString, settings);
                Open(objects, connectionString, settings);
                var type = XDocument.Parse(services.GetServiceTypes()).Descendants("servicetype")
                    .SingleOrDefault(x => string.Equals((string)x.Attribute("name"), ServiceType, StringComparison.Ordinal));
                if (type == null) throw new CliException("This K2 server does not have the Workflow Import Service type.");
                var typeId = Guid.Parse((string)type.Attribute("guid"));
                var existing = XDocument.Parse(services.GetServiceInstancesCompact(typeId)).Descendants("serviceinstance")
                    .SingleOrDefault(x => string.Equals((string)x.Attribute("name"), InstanceName, StringComparison.OrdinalIgnoreCase));

                Guid instanceId;
                if (existing == null)
                {
                    if (!services.IsServiceInstanceNameUnique(InstanceName))
                        throw new CliException("The dedicated Workflow Importer name is already in use by another service type.");
                    var config = ServiceConfigInfo.Create(services.GetServiceInstanceConfig(typeId));
                    FindSetting(config, "Schemas Path").Value = schemas;
                    FindSetting(config, "Specs Path").Value = specs;
                    instanceId = Guid.NewGuid();
                    if (!services.RegisterServiceInstance(typeId, instanceId, InstanceName, "K2 Skills Workflow Importer v1",
                        "Imports K2 Skills workflow drafts using the v1 JSONata spec.", config.WriteXml()))
                        throw new CliException("K2 did not register the dedicated Workflow Importer.");
                    Console.WriteLine("Registered Workflow Import Service instance: " + instanceId);
                }
                else
                {
                    instanceId = Guid.Parse((string)existing.Attribute("guid"));
                }

                var registered = XDocument.Parse(services.GetExistingServiceInstanceConfiguration(instanceId));
                RequirePath(registered, "Schemas Path", schemas);
                RequirePath(registered, "Specs Path", specs);
                var objectName = InstanceName + "_" + ServiceObjectName;
                var importer = FindImporter(objects, instanceId, objectName);
                if (importer == null)
                {
                    objects.GenerateSmartObjects(instanceId, true, false, false, new[] { ServiceObjectName });
                    importer = FindImporter(objects, instanceId, objectName);
                }
                if (importer == null)
                    throw new CliException("K2 did not generate the dedicated ConvertedWorkflow SmartObject.");
                return importer.Guid;
            }
            finally
            {
                Close(objects);
                Close(services);
            }
        }

        private static SmartObjectInfo FindImporter(SmartObjectManagementServer server, Guid instanceId, string objectName)
        {
            return server.GetGeneratedSmartObjects(instanceId).SmartObjectList.Cast<SmartObjectInfo>()
                .SingleOrDefault(x => string.Equals(x.Name, objectName, StringComparison.OrdinalIgnoreCase));
        }

        private static void RequireAsset(string directory, string name)
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path)) throw new CliException("Workflow Importer asset is missing: " + path);
        }

        private static SettingKeyInfo FindSetting(ServiceConfigInfo config, string name)
        {
            var key = config.ConfigSettings.Cast<SettingKeyInfo>().SingleOrDefault(x => x.Name == name);
            if (key == null) throw new CliException("The Workflow Import Service is missing configuration key: " + name);
            return key;
        }

        private static XElement FindKey(XDocument config, string name)
        {
            var key = config.Descendants("key").SingleOrDefault(x => (string)x.Attribute("name") == name);
            if (key == null) throw new CliException("The Workflow Import Service is missing configuration key: " + name);
            return key;
        }

        private static void RequirePath(XDocument config, string name, string expected)
        {
            var actual = FindKey(config, name).Value;
            if (!Path.IsPathRooted(actual) ||
                !string.Equals(Path.GetFullPath(actual).TrimEnd('\\', '/'), expected.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                throw new CliException("The existing dedicated Workflow Importer uses a different " + name + ": " + actual);
        }

        private static void Open(BaseAPI server, string connectionString, K2Settings settings)
        {
            server.CreateConnection();
            server.Connection.Open(connectionString);
            if (!server.Connection.IsConnected || !server.Connection.IsAuthenticated ||
                server.Connection.Integrated != settings.IntegratedAuthentication ||
                !string.Equals(server.Connection.SecurityLabelName, settings.SecurityLabel, StringComparison.OrdinalIgnoreCase))
                throw new CliException("K2 did not authenticate with the selected environment profile.");
        }

        private static void Close(BaseAPI server)
        {
            if (server.Connection == null) return;
            server.Connection.Close();
            server.DeleteConnection();
        }
    }
}
