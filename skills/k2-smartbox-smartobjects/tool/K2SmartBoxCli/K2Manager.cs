using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using SourceCode.Categories.Client;
using SourceCode.Hosting.Client.BaseAPI;
using SourceCode.SmartObjects.Client;
using SourceCode.SmartObjects.Management;
using SourceCode.SmartObjects.Services.Management;

namespace K2SmartBoxCli
{
    internal sealed class K2Manager
    {
        private static readonly Guid SmartBoxServiceTypeGuid = new Guid("bb835c3f-aecb-4182-9ab3-26724c3a8860");
        private static readonly string[] StandardMethods = { "Create", "Save", "Delete", "Load", "GetList" };
        private readonly DeploymentManifest _manifest;
        public K2Manager(DeploymentManifest manifest) { _manifest = manifest; }

        public void CheckConnection()
        {
            WithSmartObjectServer(server => { server.GetSmartObjects(); return 0; });
            var service = GetSmartBoxService();
            Console.WriteLine("K2 connection: OK (" + _manifest.K2.Host + ":" + _manifest.K2.Port + ")");
            Console.WriteLine("SmartBox Service Instance: " + service.Name + " (" + service.Guid + ")");
        }

        public IList<LiveObject> GetLiveObjects()
        {
            return WithSmartObjectServer(server =>
            {
                var result = new List<LiveObject>();
                foreach (var wanted in _manifest.SmartObjects)
                {
                    var xml = TryGetDefinition(server, wanted.SystemName);
                    if (xml == null) continue;
                    result.Add(LiveObject.FromXml(xml));
                }
                PopulateCategories(result);
                return (IList<LiveObject>)result;
            });
        }

        public void PrintPlan()
        {
            CheckConnection();
            var service = GetSmartBoxService();
            var live = GetLiveObjects().ToDictionary(x => x.SystemName, StringComparer.OrdinalIgnoreCase);
            Console.WriteLine("Plan: " + _manifest.Name);
            foreach (var item in _manifest.SmartObjects)
            {
                LiveObject existing;
                if (!live.TryGetValue(item.SystemName, out existing))
                    Console.WriteLine("  SmartObject: create " + item.SystemName + " (" + item.Properties.Count + " properties)");
                else
                {
                    if (existing.ServiceInstanceGuid != service.Guid)
                        throw new CliException("SmartObject name collides with a non-SmartBox object: " + item.SystemName);
                    ValidateExisting(item, existing, _manifest.Deployment.UpdateExisting);
                    var additions = item.Properties.Where(x => !existing.Properties.ContainsKey(x.Name)).Select(x => x.Name).ToArray();
                    Console.WriteLine("  SmartObject: " + (additions.Length == 0 ? "unchanged " : "add properties ") +
                        item.SystemName + (additions.Length == 0 ? "" : " [" + string.Join(",", additions) + "]"));
                }
            }
            Console.WriteLine("  Category: " + _manifest.DataCategoryPath);
            Console.WriteLine("  Verify: SmartBox binding, exact properties, standard CRUD methods" +
                (_manifest.Verification.SmokeTestLists ? ", GetList smoke tests" : ""));
        }

        public void Deploy()
        {
            var service = GetSmartBoxService();
            WithSmartObjectServer(server =>
            {
                foreach (var item in _manifest.SmartObjects)
                {
                    var currentXml = TryGetDefinition(server, item.SystemName);
                    LiveObject existing = null;
                    Guid smartObjectGuid;
                    string serviceObjectName;
                    Guid serviceObjectGuid;
                    if (currentXml == null)
                    {
                        smartObjectGuid = Guid.NewGuid();
                        serviceObjectGuid = Guid.NewGuid();
                        serviceObjectName = item.SystemName + "_" + serviceObjectGuid.ToString("N").Substring(0, 8);
                    }
                    else
                    {
                        existing = LiveObject.FromXml(currentXml);
                        if (existing.ServiceInstanceGuid != service.Guid)
                            throw new CliException("SmartObject name collides with a non-SmartBox object: " + item.SystemName);
                        ValidateExisting(item, existing, _manifest.Deployment.UpdateExisting);
                        if (item.Properties.Count == existing.Properties.Count)
                        {
                            Console.WriteLine("SmartObject: unchanged (" + item.SystemName + ")");
                            continue;
                        }
                        smartObjectGuid = existing.Guid;
                        serviceObjectGuid = existing.ServiceObjectGuid;
                        serviceObjectName = existing.ServiceObjectName;
                    }
                    var xml = SmartBoxDefinitionBuilder.Build(item, smartObjectGuid, service.Guid, service.Name,
                        serviceObjectName, serviceObjectGuid);
                    var publishResult = server.PublishSmartObject(xml);
                    if (!string.IsNullOrWhiteSpace(publishResult) &&
                        publishResult.IndexOf("<smartobjectroot", StringComparison.OrdinalIgnoreCase) < 0)
                        Console.WriteLine("SmartObject publish result: " + publishResult);
                    Console.WriteLine("SmartObject: " + (existing == null ? "created" : "updated") + " (" + item.SystemName + ")");
                }
                return 0;
            });
            PlaceObjects();
            Verify();
        }

        public void Verify()
        {
            var service = GetSmartBoxService();
            var live = GetLiveObjects().ToDictionary(x => x.SystemName, StringComparer.OrdinalIgnoreCase);
            foreach (var wanted in _manifest.SmartObjects)
            {
                LiveObject actual;
                if (!live.TryGetValue(wanted.SystemName, out actual))
                    throw new CliException("SmartObject is missing: " + wanted.SystemName);
                if (actual.ServiceInstanceGuid != service.Guid)
                    throw new CliException("SmartObject is not bound to the installed SmartBox Service: " + wanted.SystemName);
                ValidateExisting(wanted, actual, true);
                if (wanted.Properties.Count != actual.Properties.Count)
                    throw new CliException("SmartObject has undeclared properties: " + wanted.SystemName);
                foreach (var method in StandardMethods)
                    if (!actual.Methods.Contains(method))
                        throw new CliException("SmartObject method is missing: " + wanted.SystemName + "." + method);
                if (!actual.CategoryPaths.Any(x => PathsEqual(x, _manifest.DataCategoryPath)))
                    throw new CliException("SmartObject is not in " + _manifest.DataCategoryPath + ": " + wanted.SystemName);
                Console.WriteLine("SmartObject verification: OK (" + wanted.SystemName + ")");
            }
            if (_manifest.Verification.SmokeTestLists) SmokeTestLists();
        }

        public void Inspect()
        {
            var live = GetLiveObjects().ToDictionary(x => x.SystemName, StringComparer.OrdinalIgnoreCase);
            foreach (var wanted in _manifest.SmartObjects)
            {
                LiveObject item;
                if (!live.TryGetValue(wanted.SystemName, out item))
                {
                    Console.WriteLine(wanted.SystemName + ": absent");
                    continue;
                }
                Console.WriteLine(item.SystemName + " (" + item.Guid + ") service=" + item.ServiceInstanceGuid);
                Console.WriteLine("  properties=" + string.Join(",", item.Properties.Select(x => x.Key + ":" + x.Value.Type)));
                Console.WriteLine("  methods=" + string.Join(",", item.Methods.OrderBy(x => x)));
                Console.WriteLine("  categories=" + (item.CategoryPaths.Count == 0 ? "<none>" : string.Join(";", item.CategoryPaths)));
            }
        }

        public void Cleanup(bool deleteRootCategory)
        {
            var service = GetSmartBoxService();
            WithSmartObjectServer(server =>
            {
                foreach (var wanted in _manifest.SmartObjects)
                {
                    var xml = TryGetDefinition(server, wanted.SystemName);
                    if (xml == null)
                    {
                        Console.WriteLine("SmartObject: already absent (" + wanted.SystemName + ")");
                        continue;
                    }
                    var live = LiveObject.FromXml(xml);
                    if (live.ServiceInstanceGuid != service.Guid)
                        throw new CliException("Refusing to delete non-SmartBox collision: " + wanted.SystemName);
                    var document = XDocument.Parse(xml);
                    var systemTypes = document.Root.Element("types").Elements("type")
                        .Where(x => string.Equals((string)x.Attribute("name"), "system", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (systemTypes.Count > 0)
                    {
                        foreach (var systemType in systemTypes) systemType.Remove();
                        server.PublishSmartObject(document.ToString(SaveOptions.DisableFormatting));
                    }
                    server.DeleteSmartObject(live.Guid, true);
                    Console.WriteLine("SmartObject: deleted (" + wanted.SystemName + ")");
                }
                return 0;
            });
            WithCategoryServer(server =>
            {
                DeleteCategoryIfEmpty(server, _manifest.DataCategoryPath);
                if (deleteRootCategory) DeleteCategoryIfEmpty(server, _manifest.Application.RootCategoryPath);
                return 0;
            });
        }

        internal static void ValidateExisting(SmartObjectDefinition wanted, LiveObject actual, bool allowAdditions)
        {
            foreach (var liveProperty in actual.Properties)
                if (!wanted.Properties.Any(x => string.Equals(x.Name, liveProperty.Key, StringComparison.OrdinalIgnoreCase)))
                    throw new CliException("Removing existing SmartBox properties is not supported: " + wanted.SystemName + "." + liveProperty.Key);
            foreach (var property in wanted.Properties)
            {
                LiveProperty live;
                if (!actual.Properties.TryGetValue(property.Name, out live))
                {
                    if (!allowAdditions)
                        throw new CliException("SmartObject exists and differs; set deployment.updateExisting=true for additive updates: " + wanted.SystemName);
                    continue;
                }
                if (!string.Equals(property.Type, live.Type, StringComparison.OrdinalIgnoreCase) || property.Key != live.Key)
                    throw new CliException("Changing a SmartBox property type or key is not supported: " + wanted.SystemName + "." + property.Name);
            }
        }

        private void PlaceObjects()
        {
            var live = GetLiveObjects();
            WithCategoryServer(server =>
            {
                var manager = server.GetCategoryManager(1, true, true);
                server.FindCategoryIdByPathName(manager, _manifest.DataCategoryPath, "\\", true);
                manager = server.GetCategoryManager(1, true, true);
                var id = server.FindCategoryIdByPathName(manager, _manifest.DataCategoryPath, "\\", false);
                var target = manager.Categories.Cast<Category>().FirstOrDefault(x => x != null && x.Id == id);
                if (target == null) throw new CliException("K2 did not return created category: " + _manifest.DataCategoryPath);
                foreach (var item in live)
                {
                    var links = FindLinks(manager, item.Guid).ToList();
                    if (links.Any(x => PathsEqual(GetPath(x.Category), _manifest.DataCategoryPath))) continue;
                    var source = links.FirstOrDefault();
                    if (source == null)
                        server.AddCategoryData(target, item.Guid.ToString(), CategoryServer.dataType.SmartObject, item.DisplayName);
                    else
                        server.AddCategoryData(target, item.Guid.ToString(), CategoryServer.dataType.SmartObject,
                            item.DisplayName, GetPath(source.Category), true);
                    Console.WriteLine("SmartObject category: placed (" + item.SystemName + " -> " + _manifest.DataCategoryPath + ")");
                }
                return 0;
            });
        }

        private void PopulateCategories(IList<LiveObject> objects)
        {
            if (objects.Count == 0) return;
            WithCategoryServer(server =>
            {
                var manager = server.GetCategoryManager(1, true, true);
                foreach (var item in objects)
                    item.CategoryPaths = FindLinks(manager, item.Guid).Select(x => GetPath(x.Category))
                        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
                return 0;
            });
        }

        private void SmokeTestLists()
        {
            var server = new SmartObjectClientServer();
            try
            {
                server.CreateConnection();
                server.Connection.Open(BuildConnectionString());
                foreach (var item in _manifest.SmartObjects)
                {
                    var smartObject = server.GetSmartObject(item.SystemName);
                    smartObject.MethodToExecute = "GetList";
                    var timer = Stopwatch.StartNew();
                    DataTable data = server.ExecuteListDataTable(smartObject, 1, 1);
                    timer.Stop();
                    Console.WriteLine("SmartObject smoke test: OK (" + item.SystemName + ".GetList, " +
                        data.Rows.Count + " row(s), " + timer.ElapsedMilliseconds + " ms)");
                }
            }
            finally { Close(server); }
        }

        private ServiceState GetSmartBoxService()
        {
            return WithServiceServer(server =>
            {
                var doc = XDocument.Parse(server.GetServiceInstancesCompact(SmartBoxServiceTypeGuid));
                var items = doc.Descendants("serviceinstance").Select(x => new ServiceState
                {
                    Guid = Guid.Parse((string)x.Attribute("guid")),
                    Name = (string)x.Attribute("name")
                }).ToList();
                if (items.Count == 0) throw new CliException("No SmartBox Service Instance is installed.");
                if (items.Count > 1) throw new CliException("Multiple SmartBox Service Instances were found; explicit selection is not yet supported.");
                return items[0];
            });
        }

        private static IEnumerable<CategoryLink> FindLinks(CategoryManager manager, Guid guid)
        {
            foreach (Category category in manager.Categories)
            {
                if (category == null || category.DataList == null) continue;
                foreach (CategoryData data in category.DataList)
                    if (data.DataType == CategoryServer.dataType.SmartObject && data.Guid == guid)
                        yield return new CategoryLink { Category = category };
            }
        }

        private static string TryGetDefinition(SmartObjectManagementServer server, string name)
        {
            try { return server.GetSmartObjectDefinition(name); }
            catch { return null; }
        }

        private static void DeleteCategoryIfEmpty(CategoryServer server, string path)
        {
            var manager = server.GetCategoryManager(1, true, true);
            var category = manager.Categories.Cast<Category>().FirstOrDefault(x => x != null && PathsEqual(GetPath(x), path));
            if (category == null) { Console.WriteLine("K2 category: already absent (" + path + ")"); return; }
            if (category.IsRoot) throw new CliException("Refusing to delete a K2 category-system root: " + path);
            if (!category.HasLoadedData) server.LoadCategoryData(category);
            var children = category.ChildCategoryIds == null ? 0 : category.ChildCategoryIds.Count;
            var data = category.DataList == null ? 0 : category.DataList.Count;
            if (children != 0 || data != 0)
            {
                Console.WriteLine("K2 category: retained (not empty: " + path + ")");
                return;
            }
            server.DeleteCategory(category);
            Console.WriteLine("K2 category: deleted (" + path + ")");
        }

        private string BuildConnectionString()
        {
            var k2 = _manifest.K2;
            var builder = new SCConnectionStringBuilder
            {
                Authenticate = true, Host = k2.Host, Port = (uint)k2.Port,
                Integrated = k2.Integrated, IsPrimaryLogin = true, SecurityLabelName = k2.SecurityLabel
            };
            if (!k2.Integrated)
            {
                builder.WindowsDomain = k2.Domain;
                builder.UserID = k2.UserName;
                builder.Password = ReadEnvironment(k2.PasswordEnvironmentVariable);
                builder.CachePassword = false;
            }
            return builder.ConnectionString;
        }

        private T WithSmartObjectServer<T>(Func<SmartObjectManagementServer, T> action)
        {
            var server = new SmartObjectManagementServer();
            try { server.CreateConnection(); server.Connection.Open(BuildConnectionString()); return action(server); }
            finally { Close(server); }
        }
        private T WithServiceServer<T>(Func<ServiceManagementServer, T> action)
        {
            var server = new ServiceManagementServer();
            try { server.CreateConnection(); server.Connection.Open(BuildConnectionString()); return action(server); }
            finally { Close(server); }
        }
        private T WithCategoryServer<T>(Func<CategoryServer, T> action)
        {
            var server = new CategoryServer();
            try { server.CreateConnection(); server.Connection.Open(BuildConnectionString()); return action(server); }
            finally { Close(server); }
        }
        private static void Close(SourceCode.Hosting.Client.BaseAPI.BaseAPI server)
        {
            if (server.Connection == null) return;
            server.Connection.Close();
            server.DeleteConnection();
        }
        private static string ReadEnvironment(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value)) throw new CliException("Required environment variable is empty: " + name);
            return value;
        }
        private static string GetPath(Category category)
        {
            if (category == null) return null;
            if (string.IsNullOrWhiteSpace(category.Path)) return category.Name;
            return category.Path.TrimEnd('\\', '/') + "\\" + category.Name;
        }
        private static bool PathsEqual(string left, string right)
        {
            return string.Equals((left ?? "").Trim('\\', '/'), (right ?? "").Trim('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class ServiceState { public Guid Guid; public string Name; }
    internal sealed class CategoryLink { public Category Category; }
    internal sealed class LiveProperty { public string Type; public bool Key; public bool Required; }
    internal sealed class LiveObject
    {
        public Guid Guid;
        public string SystemName, DisplayName, ServiceObjectName;
        public Guid ServiceInstanceGuid, ServiceObjectGuid;
        public Dictionary<string, LiveProperty> Properties = new Dictionary<string, LiveProperty>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public IList<string> CategoryPaths = new List<string>();

        public static LiveObject FromXml(string xml)
        {
            var document = XDocument.Parse(xml);
            var root = document.Root;
            var rootService = root.Element("metadata").Element("service");
            var item = new LiveObject
            {
                Guid = Guid.Parse((string)root.Attribute("guid")),
                SystemName = (string)root.Attribute("name"),
                DisplayName = root.Element("metadata").Element("display").Element("displayname").Value,
                ServiceInstanceGuid = Guid.Parse(rootService.Elements("key")
                    .First(x => (string)x.Attribute("name") == "serviceinstance").Value),
                ServiceObjectName = rootService.Elements("key")
                    .First(x => (string)x.Attribute("name") == "serviceobject").Value
            };
            var objectData = root.Element("extendingobject").Element("objectdata");
            item.ServiceObjectGuid = Guid.Parse(objectData.Descendants("key")
                .First(x => (string)x.Attribute("name") == "guid").Descendants("guid").First().Value);
            foreach (var property in root.Element("properties").Elements("property"))
                item.Properties[(string)property.Attribute("name")] = new LiveProperty
                {
                    Type = (string)property.Attribute("type"),
                    Key = string.Equals((string)property.Attribute("unique"), "true", StringComparison.OrdinalIgnoreCase),
                    Required = string.Equals((string)property.Attribute("required"), "true", StringComparison.OrdinalIgnoreCase)
                };
            foreach (var method in root.Element("methods").Elements("method"))
                item.Methods.Add((string)method.Attribute("name"));
            return item;
        }
    }
}
