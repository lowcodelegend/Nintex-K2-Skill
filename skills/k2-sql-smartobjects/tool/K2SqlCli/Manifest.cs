using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public List<ApprovalMatrixDefinition> ApprovalMatrices { get; set; }
        public List<MasterDetailDefinition> MasterDetails { get; set; }
        public VerificationOptions Verification { get; set; }

        public DeploymentManifest()
        {
            Application = new ApplicationOptions();
            Database = new DatabaseOptions();
            K2 = new K2Options();
            ApprovalMatrices = new List<ApprovalMatrixDefinition>();
            MasterDetails = new List<MasterDetailDefinition>();
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
            if (ApprovalMatrices == null) ApprovalMatrices = new List<ApprovalMatrixDefinition>();
            if (MasterDetails == null) MasterDetails = new List<MasterDetailDefinition>();
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

            ValidateApprovalMatrices();
            ValidateMasterDetails();
        }

        private void ValidateMasterDetails()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var relationship in MasterDetails)
            {
                if (relationship == null) throw new CliException("masterDetails entries cannot be null.");
                Require(relationship.Name, "masterDetails.name");
                if (!names.Add(relationship.Name)) throw new CliException("masterDetails.name values must be unique: " + relationship.Name);
                Require(relationship.MasterSchema, "masterDetails.masterSchema");
                Require(relationship.MasterTable, "masterDetails.masterTable");
                Require(relationship.MasterKey, "masterDetails.masterKey");
                Require(relationship.DetailSchema, "masterDetails.detailSchema");
                Require(relationship.DetailTable, "masterDetails.detailTable");
                Require(relationship.DetailKey, "masterDetails.detailKey");
                Require(relationship.DetailForeignKey, "masterDetails.detailForeignKey");
                ValidateSqlIdentifier(relationship.MasterSchema, "masterDetails.masterSchema");
                ValidateSqlIdentifier(relationship.MasterTable, "masterDetails.masterTable");
                ValidateSqlIdentifier(relationship.MasterKey, "masterDetails.masterKey");
                ValidateSqlIdentifier(relationship.DetailSchema, "masterDetails.detailSchema");
                ValidateSqlIdentifier(relationship.DetailTable, "masterDetails.detailTable");
                ValidateSqlIdentifier(relationship.DetailKey, "masterDetails.detailKey");
                ValidateSqlIdentifier(relationship.DetailForeignKey, "masterDetails.detailForeignKey");
                relationship.DeleteBehavior = string.IsNullOrWhiteSpace(relationship.DeleteBehavior)
                    ? "restrict"
                    : relationship.DeleteBehavior.Trim().ToLowerInvariant();
                if (!new[] { "restrict", "no-action", "cascade" }.Contains(relationship.DeleteBehavior, StringComparer.OrdinalIgnoreCase))
                    throw new CliException("masterDetails.deleteBehavior must be restrict, no-action, or cascade: " + relationship.Name);
                AddSqlExpectation("table", relationship.MasterSchema, relationship.MasterTable);
                AddSqlExpectation("table", relationship.DetailSchema, relationship.DetailTable);
                AddServiceObjectExpectation(relationship.MasterSchema + "-" + relationship.MasterTable);
                AddServiceObjectExpectation(relationship.DetailSchema + "-" + relationship.DetailTable);
            }
        }

        private void ValidateApprovalMatrices()
        {
            var matrixCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var matrix in ApprovalMatrices)
            {
                if (matrix == null) throw new CliException("approvalMatrices entries cannot be null.");
                Require(matrix.Name, "approvalMatrices.name");
                Require(matrix.Schema, "approvalMatrices.schema");
                Require(matrix.Table, "approvalMatrices.table");
                Require(matrix.ResolverProcedure, "approvalMatrices.resolverProcedure");
                Require(matrix.MatrixCode, "approvalMatrices.matrixCode");
                ValidateSqlIdentifier(matrix.Schema, "approvalMatrices.schema");
                ValidateSqlIdentifier(matrix.Table, "approvalMatrices.table");
                ValidateSqlIdentifier(matrix.ResolverProcedure, "approvalMatrices.resolverProcedure");
                if (!matrixCodes.Add(matrix.MatrixCode)) throw new CliException("approvalMatrices.matrixCode values must be unique: " + matrix.MatrixCode);
                if (matrix.Dimensions == null) matrix.Dimensions = new List<ApprovalMatrixDimension>();
                if (matrix.Rules == null || matrix.Rules.Count == 0) throw new CliException("Approval matrix must contain at least one rule: " + matrix.MatrixCode);
                var dimensionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var dimension in matrix.Dimensions)
                {
                    if (dimension == null) throw new CliException("Approval matrix dimensions cannot be null: " + matrix.MatrixCode);
                    Require(dimension.Name, "approvalMatrices.dimensions.name");
                    ValidateSqlIdentifier(dimension.Name, "approvalMatrices.dimensions.name");
                    if (!dimensionNames.Add(dimension.Name)) throw new CliException("Approval matrix dimension names must be unique: " + dimension.Name);
                    if (string.IsNullOrWhiteSpace(dimension.Type)) dimension.Type = "text";
                    var type = dimension.Type.ToLowerInvariant();
                    if (type != "text" && type != "number" && type != "decimal" && type != "guid")
                        throw new CliException("Approval matrix dimension type must be text, number, decimal, or guid: " + dimension.Name);
                    if (type == "text" && dimension.MaxLength <= 0) dimension.MaxLength = 100;
                    if (dimension.MaxLength > 4000) throw new CliException("Approval matrix text dimension maxLength cannot exceed 4000: " + dimension.Name);
                }
                var ruleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var rule in matrix.Rules)
                {
                    if (rule == null) throw new CliException("Approval matrix rules cannot be null: " + matrix.MatrixCode);
                    Require(rule.Key, "approvalMatrices.rules.key");
                    if (!ruleKeys.Add(rule.Key)) throw new CliException("Approval matrix rule keys must be unique within " + matrix.MatrixCode + ": " + rule.Key);
                    if (rule.Stage <= 0) throw new CliException("Approval matrix rule stage must be positive: " + matrix.MatrixCode + "/" + rule.Key);
                    if (rule.MinAmount < 0) throw new CliException("Approval matrix rule minAmount cannot be negative: " + matrix.MatrixCode + "/" + rule.Key);
                    if (rule.MaxAmount.HasValue && rule.MaxAmount.Value <= rule.MinAmount)
                        throw new CliException("Approval matrix rule maxAmount must be greater than minAmount: " + matrix.MatrixCode + "/" + rule.Key);
                    if (rule.Priority <= 0) rule.Priority = 100;
                    if (string.IsNullOrWhiteSpace(rule.ApproverType)) rule.ApproverType = "User";
                    if (!new[] { "User", "Group", "Role", "K2String" }.Contains(rule.ApproverType, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("Approval matrix approverType must be User, Group, Role, or K2String: " + matrix.MatrixCode + "/" + rule.Key);
                    if (string.IsNullOrWhiteSpace(rule.Approver)) rule.Approver = "$designer";
                    if (rule.Conditions == null) rule.Conditions = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    foreach (var condition in rule.Conditions.Keys)
                        if (!dimensionNames.Contains(condition)) throw new CliException("Approval matrix rule uses an undeclared dimension '" + condition + "': " + matrix.MatrixCode + "/" + rule.Key);
                }

                AddSqlExpectation("table", matrix.Schema, matrix.Table);
                AddSqlExpectation("procedure", matrix.Schema, matrix.ResolverProcedure);
                AddServiceObjectExpectation(matrix.Schema + "-" + matrix.Table);
                AddServiceObjectExpectation(matrix.Schema + "-" + matrix.ResolverProcedure);
            }
        }

        private void AddSqlExpectation(string type, string schema, string name)
        {
            if (!Verification.SqlObjects.Any(x => x != null && string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Schema, schema, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                Verification.SqlObjects.Add(new SqlObjectExpectation { Type = type, Schema = schema, Name = name });
        }

        private void AddServiceObjectExpectation(string name)
        {
            if (!Verification.SmartObjectServiceObjects.Contains(name, StringComparer.OrdinalIgnoreCase))
                Verification.SmartObjectServiceObjects.Add(name);
        }

        private static void ValidateSqlIdentifier(string value, string field)
        {
            if (!Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new CliException(field + " must be a conventional SQL identifier: " + value);
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

    public sealed class ApprovalMatrixDefinition
    {
        public string Name { get; set; }
        public string Schema { get; set; }
        public string Table { get; set; }
        public string ResolverProcedure { get; set; }
        public string MatrixCode { get; set; }
        public List<ApprovalMatrixDimension> Dimensions { get; set; }
        public List<ApprovalMatrixRule> Rules { get; set; }

        public ApprovalMatrixDefinition()
        {
            Table = "ApprovalMatrixRule";
            ResolverProcedure = "ResolveApprovalMatrix";
            Dimensions = new List<ApprovalMatrixDimension>();
            Rules = new List<ApprovalMatrixRule>();
        }
    }

    public sealed class MasterDetailDefinition
    {
        public string Name { get; set; }
        public string MasterSchema { get; set; }
        public string MasterTable { get; set; }
        public string MasterKey { get; set; }
        public string DetailSchema { get; set; }
        public string DetailTable { get; set; }
        public string DetailKey { get; set; }
        public string DetailForeignKey { get; set; }
        public string DeleteBehavior { get; set; }
        public bool RequireIdentityMasterKey { get; set; }
        public bool RequireForeignKeyIndex { get; set; }

        public MasterDetailDefinition()
        {
            DeleteBehavior = "restrict";
            RequireIdentityMasterKey = true;
            RequireForeignKeyIndex = true;
        }
    }

    public sealed class ApprovalMatrixDimension
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public int MaxLength { get; set; }

        public ApprovalMatrixDimension()
        {
            Type = "text";
            MaxLength = 100;
        }
    }

    public sealed class ApprovalMatrixRule
    {
        public string Key { get; set; }
        public int Stage { get; set; }
        public decimal MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public int Priority { get; set; }
        public Dictionary<string, object> Conditions { get; set; }
        public string ApproverType { get; set; }
        public string Approver { get; set; }
        public string ApproverLabel { get; set; }
        public bool IsActive { get; set; }

        public ApprovalMatrixRule()
        {
            Priority = 100;
            Conditions = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            ApproverType = "User";
            Approver = "$designer";
            IsActive = true;
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
