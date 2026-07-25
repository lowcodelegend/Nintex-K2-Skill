using System;
using System.Data;
using System.IO;
using System.Linq;
using SourceCode.Hosting.Client.BaseAPI;
using SourceCode.SmartObjects.Client;

namespace K2WebComponentCli
{
    internal sealed class ControlManager
    {
        private const string ManagementSmartObject = "com_K2_System_CustomControls_SmartObject_CustomControlManagement";
        private readonly Options _options;

        public ControlManager(Options options) { _options = options; }

        public void Doctor()
        {
            WithServer(server =>
            {
                var smartObject = server.GetSmartObject(ManagementSmartObject);
                var required = new[] { "List", "UploadDraft", "RegisterControl", "Delete", "Dependencies" };
                var available = smartObject.AllMethods.Select(method => method.Name).ToArray();
                foreach (var method in required)
                    if (!available.Contains(method, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("Modern custom-control management method is unavailable: " + method);
                Console.WriteLine("K2 modern Web Component management: OK (" + _options.Host + ":" + _options.Port + ")");
                return 0;
            });
        }

        public void List()
        {
            var table = ExecuteList("List");
            foreach (DataRow row in table.Rows)
                Console.WriteLine(Value(row, "Name") + "\t" + Value(row, "DisplayName") + "\t" + Value(row, "ID") + "\tusage=" + Value(row, "Usage"));
            Console.WriteLine("Registered modern controls: " + table.Rows.Count);
        }

        public void Deploy(string packagePath, string tag)
        {
            if (!File.Exists(packagePath)) throw new CliException("Package not found: " + packagePath);
            if (!string.Equals(Path.GetExtension(packagePath), ".zip", StringComparison.OrdinalIgnoreCase))
                throw new CliException("--package must be a .zip file.");
            var bytes = File.ReadAllBytes(packagePath);
            WithServer(server =>
            {
                var draft = server.GetSmartObject(ManagementSmartObject);
                draft.MethodToExecute = "UploadDraft";
                SetFile(draft, "ZipFile", Path.GetFileName(packagePath), bytes);
                draft = server.ExecuteScalar(draft);
                RequireSuccess(draft, "UploadDraft");

                var displayName = Get(draft, "DisplayName");
                var description = Get(draft, "Description");
                if (string.IsNullOrWhiteSpace(displayName)) throw new CliException("UploadDraft did not return the manifest display name.");

                var existing = Find(server, tag);
                var register = server.GetSmartObject(ManagementSmartObject);
                register.MethodToExecute = "RegisterControl";
                Set(register, "DisplayName", displayName);
                Set(register, "Description", description);
                SetFile(register, "ZipFile", Path.GetFileName(packagePath), bytes);
                CopyImage(draft, register, "IconFile");
                if (existing != null) Set(register, "ID", Value(existing, "ID"));
                register = server.ExecuteScalar(register);
                RequireSuccess(register, "RegisterControl");

                var live = Find(server, tag);
                if (live == null) throw new CliException("Control registration completed but '" + tag + "' was not returned by List.");
                Console.WriteLine((existing == null ? "Registered" : "Updated") + " modern K2 Web Component: " + tag + " [" + Value(live, "ID") + "]");
                return 0;
            });
        }

        public void Verify(string tag)
        {
            WithServer(server =>
            {
                var row = Find(server, tag);
                if (row == null) throw new CliException("Registered modern control not found: " + tag);
                Console.WriteLine("Verified modern K2 Web Component: " + Value(row, "Name") + " [" + Value(row, "ID") + "], " + Value(row, "DisplayName"));
                return 0;
            });
        }

        public void Cleanup(string tag)
        {
            WithServer(server =>
            {
                var row = Find(server, tag);
                if (row == null) { Console.WriteLine("Modern K2 Web Component is already absent: " + tag); return 0; }
                var id = Value(row, "ID");
                var dependencyObject = server.GetSmartObject(ManagementSmartObject);
                dependencyObject.MethodToExecute = "Dependencies";
                Set(dependencyObject, "ID", id);
                var dependencies = server.ExecuteListDataTable(dependencyObject);
                if (dependencies.Rows.Count > 0)
                    throw new CliException("Control '" + tag + "' has " + dependencies.Rows.Count + " dependency row(s); remove it from Forms and Views before cleanup.");
                var delete = server.GetSmartObject(ManagementSmartObject);
                delete.MethodToExecute = "Delete";
                Set(delete, "ID", id);
                delete = server.ExecuteScalar(delete);
                RequireSuccess(delete, "Delete");
                if (Find(server, tag) != null) throw new CliException("Control still appears in List after Delete: " + tag);
                Console.WriteLine("Deleted modern K2 Web Component: " + tag + " [" + id + "]");
                return 0;
            });
        }

        private DataTable ExecuteList(string method)
        {
            return WithServer(server =>
            {
                var smartObject = server.GetSmartObject(ManagementSmartObject);
                smartObject.MethodToExecute = method;
                return server.ExecuteListDataTable(smartObject);
            });
        }

        private static DataRow Find(SmartObjectClientServer server, string name)
        {
            var smartObject = server.GetSmartObject(ManagementSmartObject);
            smartObject.MethodToExecute = "List";
            var table = server.ExecuteListDataTable(smartObject);
            return table.Rows.Cast<DataRow>().FirstOrDefault(row => string.Equals(Value(row, "Name"), name, StringComparison.OrdinalIgnoreCase));
        }

        private static string Value(DataRow row, string name)
        {
            return row.Table.Columns.Contains(name) && row[name] != DBNull.Value ? Convert.ToString(row[name]) : string.Empty;
        }
        private static string Get(SmartObject smartObject, string name)
        {
            var property = smartObject.Properties.Cast<SmartProperty>().FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            return property == null ? string.Empty : property.Value;
        }
        private static void Set(SmartObject smartObject, string name, string value)
        {
            var property = smartObject.Properties.Cast<SmartProperty>().FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (property == null) throw new CliException("Custom-control management property is unavailable: " + name);
            property.Value = value ?? string.Empty;
        }
        private static void SetFile(SmartObject smartObject, string name, string fileName, byte[] bytes)
        {
            var property = smartObject.Properties.Cast<SmartProperty>().FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) as SmartFileProperty;
            if (property == null) throw new CliException("Custom-control file property is unavailable: " + name);
            property.FileName = fileName;
            property.Content = Convert.ToBase64String(bytes);
        }
        private static void CopyImage(SmartObject from, SmartObject to, string name)
        {
            var source = from.Properties.Cast<SmartProperty>().FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) as SmartImageProperty;
            var target = to.Properties.Cast<SmartProperty>().FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) as SmartImageProperty;
            if (source == null || target == null || string.IsNullOrWhiteSpace(source.Content))
                throw new CliException("UploadDraft did not return the control icon.");
            target.FileName = source.FileName;
            target.Content = source.Content;
        }
        private static void RequireSuccess(SmartObject smartObject, string method)
        {
            var success = Get(smartObject, "Success");
            if (!string.IsNullOrWhiteSpace(success) && !string.Equals(success, "true", StringComparison.OrdinalIgnoreCase))
                throw new CliException(method + " reported failure.");
        }

        private T WithServer<T>(Func<SmartObjectClientServer, T> action)
        {
            var server = new SmartObjectClientServer();
            try
            {
                server.CreateConnection();
                server.Connection.Open(ConnectionString());
                return action(server);
            }
            finally
            {
                if (server.Connection != null)
                {
                    server.Connection.Close();
                    server.DeleteConnection();
                }
            }
        }
        private string ConnectionString()
        {
            var builder = new SCConnectionStringBuilder
            {
                Authenticate = true, Host = _options.Host, Port = (uint)_options.Port,
                Integrated = _options.Integrated, IsPrimaryLogin = true, SecurityLabelName = _options.SecurityLabel
            };
            if (!_options.Integrated)
            {
                if (string.IsNullOrWhiteSpace(_options.UserName) || string.IsNullOrWhiteSpace(_options.PasswordEnvironmentVariable))
                    throw new CliException("Non-integrated authentication requires --user and --password-env.");
                var password = Environment.GetEnvironmentVariable(_options.PasswordEnvironmentVariable);
                if (string.IsNullOrWhiteSpace(password)) throw new CliException("Password environment variable is empty: " + _options.PasswordEnvironmentVariable);
                builder.WindowsDomain = _options.Domain;
                builder.UserID = _options.UserName;
                builder.Password = password;
                builder.CachePassword = false;
            }
            return builder.ConnectionString;
        }
    }
}
