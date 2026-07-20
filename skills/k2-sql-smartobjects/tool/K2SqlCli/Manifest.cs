using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace K2SqlCli
{
    public sealed class DeploymentManifest
    {
        public string Name { get; set; }
        public ApplicationOptions Application { get; set; }
        public DatabaseOptions Database { get; set; }
        public K2Options K2 { get; set; }
        public VerificationOptions Verification { get; set; }

        public DeploymentManifest()
        {
            Application = new ApplicationOptions();
            Database = new DatabaseOptions();
            K2 = new K2Options();
            Verification = new VerificationOptions();
        }

        public static DeploymentManifest Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new CliException("Specify --manifest <path>.");
            }
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new CliException("Manifest not found: " + fullPath);
            }
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            DeploymentManifest manifest;
            try
            {
                manifest = serializer.Deserialize<DeploymentManifest>(File.ReadAllText(fullPath));
            }
            catch (Exception ex)
            {
                throw new CliException("Invalid manifest JSON: " + ex.Message);
            }
            if (manifest == null)
            {
                throw new CliException("Manifest is empty.");
            }
            manifest.ManifestPath = fullPath;
            manifest.NormalizeAndValidate();
            return manifest;
        }

        [ScriptIgnore]
        public string ManifestPath { get; private set; }

        [ScriptIgnore]
        public string BaseDirectory { get { return Path.GetDirectoryName(ManifestPath); } }

        private void NormalizeAndValidate()
        {
            if (Application == null) Application = new ApplicationOptions();
            if (Database == null) Database = new DatabaseOptions();
            if (K2 == null) K2 = new K2Options();
            if (Verification == null) Verification = new VerificationOptions();
            if (K2.ServiceInstance == null) K2.ServiceInstance = new ServiceInstanceOptions();
            if (K2.SmartObjects == null) K2.SmartObjects = new SmartObjectGenerationOptions();
            if (Database.Scripts == null) Database.Scripts = new List<string>();
            if (Verification.SqlObjects == null) Verification.SqlObjects = new List<SqlObjectExpectation>();
            if (Verification.Queries == null) Verification.Queries = new List<QueryExpectation>();
            if (Verification.SmartObjectServiceObjects == null) Verification.SmartObjectServiceObjects = new List<string>();

            Require(Name, "name");
            if (!string.IsNullOrWhiteSpace(Application.RootCategoryPath))
            {
                Application.RootCategoryPath = Application.RootCategoryPath.Trim().TrimEnd('\\', '/');
                if (Application.RootCategoryPath.Length == 0)
                {
                    throw new CliException("application.rootCategoryPath must contain at least one category name.");
                }
                var segments = Application.RootCategoryPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                var leaf = segments[segments.Length - 1];
                if (string.Equals(leaf, "Data", StringComparison.OrdinalIgnoreCase))
                {
                    throw new CliException("application.rootCategoryPath must be the solution root, not its Data subfolder.");
                }
            }
            Require(Database.Server, "database.server");
            Require(Database.Name, "database.name");
            Require(K2.Host, "k2.host");
            Require(K2.ServiceInstance.SystemName, "k2.serviceInstance.systemName");
            if (K2.Port <= 0 || K2.Port > 65535) throw new CliException("k2.port must be between 1 and 65535.");
            if (Database.CommandTimeoutSeconds <= 0) Database.CommandTimeoutSeconds = 120;
            if (K2.ServiceInstance.CommandTimeoutSeconds <= 0) K2.ServiceInstance.CommandTimeoutSeconds = 30;

            if (!Regex.IsMatch(K2.ServiceInstance.SystemName, @"^[A-Za-z0-9_.-]+$"))
            {
                throw new CliException("k2.serviceInstance.systemName may contain only letters, digits, underscore, dot, and hyphen.");
            }
            if (string.IsNullOrWhiteSpace(K2.ServiceInstance.DisplayName))
            {
                K2.ServiceInstance.DisplayName = K2.ServiceInstance.SystemName;
            }
            if (string.IsNullOrWhiteSpace(K2.ServiceInstance.Description))
            {
                K2.ServiceInstance.Description = "Managed by k2sql for " + Name;
            }
            if (string.IsNullOrWhiteSpace(K2.ServiceInstance.SqlServer)) K2.ServiceInstance.SqlServer = Database.Server;
            if (string.IsNullOrWhiteSpace(K2.ServiceInstance.Database)) K2.ServiceInstance.Database = Database.Name;
            if (string.IsNullOrWhiteSpace(K2.ServiceInstance.AuthenticationMode)) K2.ServiceInstance.AuthenticationMode = "service-account";
            var mode = K2.ServiceInstance.AuthenticationMode.ToLowerInvariant();
            if (mode != "service-account" && mode != "impersonate" && mode != "static")
            {
                throw new CliException("k2.serviceInstance.authenticationMode must be service-account, impersonate, or static.");
            }
            if (mode == "static")
            {
                Require(K2.ServiceInstance.UserName, "k2.serviceInstance.userName");
                Require(K2.ServiceInstance.PasswordEnvironmentVariable, "k2.serviceInstance.passwordEnvironmentVariable");
            }
            if (!Database.IntegratedSecurity)
            {
                Require(Database.UserName, "database.userName");
                Require(Database.PasswordEnvironmentVariable, "database.passwordEnvironmentVariable");
            }

            foreach (var script in Database.Scripts)
            {
                var scriptPath = ResolvePath(script);
                if (!File.Exists(scriptPath)) throw new CliException("SQL script not found: " + scriptPath);
            }
        }

        public string ResolvePath(string relativeOrAbsolute)
        {
            return Path.GetFullPath(Path.IsPathRooted(relativeOrAbsolute)
                ? relativeOrAbsolute
                : Path.Combine(BaseDirectory, relativeOrAbsolute));
        }

        private static void Require(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new CliException("Manifest field '" + field + "' is required.");
        }
    }

    public sealed class ApplicationOptions
    {
        public string RootCategoryPath { get; set; }

        [ScriptIgnore]
        public string DataCategoryPath
        {
            get
            {
                return string.IsNullOrWhiteSpace(RootCategoryPath)
                    ? null
                    : RootCategoryPath + "\\Data";
            }
        }
    }

    public sealed class DatabaseOptions
    {
        public string Server { get; set; }
        public string Name { get; set; }
        public bool IntegratedSecurity { get; set; }
        public string UserName { get; set; }
        public string PasswordEnvironmentVariable { get; set; }
        public bool CreateIfMissing { get; set; }
        public int CommandTimeoutSeconds { get; set; }
        public string RuntimePrincipal { get; set; }
        public List<string> Scripts { get; set; }

        public DatabaseOptions()
        {
            Server = "localhost";
            IntegratedSecurity = true;
            CreateIfMissing = true;
            CommandTimeoutSeconds = 120;
            Scripts = new List<string>();
        }
    }

    public sealed class K2Options
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool Integrated { get; set; }
        public string SecurityLabel { get; set; }
        public string Domain { get; set; }
        public string UserName { get; set; }
        public string PasswordEnvironmentVariable { get; set; }
        public ServiceInstanceOptions ServiceInstance { get; set; }
        public SmartObjectGenerationOptions SmartObjects { get; set; }

        public K2Options()
        {
            Host = "localhost";
            Port = 5555;
            Integrated = true;
            SecurityLabel = "K2";
            ServiceInstance = new ServiceInstanceOptions();
            SmartObjects = new SmartObjectGenerationOptions();
        }
    }

    public sealed class ServiceInstanceOptions
    {
        public string SystemName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string SqlServer { get; set; }
        public string Database { get; set; }
        public string AuthenticationMode { get; set; }
        public string UserName { get; set; }
        public string PasswordEnvironmentVariable { get; set; }
        public int CommandTimeoutSeconds { get; set; }
        public bool UseNativeSqlExecution { get; set; }
        public bool UseSqlPaging { get; set; }
        public bool EncryptConnection { get; set; }
        public bool OnDifferentSqlServer { get; set; }

        public ServiceInstanceOptions()
        {
            AuthenticationMode = "service-account";
            CommandTimeoutSeconds = 30;
            UseNativeSqlExecution = true;
        }
    }

    public sealed class SmartObjectGenerationOptions
    {
        public bool CreateNew { get; set; }
        public bool UpdateExisting { get; set; }
        public bool DeleteRemoved { get; set; }

        public SmartObjectGenerationOptions()
        {
            CreateNew = true;
            UpdateExisting = true;
            DeleteRemoved = false;
        }
    }

    public sealed class VerificationOptions
    {
        public List<SqlObjectExpectation> SqlObjects { get; set; }
        public List<QueryExpectation> Queries { get; set; }
        public List<string> SmartObjectServiceObjects { get; set; }
        public int MinimumGeneratedSmartObjects { get; set; }
        public bool SmokeTestListMethods { get; set; }

        public VerificationOptions()
        {
            SqlObjects = new List<SqlObjectExpectation>();
            Queries = new List<QueryExpectation>();
            SmartObjectServiceObjects = new List<string>();
            MinimumGeneratedSmartObjects = 1;
            SmokeTestListMethods = true;
        }
    }

    public sealed class SqlObjectExpectation
    {
        public string Type { get; set; }
        public string Schema { get; set; }
        public string Name { get; set; }
    }

    public sealed class QueryExpectation
    {
        public string Name { get; set; }
        public string Sql { get; set; }
        public string ExpectedScalar { get; set; }
    }

    internal sealed class CliException : Exception
    {
        public CliException(string message) : base(message) { }
    }
}
