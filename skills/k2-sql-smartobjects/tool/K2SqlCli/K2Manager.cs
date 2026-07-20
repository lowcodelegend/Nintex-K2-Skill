using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using SourceCode.Categories.Client;
using SourceCode.Hosting.Client.BaseAPI;
using SourceCode.SmartObjects.Client;
using SourceCode.SmartObjects.Management;
using SourceCode.SmartObjects.Services.Management;

namespace K2SqlCli
{
    internal sealed class K2Manager
    {
        private static readonly Guid SqlServerServiceTypeGuid = new Guid("f393f637-d443-4dab-8497-4e77830c527d");
        private readonly DeploymentManifest _manifest;

        public K2Manager(DeploymentManifest manifest)
        {
            _manifest = manifest;
        }

        public void CheckConnection()
        {
            WithServiceServer(delegate(ServiceManagementServer server)
            {
                var xml = XDocument.Parse(server.GetServiceType(SqlServerServiceTypeGuid));
                var displayName = xml.Descendants("displayname").Select(x => x.Value).FirstOrDefault() ?? "SQL Server Service";
                Console.WriteLine("K2 connection: OK (" + displayName + ")");
                return 0;
            });
        }

        public ServiceInstanceState GetServiceInstanceState()
        {
            return WithServiceServer(delegate(ServiceManagementServer server)
            {
                return FindServiceInstance(server);
            });
        }

        public Guid EnsureServiceInstance()
        {
            return WithServiceServer(delegate(ServiceManagementServer server)
            {
                var state = FindServiceInstance(server);
                var config = BuildSqlServiceConfig(server);
                if (state == null)
                {
                    var guid = Guid.NewGuid();
                    var created = server.RegisterServiceInstance(
                        SqlServerServiceTypeGuid,
                        guid,
                        _manifest.K2.ServiceInstance.SystemName,
                        _manifest.K2.ServiceInstance.DisplayName,
                        _manifest.K2.ServiceInstance.Description,
                        config);
                    if (!created) throw new CliException("K2 did not register the SQL Service Instance.");
                    Console.WriteLine("K2 service instance: created (" + guid + ")");
                    return guid;
                }

                var updated = server.UpdateServiceInstance(
                    SqlServerServiceTypeGuid,
                    state.Guid,
                    _manifest.K2.ServiceInstance.SystemName,
                    _manifest.K2.ServiceInstance.DisplayName,
                    _manifest.K2.ServiceInstance.Description,
                    config);
                if (!updated) throw new CliException("K2 did not update the SQL Service Instance.");
                if (!server.RefreshServiceInstance(state.Guid)) throw new CliException("K2 did not refresh the SQL Service Instance.");
                Console.WriteLine("K2 service instance: updated and refreshed (" + state.Guid + ")");
                return state.Guid;
            });
        }

        public void GenerateSmartObjects(Guid serviceInstanceGuid)
        {
            WithSmartObjectManagementServer(delegate(SmartObjectManagementServer server)
            {
                var options = _manifest.K2.SmartObjects;
                server.GenerateSmartObjects(serviceInstanceGuid, options.CreateNew, options.UpdateExisting, options.DeleteRemoved);
                Console.WriteLine("K2 SmartObjects: generation complete");
                return 0;
            });

            PlaceGeneratedSmartObjects(serviceInstanceGuid);
        }

        public IList<SmartObjectState> GetGeneratedSmartObjects(Guid serviceInstanceGuid)
        {
            return WithSmartObjectManagementServer(delegate(SmartObjectManagementServer server)
            {
                var explorer = server.GetGeneratedSmartObjects(serviceInstanceGuid);
                var result = new List<SmartObjectState>();
                foreach (SmartObjectInfo item in explorer.SmartObjects)
                {
                    result.Add(new SmartObjectState
                    {
                        Guid = item.Guid,
                        SystemName = item.Name,
                        DisplayName = item.Metadata == null ? item.Name : item.Metadata.DisplayName,
                        ServiceObjectName = item.ServiceObjectName,
                        MethodNames = item.Methods.Cast<SmartMethodInfo>().Select(m => m.Name).ToList()
                    });
                }
                PopulateCategoryPaths(result);
                return result;
            });
        }

        private void PlaceGeneratedSmartObjects(Guid serviceInstanceGuid)
        {
            var targetPath = _manifest.Application.DataCategoryPath;
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                Console.WriteLine("K2 SmartObject category: unchanged (application.rootCategoryPath not configured)");
                return;
            }

            var smartObjects = GetGeneratedSmartObjects(serviceInstanceGuid);
            WithCategoryServer(delegate(CategoryServer server)
            {
                var manager = server.GetCategoryManager(1, true, true);
                server.FindCategoryIdByPathName(manager, targetPath, "\\", true);
                manager = server.GetCategoryManager(1, true, true);
                var categoryId = server.FindCategoryIdByPathName(manager, targetPath, "\\", false);
                var target = manager.Categories.Cast<Category>().FirstOrDefault(x => x != null && x.Id == categoryId);
                if (target == null)
                {
                    throw new CliException("K2 created the SmartObject category but did not return it: " + targetPath);
                }

                foreach (var smartObject in smartObjects)
                {
                    var links = FindSmartObjectCategoryLinks(manager, smartObject.Guid).ToList();
                    if (links.Any(x => PathsEqual(GetCategoryFullPath(x.Category), targetPath)))
                    {
                        Console.WriteLine("SmartObject category: already placed (" + smartObject.SystemName + " -> " + targetPath + ")");
                        continue;
                    }

                    var source = links.FirstOrDefault();
                    if (source == null)
                    {
                        server.AddCategoryData(target, smartObject.Guid.ToString(), CategoryServer.dataType.SmartObject, smartObject.DisplayName);
                    }
                    else
                    {
                        server.AddCategoryData(target, smartObject.Guid.ToString(), CategoryServer.dataType.SmartObject, smartObject.DisplayName, GetCategoryFullPath(source.Category), true);
                    }
                    Console.WriteLine("SmartObject category: placed (" + smartObject.SystemName + " -> " + targetPath + ")");
                }
                return 0;
            });
        }

        private void PopulateCategoryPaths(IList<SmartObjectState> states)
        {
            if (states.Count == 0) return;
            WithCategoryServer(delegate(CategoryServer server)
            {
                var manager = server.GetCategoryManager(1, true, true);
                foreach (var state in states)
                {
                    state.CategoryPaths = FindSmartObjectCategoryLinks(manager, state.Guid)
                        .Select(x => GetCategoryFullPath(x.Category))
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                return 0;
            });
        }

        private static IEnumerable<SmartObjectCategoryLink> FindSmartObjectCategoryLinks(CategoryManager manager, Guid smartObjectGuid)
        {
            foreach (Category category in manager.Categories)
            {
                if (category == null || category.DataList == null) continue;
                foreach (CategoryData data in category.DataList)
                {
                    if (data.DataType == CategoryServer.dataType.SmartObject && data.Guid == smartObjectGuid)
                    {
                        yield return new SmartObjectCategoryLink { Category = category, Data = data };
                    }
                }
            }
        }

        public void Verify()
        {
            var instance = GetServiceInstanceState();
            if (instance == null) throw new CliException("K2 Service Instance is missing: " + _manifest.K2.ServiceInstance.SystemName);
            var smartObjects = GetGeneratedSmartObjects(instance.Guid);
            if (smartObjects.Count < _manifest.Verification.MinimumGeneratedSmartObjects)
            {
                throw new CliException(string.Format(CultureInfo.InvariantCulture, "Expected at least {0} generated SmartObjects but found {1}.", _manifest.Verification.MinimumGeneratedSmartObjects, smartObjects.Count));
            }
            foreach (var serviceObjectName in _manifest.Verification.SmartObjectServiceObjects)
            {
                if (!smartObjects.Any(x => string.Equals(x.ServiceObjectName, serviceObjectName, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new CliException("No generated SmartObject maps to service object: " + serviceObjectName);
                }
            }
            foreach (var smartObject in smartObjects.OrderBy(x => x.SystemName))
            {
                Console.WriteLine("SmartObject: " + smartObject.SystemName + " <= " + smartObject.ServiceObjectName + " [" + string.Join(",", smartObject.MethodNames.ToArray()) + "] category=" + FormatCategoryPaths(smartObject.CategoryPaths));
            }
            var expectedCategory = _manifest.Application.DataCategoryPath;
            if (!string.IsNullOrWhiteSpace(expectedCategory))
            {
                foreach (var smartObject in smartObjects)
                {
                    if (!smartObject.CategoryPaths.Any(x => PathsEqual(x, expectedCategory)))
                    {
                        throw new CliException("Generated SmartObject is not in the solution Data category: " + smartObject.SystemName + " (expected " + expectedCategory + ")");
                    }
                }
                Console.WriteLine("SmartObject category verification: OK (" + expectedCategory + ")");
            }
            if (_manifest.Verification.SmokeTestListMethods)
            {
                SmokeTestListMethods(smartObjects);
            }
            Console.WriteLine("K2 verification: OK (" + smartObjects.Count + " generated SmartObjects)");
        }

        public void DeleteGeneratedSmartObjectsAndServiceInstance()
        {
            var instance = GetServiceInstanceState();
            if (instance == null)
            {
                Console.WriteLine("K2 service instance: already absent");
                return;
            }
            var smartObjects = GetGeneratedSmartObjects(instance.Guid);
            WithSmartObjectManagementServer(delegate(SmartObjectManagementServer server)
            {
                foreach (var smartObject in smartObjects)
                {
                    server.DeleteSmartObject(smartObject.Guid, true);
                    Console.WriteLine("SmartObject: deleted (" + smartObject.SystemName + ")");
                }
                return 0;
            });
            WithServiceServer(delegate(ServiceManagementServer server)
            {
                if (!server.DeleteServiceInstance(instance.Guid, false))
                {
                    throw new CliException("K2 did not delete the Service Instance.");
                }
                return 0;
            });
            Console.WriteLine("K2 service instance: deleted (" + instance.Guid + ")");
        }

        private void SmokeTestListMethods(IList<SmartObjectState> states)
        {
            var server = new SmartObjectClientServer();
            try
            {
                server.CreateConnection();
                server.Connection.Open(BuildConnectionString());
                foreach (var state in states)
                {
                    var smartObject = server.GetSmartObject(state.SystemName);
                    var listMethod = smartObject.ListMethods.Cast<SmartListMethod>()
                        .FirstOrDefault(m => m.RequiredProperties.Count == 0 && m.Parameters.Count == 0);
                    if (listMethod == null)
                    {
                        Console.WriteLine("SmartObject smoke test: skipped (no parameterless List method: " + state.SystemName + ")");
                        continue;
                    }
                    smartObject.MethodToExecute = listMethod.Name;
                    var stopwatch = Stopwatch.StartNew();
                    var table = server.ExecuteListDataTable(smartObject, 1, 1);
                    stopwatch.Stop();
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "SmartObject smoke test: OK ({0}.{1}, {2} row(s), {3} ms)", state.SystemName, listMethod.Name, table.Rows.Count, stopwatch.ElapsedMilliseconds));
                }
            }
            finally
            {
                Close(server);
            }
        }

        private string BuildSqlServiceConfig(ServiceManagementServer server)
        {
            var document = XDocument.Parse(server.GetServiceInstanceConfig(SqlServerServiceTypeGuid));
            SetKey(document, "Database", _manifest.K2.ServiceInstance.Database);
            SetKey(document, "Server", _manifest.K2.ServiceInstance.SqlServer);
            SetKey(document, "On Different SQL Server", Bool(_manifest.K2.ServiceInstance.OnDifferentSqlServer));
            SetKey(document, "Command Timeout", _manifest.K2.ServiceInstance.CommandTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
            SetKey(document, "Use Native SQL Execution", Bool(_manifest.K2.ServiceInstance.UseNativeSqlExecution));
            SetKey(document, "Use SQL paging", Bool(_manifest.K2.ServiceInstance.UseSqlPaging));
            SetKey(document, "Encrypt connection", Bool(_manifest.K2.ServiceInstance.EncryptConnection));

            var authentication = document.Descendants("serviceauthentication").First();
            var mode = _manifest.K2.ServiceInstance.AuthenticationMode.ToLowerInvariant();
            authentication.SetAttributeValue("impersonate", Bool(mode == "impersonate"));
            authentication.SetAttributeValue("enforceimpersonation", Bool(mode == "impersonate"));
            var username = authentication.Element("username");
            var password = authentication.Element("password");
            username.Value = mode == "static" ? _manifest.K2.ServiceInstance.UserName : string.Empty;
            password.Value = mode == "static" ? ReadRequiredEnvironmentVariable(_manifest.K2.ServiceInstance.PasswordEnvironmentVariable) : string.Empty;
            return document.ToString(SaveOptions.DisableFormatting);
        }

        private ServiceInstanceState FindServiceInstance(ServiceManagementServer server)
        {
            var document = XDocument.Parse(server.GetServiceInstancesCompact(SqlServerServiceTypeGuid));
            var element = document.Descendants("serviceinstance")
                .FirstOrDefault(x => string.Equals((string)x.Attribute("name"), _manifest.K2.ServiceInstance.SystemName, StringComparison.OrdinalIgnoreCase));
            if (element == null) return null;
            return new ServiceInstanceState
            {
                Guid = Guid.Parse((string)element.Attribute("guid")),
                SystemName = (string)element.Attribute("name"),
                DisplayName = element.Descendants("displayname").Select(x => x.Value).FirstOrDefault()
            };
        }

        private string BuildConnectionString()
        {
            var options = _manifest.K2;
            var builder = new SCConnectionStringBuilder
            {
                Authenticate = true,
                Host = options.Host,
                Port = (uint)options.Port,
                Integrated = options.Integrated,
                IsPrimaryLogin = true,
                SecurityLabelName = options.SecurityLabel
            };
            if (!options.Integrated)
            {
                builder.WindowsDomain = options.Domain;
                builder.UserID = options.UserName;
                builder.Password = ReadRequiredEnvironmentVariable(options.PasswordEnvironmentVariable);
                builder.CachePassword = false;
            }
            return builder.ConnectionString;
        }

        private T WithServiceServer<T>(Func<ServiceManagementServer, T> action)
        {
            var server = new ServiceManagementServer();
            try
            {
                server.CreateConnection();
                server.Connection.Open(BuildConnectionString());
                return action(server);
            }
            finally
            {
                Close(server);
            }
        }

        private T WithSmartObjectManagementServer<T>(Func<SmartObjectManagementServer, T> action)
        {
            var server = new SmartObjectManagementServer();
            try
            {
                server.CreateConnection();
                server.Connection.Open(BuildConnectionString());
                return action(server);
            }
            finally
            {
                Close(server);
            }
        }

        private T WithCategoryServer<T>(Func<CategoryServer, T> action)
        {
            var server = new CategoryServer();
            try
            {
                server.CreateConnection();
                server.Connection.Open(BuildConnectionString());
                return action(server);
            }
            finally
            {
                Close(server);
            }
        }

        private static void Close(SourceCode.Hosting.Client.BaseAPI.BaseAPI server)
        {
            if (server.Connection != null)
            {
                server.Connection.Close();
                server.DeleteConnection();
            }
        }

        private static void SetKey(XDocument document, string name, string value)
        {
            var key = document.Descendants("key").FirstOrDefault(x => string.Equals((string)x.Attribute("name"), name, StringComparison.OrdinalIgnoreCase));
            if (key == null) throw new CliException("The installed SQL Server Service type does not expose setting: " + name);
            key.RemoveNodes();
            key.Value = value ?? string.Empty;
        }

        private static string Bool(bool value) { return value ? "true" : "false"; }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals((left ?? string.Empty).Trim('\\', '/'), (right ?? string.Empty).Trim('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatCategoryPaths(IList<string> paths)
        {
            return paths == null || paths.Count == 0 ? "<none>" : string.Join(";", paths.ToArray());
        }

        private static string GetCategoryFullPath(Category category)
        {
            if (category == null) return null;
            if (string.IsNullOrWhiteSpace(category.Path)) return category.Name;
            if (string.IsNullOrWhiteSpace(category.Name)) return category.Path;
            return category.Path.TrimEnd('\\', '/') + "\\" + category.Name;
        }

        private static string ReadRequiredEnvironmentVariable(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value)) throw new CliException("Required environment variable is not set: " + name);
            return value;
        }
    }

    internal sealed class ServiceInstanceState
    {
        public Guid Guid { get; set; }
        public string SystemName { get; set; }
        public string DisplayName { get; set; }
    }

    internal sealed class SmartObjectState
    {
        public Guid Guid { get; set; }
        public string SystemName { get; set; }
        public string DisplayName { get; set; }
        public string ServiceObjectName { get; set; }
        public List<string> MethodNames { get; set; }
        public List<string> CategoryPaths { get; set; }
    }

    internal sealed class SmartObjectCategoryLink
    {
        public Category Category { get; set; }
        public CategoryData Data { get; set; }
    }
}
