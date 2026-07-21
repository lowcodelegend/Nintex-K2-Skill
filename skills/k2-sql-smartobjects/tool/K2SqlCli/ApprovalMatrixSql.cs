using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Security.Principal;
using System.Text;

namespace K2SqlCli
{
    internal sealed class ApprovalMatrixSql
    {
        private readonly DeploymentManifest _manifest;

        public ApprovalMatrixSql(DeploymentManifest manifest)
        {
            _manifest = manifest;
        }

        public string DesignerIdentity
        {
            get { return "K2:" + WindowsIdentity.GetCurrent().Name; }
        }

        public void PrintPlan()
        {
            foreach (var matrix in _manifest.ApprovalMatrices)
            {
                Console.WriteLine("  Approval matrix: upsert " + matrix.MatrixCode + " -> " + matrix.Schema + "." + matrix.Table +
                    ", resolver " + matrix.Schema + "." + matrix.ResolverProcedure + ", " + matrix.Rules.Count + " seeded rule(s)");
                Console.WriteLine("    Dimensions: Amount" + (matrix.Dimensions.Count == 0 ? "" : ", " + string.Join(", ", matrix.Dimensions.Select(x => x.Name).ToArray())));
                var boundRules = matrix.Rules.Count(x => string.Equals(x.Approver, "$designer", StringComparison.OrdinalIgnoreCase));
                if (boundRules > 0) Console.WriteLine("    Explicit deployer-bound destinations: " + boundRules + " $designer rule(s) -> " + DesignerIdentity);
            }
        }

        public void Apply(SqlConnection connection, int commandTimeout)
        {
            foreach (var matrix in _manifest.ApprovalMatrices)
            {
                EnsureTable(connection, commandTimeout, matrix);
                EnsureResolver(connection, commandTimeout, matrix);
                foreach (var rule in matrix.Rules) UpsertRule(connection, commandTimeout, matrix, rule);
                var boundRules = matrix.Rules.Count(x => string.Equals(x.Approver, "$designer", StringComparison.OrdinalIgnoreCase));
                Console.WriteLine("Approval matrix: applied (" + matrix.MatrixCode + ", " + matrix.Rules.Count + " seeded rule(s)" +
                    (boundRules == 0 ? string.Empty : ", explicit $designer destinations=" + boundRules + " -> " + DesignerIdentity) + ")");
            }
        }

        public void Verify(SqlConnection connection, int commandTimeout)
        {
            foreach (var matrix in _manifest.ApprovalMatrices)
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandTimeout = commandTimeout;
                    command.CommandText = "SELECT COUNT(*) FROM " + Qualified(matrix) + " WHERE MatrixCode=@matrixCode AND NULLIF(LTRIM(RTRIM(ApproverValue)),N'') IS NOT NULL";
                    command.Parameters.AddWithValue("@matrixCode", matrix.MatrixCode);
                    var count = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                    if (count < matrix.Rules.Count) throw new CliException("Approval matrix seed verification failed: " + matrix.MatrixCode);
                }

                var sample = matrix.Rules.OrderBy(x => x.Stage).ThenBy(x => x.Priority).First();
                using (var command = connection.CreateCommand())
                {
                    command.CommandTimeout = commandTimeout;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = QualifiedResolver(matrix);
                    command.Parameters.AddWithValue("@MatrixCodeInput", matrix.MatrixCode);
                    command.Parameters.AddWithValue("@AmountInput", sample.MinAmount);
                    command.Parameters.AddWithValue("@CurrentStageInput", Math.Max(0, sample.Stage - 1));
                    foreach (var dimension in matrix.Dimensions)
                    {
                        object value;
                        if (!TryCondition(sample, dimension.Name, out value)) value = DBNull.Value;
                        command.Parameters.Add(Parameter("@" + dimension.Name + "Input", dimension, value));
                    }
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read() || !Convert.ToBoolean(reader["HasApprover"], CultureInfo.InvariantCulture) || string.IsNullOrWhiteSpace(Convert.ToString(reader["ApproverValue"], CultureInfo.InvariantCulture)))
                            throw new CliException("Approval matrix resolver did not return a usable approver: " + matrix.MatrixCode);
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandTimeout = commandTimeout;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = QualifiedResolver(matrix);
                    command.Parameters.AddWithValue("@MatrixCodeInput", matrix.MatrixCode);
                    command.Parameters.AddWithValue("@AmountInput", sample.MinAmount);
                    command.Parameters.AddWithValue("@CurrentStageInput", int.MaxValue);
                    foreach (var dimension in matrix.Dimensions)
                        command.Parameters.Add(Parameter("@" + dimension.Name + "Input", dimension, DBNull.Value));
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read() || Convert.ToBoolean(reader["HasApprover"], CultureInfo.InvariantCulture))
                            throw new CliException("Approval matrix resolver did not return the no-more-stages sentinel: " + matrix.MatrixCode);
                    }
                }
                Console.WriteLine("Approval matrix verification: OK (" + matrix.MatrixCode + ", resolver=" + matrix.Schema + "." + matrix.ResolverProcedure + ")");
            }
        }

        public void Inspect(SqlConnection connection, int commandTimeout)
        {
            foreach (var matrix in _manifest.ApprovalMatrices)
            using (var command = connection.CreateCommand())
            {
                command.CommandTimeout = commandTimeout;
                command.CommandText = "SELECT COUNT(*) FROM " + Qualified(matrix) + " WHERE MatrixCode=@matrixCode";
                command.Parameters.AddWithValue("@matrixCode", matrix.MatrixCode);
                Console.WriteLine("Approval matrix: " + matrix.MatrixCode + " (" + Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) + " row(s), " +
                    matrix.Schema + "." + matrix.Table + " -> " + matrix.Schema + "." + matrix.ResolverProcedure + ")");
            }
        }

        private static void EnsureTable(SqlConnection connection, int commandTimeout, ApprovalMatrixDefinition matrix)
        {
            var constraintSuffix = SafeConstraintSuffix(matrix.Table);
            var sql = new StringBuilder();
            sql.AppendLine("IF SCHEMA_ID(N'" + EscapeLiteral(matrix.Schema) + "') IS NULL EXEC(N'CREATE SCHEMA " + Quote(matrix.Schema) + " AUTHORIZATION dbo');");
            sql.AppendLine("IF OBJECT_ID(N'" + EscapeLiteral(matrix.Schema + "." + matrix.Table) + "', N'U') IS NULL");
            sql.AppendLine("BEGIN");
            sql.AppendLine("  CREATE TABLE " + Qualified(matrix) + "(");
            sql.AppendLine("    RuleId bigint IDENTITY(1,1) NOT NULL CONSTRAINT " + Quote("PK_" + constraintSuffix) + " PRIMARY KEY,");
            sql.AppendLine("    MatrixCode nvarchar(128) NOT NULL,");
            sql.AppendLine("    RuleKey nvarchar(128) NOT NULL,");
            sql.AppendLine("    StageNumber int NOT NULL,");
            sql.AppendLine("    MinAmount decimal(18,2) NOT NULL CONSTRAINT " + Quote("DF_" + constraintSuffix + "_MinAmount") + " DEFAULT(0),");
            sql.AppendLine("    MaxAmount decimal(18,2) NULL,");
            sql.AppendLine("    Priority int NOT NULL CONSTRAINT " + Quote("DF_" + constraintSuffix + "_Priority") + " DEFAULT(100),");
            sql.AppendLine("    ApproverType nvarchar(20) NOT NULL,");
            sql.AppendLine("    ApproverValue nvarchar(256) NOT NULL,");
            sql.AppendLine("    ApproverLabel nvarchar(256) NULL,");
            sql.AppendLine("    IsActive bit NOT NULL CONSTRAINT " + Quote("DF_" + constraintSuffix + "_IsActive") + " DEFAULT(1),");
            sql.AppendLine("    CONSTRAINT " + Quote("UQ_" + constraintSuffix + "_Matrix_Rule") + " UNIQUE(MatrixCode, RuleKey),");
            sql.AppendLine("    CONSTRAINT " + Quote("CK_" + constraintSuffix + "_Stage") + " CHECK(StageNumber > 0),");
            sql.AppendLine("    CONSTRAINT " + Quote("CK_" + constraintSuffix + "_Amount") + " CHECK(MinAmount >= 0 AND (MaxAmount IS NULL OR MaxAmount > MinAmount)),");
            sql.AppendLine("    CONSTRAINT " + Quote("CK_" + constraintSuffix + "_Priority") + " CHECK(Priority > 0),");
            sql.AppendLine("    CONSTRAINT " + Quote("CK_" + constraintSuffix + "_ApproverType") + " CHECK(ApproverType IN (N'User',N'Group',N'Role',N'K2String'))");
            sql.AppendLine("  );");
            sql.AppendLine("END;");
            foreach (var dimension in matrix.Dimensions)
                sql.AppendLine("IF COL_LENGTH(N'" + EscapeLiteral(matrix.Schema + "." + matrix.Table) + "', N'" + EscapeLiteral(dimension.Name) + "') IS NULL ALTER TABLE " + Qualified(matrix) + " ADD " + Quote(dimension.Name) + " " + SqlType(dimension) + " NULL;");
            Execute(connection, commandTimeout, sql.ToString());
        }

        private static void EnsureResolver(SqlConnection connection, int commandTimeout, ApprovalMatrixDefinition matrix)
        {
            var sql = new StringBuilder();
            sql.AppendLine("CREATE OR ALTER PROCEDURE " + QualifiedResolver(matrix));
            sql.AppendLine("  @MatrixCodeInput nvarchar(128),");
            sql.AppendLine("  @AmountInput decimal(18,2),");
            sql.Append("  @CurrentStageInput int = 0");
            foreach (var dimension in matrix.Dimensions) sql.AppendLine(",").Append("  @" + QuoteParameter(dimension.Name) + "Input " + SqlType(dimension) + " = NULL");
            sql.AppendLine();
            sql.AppendLine("AS");
            sql.AppendLine("BEGIN");
            sql.AppendLine("  SET NOCOUNT ON;");
            sql.AppendLine("  SET @CurrentStageInput=ISNULL(@CurrentStageInput,0);");
            sql.AppendLine("  DECLARE @NextStage int;");
            sql.AppendLine("  SELECT @NextStage=MIN(r.StageNumber) FROM " + Qualified(matrix) + " r");
            AppendResolverPredicate(sql, matrix, "  ");
            sql.AppendLine("  IF @NextStage IS NULL");
            sql.AppendLine("  BEGIN");
            sql.AppendLine("    SELECT CAST(0 AS bit) AS HasApprover, CAST(NULL AS bigint) AS RuleId, @MatrixCodeInput AS MatrixCode,");
            sql.AppendLine("      CAST(NULL AS nvarchar(128)) AS RuleKey, @CurrentStageInput AS StageNumber, CAST(NULL AS decimal(18,2)) AS MinAmount,");
            sql.AppendLine("      CAST(NULL AS decimal(18,2)) AS MaxAmount, CAST(NULL AS int) AS Priority, CAST(NULL AS nvarchar(20)) AS ApproverType,");
            sql.Append("      CAST(NULL AS nvarchar(256)) AS ApproverValue, CAST(NULL AS nvarchar(256)) AS ApproverLabel");
            foreach (var dimension in matrix.Dimensions) sql.Append(", CAST(NULL AS " + SqlType(dimension) + ") AS " + Quote(dimension.Name));
            sql.AppendLine(";");
            sql.AppendLine("    RETURN;");
            sql.AppendLine("  END;");
            sql.AppendLine("  SELECT TOP(1) CAST(1 AS bit) AS HasApprover, RuleId, MatrixCode, RuleKey, StageNumber, MinAmount, MaxAmount, Priority,");
            sql.Append("    ApproverType, ApproverValue, ApproverLabel");
            foreach (var dimension in matrix.Dimensions) sql.Append(", " + Quote(dimension.Name));
            sql.AppendLine();
            sql.AppendLine("  FROM " + Qualified(matrix) + " r");
            AppendResolverPredicate(sql, matrix, "  ");
            sql.AppendLine("    AND r.StageNumber=@NextStage ORDER BY Priority, RuleId;");
            sql.AppendLine("END;");
            Execute(connection, commandTimeout, sql.ToString());
        }

        private static void AppendResolverPredicate(StringBuilder sql, ApprovalMatrixDefinition matrix, string indent)
        {
            sql.AppendLine(indent + "WHERE r.MatrixCode=@MatrixCodeInput AND r.IsActive=1 AND r.StageNumber>@CurrentStageInput");
            sql.AppendLine(indent + "  AND r.MinAmount<=@AmountInput AND (r.MaxAmount IS NULL OR @AmountInput<r.MaxAmount)");
            foreach (var dimension in matrix.Dimensions)
                sql.AppendLine(indent + "  AND (r." + Quote(dimension.Name) + " IS NULL OR r." + Quote(dimension.Name) + "=@" + QuoteParameter(dimension.Name) + "Input)");
        }

        private void UpsertRule(SqlConnection connection, int commandTimeout, ApprovalMatrixDefinition matrix, ApprovalMatrixRule rule)
        {
            var dimensionColumns = matrix.Dimensions.Select(x => Quote(x.Name)).ToArray();
            var columns = new List<string> { "MatrixCode", "RuleKey", "StageNumber", "MinAmount", "MaxAmount", "Priority" };
            columns.AddRange(dimensionColumns);
            columns.AddRange(new[] { "ApproverType", "ApproverValue", "ApproverLabel", "IsActive" });
            var parameterNames = columns.Select(x => "@" + x.Trim('[', ']')).ToArray();
            var updates = columns.Where(x => x != "MatrixCode" && x != "RuleKey").Select(x => "target." + x + "=source." + x).ToArray();
            var sql = "MERGE " + Qualified(matrix) + " AS target USING (SELECT " + string.Join(",", parameterNames.Select((x, i) => x + " AS " + columns[i]).ToArray()) + ") AS source " +
                "ON target.MatrixCode=source.MatrixCode AND target.RuleKey=source.RuleKey " +
                "WHEN MATCHED THEN UPDATE SET " + string.Join(",", updates) + " " +
                "WHEN NOT MATCHED THEN INSERT(" + string.Join(",", columns) + ") VALUES(" + string.Join(",", columns.Select(x => "source." + x).ToArray()) + ");";
            using (var command = connection.CreateCommand())
            {
                command.CommandTimeout = commandTimeout;
                command.CommandText = sql;
                command.Parameters.AddWithValue("@MatrixCode", matrix.MatrixCode);
                command.Parameters.AddWithValue("@RuleKey", rule.Key);
                command.Parameters.AddWithValue("@StageNumber", rule.Stage);
                command.Parameters.AddWithValue("@MinAmount", rule.MinAmount);
                command.Parameters.AddWithValue("@MaxAmount", rule.MaxAmount.HasValue ? (object)rule.MaxAmount.Value : DBNull.Value);
                command.Parameters.AddWithValue("@Priority", rule.Priority);
                foreach (var dimension in matrix.Dimensions)
                {
                    object value;
                    if (!TryCondition(rule, dimension.Name, out value)) value = DBNull.Value;
                    command.Parameters.Add(Parameter("@" + dimension.Name, dimension, value));
                }
                command.Parameters.AddWithValue("@ApproverType", CanonicalApproverType(rule.ApproverType));
                command.Parameters.AddWithValue("@ApproverValue", string.Equals(rule.Approver, "$designer", StringComparison.OrdinalIgnoreCase) ? DesignerIdentity : rule.Approver);
                command.Parameters.AddWithValue("@ApproverLabel", string.IsNullOrWhiteSpace(rule.ApproverLabel) ? (object)DBNull.Value : rule.ApproverLabel);
                command.Parameters.AddWithValue("@IsActive", rule.IsActive);
                command.ExecuteNonQuery();
            }
        }

        private static SqlParameter Parameter(string name, ApprovalMatrixDimension dimension, object value)
        {
            var type = dimension.Type.ToLowerInvariant();
            var parameter = new SqlParameter(name, type == "number" ? SqlDbType.Int : type == "decimal" ? SqlDbType.Decimal : type == "guid" ? SqlDbType.UniqueIdentifier : SqlDbType.NVarChar);
            if (type == "text") parameter.Size = dimension.MaxLength;
            if (type == "decimal") { parameter.Precision = 18; parameter.Scale = 2; }
            parameter.Value = value ?? DBNull.Value;
            return parameter;
        }

        private static bool TryCondition(ApprovalMatrixRule rule, string name, out object value)
        {
            foreach (var item in rule.Conditions)
                if (string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase)) { value = item.Value ?? DBNull.Value; return true; }
            value = null;
            return false;
        }

        private static string SqlType(ApprovalMatrixDimension dimension)
        {
            switch (dimension.Type.ToLowerInvariant())
            {
                case "number": return "int";
                case "decimal": return "decimal(18,2)";
                case "guid": return "uniqueidentifier";
                default: return "nvarchar(" + dimension.MaxLength.ToString(CultureInfo.InvariantCulture) + ")";
            }
        }

        private static string CanonicalApproverType(string value)
        {
            if (string.Equals(value, "user", StringComparison.OrdinalIgnoreCase)) return "User";
            if (string.Equals(value, "group", StringComparison.OrdinalIgnoreCase)) return "Group";
            if (string.Equals(value, "role", StringComparison.OrdinalIgnoreCase)) return "Role";
            return "K2String";
        }

        private static void Execute(SqlConnection connection, int commandTimeout, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandTimeout = commandTimeout;
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        private static string Qualified(ApprovalMatrixDefinition matrix) { return Quote(matrix.Schema) + "." + Quote(matrix.Table); }
        private static string QualifiedResolver(ApprovalMatrixDefinition matrix) { return Quote(matrix.Schema) + "." + Quote(matrix.ResolverProcedure); }
        private static string Quote(string value) { return "[" + value.Replace("]", "]]" ) + "]"; }
        private static string QuoteParameter(string value) { return value.Replace("]", ""); }
        private static string EscapeLiteral(string value) { return value.Replace("'", "''"); }
        private static string SafeConstraintSuffix(string value) { return value.Length <= 80 ? value : value.Substring(0, 80); }
    }
}
