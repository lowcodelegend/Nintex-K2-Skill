using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace K2SqlCli
{
    internal sealed class SqlDeployer
    {
        private static readonly HashSet<string> ProtectedDatabases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "master", "model", "msdb", "tempdb", "K2"
        };

        private readonly DeploymentManifest _manifest;

        public SqlDeployer(DeploymentManifest manifest)
        {
            _manifest = manifest;
        }

        public void CheckConnection()
        {
            using (var connection = OpenConnection("master"))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))";
                command.CommandTimeout = _manifest.Database.CommandTimeoutSeconds;
                var version = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                Console.WriteLine("SQL connection: OK (" + version + ")");
            }
        }

        public bool DatabaseExists()
        {
            using (var connection = OpenConnection("master"))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @name";
                command.Parameters.AddWithValue("@name", _manifest.Database.Name);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
            }
        }

        public void EnsureDatabase()
        {
            if (DatabaseExists())
            {
                Console.WriteLine("Database: exists (" + _manifest.Database.Name + ")");
                return;
            }
            if (!_manifest.Database.CreateIfMissing)
            {
                throw new CliException("Database does not exist and createIfMissing is false: " + _manifest.Database.Name);
            }
            using (var connection = OpenConnection("master"))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE DATABASE " + QuoteIdentifier(_manifest.Database.Name);
                command.CommandTimeout = _manifest.Database.CommandTimeoutSeconds;
                command.ExecuteNonQuery();
            }
            Console.WriteLine("Database: created (" + _manifest.Database.Name + ")");
        }

        public void RunScripts()
        {
            using (var connection = OpenConnection(_manifest.Database.Name))
            {
                foreach (var script in _manifest.Database.Scripts)
                {
                    var path = _manifest.ResolvePath(script);
                    var batches = SplitBatches(File.ReadAllText(path));
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var batch in batches)
                            {
                                for (var repeat = 0; repeat < batch.RepeatCount; repeat++)
                                {
                                    using (var command = connection.CreateCommand())
                                    {
                                        command.Transaction = transaction;
                                        command.CommandTimeout = _manifest.Database.CommandTimeoutSeconds;
                                        command.CommandText = batch.Sql;
                                        command.ExecuteNonQuery();
                                    }
                                }
                            }
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                    Console.WriteLine("SQL script: applied (" + Path.GetFileName(path) + ")");
                }
            }
        }

        public void ApplyApprovalMatrices()
        {
            if (_manifest.ApprovalMatrices.Count == 0) return;
            using (var connection = OpenConnection(_manifest.Database.Name))
                new ApprovalMatrixSql(_manifest).Apply(connection, _manifest.Database.CommandTimeoutSeconds);
        }

        public void EnsureRuntimeAccess()
        {
            var principal = _manifest.Database.RuntimePrincipal;
            if (string.IsNullOrWhiteSpace(principal))
            {
                return;
            }
            using (var connection = OpenConnection(_manifest.Database.Name))
            using (var command = connection.CreateCommand())
            {
                var quoted = QuoteIdentifier(principal);
                command.CommandText =
                    "IF DATABASE_PRINCIPAL_ID(@principal) IS NULL CREATE USER " + quoted + " FOR LOGIN " + quoted + "; " +
                    "GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE, VIEW DEFINITION TO " + quoted + ";";
                command.Parameters.AddWithValue("@principal", principal);
                command.CommandTimeout = _manifest.Database.CommandTimeoutSeconds;
                command.ExecuteNonQuery();
            }
            Console.WriteLine("SQL runtime access: granted (" + principal + ")");
        }

        public void Verify()
        {
            if (!DatabaseExists()) throw new CliException("Database verification failed; database is missing.");
            using (var connection = OpenConnection(_manifest.Database.Name))
            {
                foreach (var expected in _manifest.Verification.SqlObjects)
                {
                    VerifyObject(connection, expected);
                }
                foreach (var query in _manifest.Verification.Queries)
                {
                    VerifyQuery(connection, query);
                }
                new ApprovalMatrixSql(_manifest).Verify(connection, _manifest.Database.CommandTimeoutSeconds);
            }
            Console.WriteLine("SQL verification: OK");
        }

        public void InspectApprovalMatrices()
        {
            if (_manifest.ApprovalMatrices.Count == 0) return;
            using (var connection = OpenConnection(_manifest.Database.Name))
                new ApprovalMatrixSql(_manifest).Inspect(connection, _manifest.Database.CommandTimeoutSeconds);
        }

        public void DropDatabase()
        {
            if (ProtectedDatabases.Contains(_manifest.Database.Name))
            {
                throw new CliException("Refusing to drop protected database: " + _manifest.Database.Name);
            }
            if (!DatabaseExists())
            {
                Console.WriteLine("Database: already absent (" + _manifest.Database.Name + ")");
                return;
            }
            using (var connection = OpenConnection("master"))
            using (var command = connection.CreateCommand())
            {
                var quoted = QuoteIdentifier(_manifest.Database.Name);
                command.CommandText = "ALTER DATABASE " + quoted + " SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE " + quoted + ";";
                command.CommandTimeout = _manifest.Database.CommandTimeoutSeconds;
                command.ExecuteNonQuery();
            }
            Console.WriteLine("Database: dropped (" + _manifest.Database.Name + ")");
        }

        private void VerifyObject(SqlConnection connection, SqlObjectExpectation expected)
        {
            if (expected == null || string.IsNullOrWhiteSpace(expected.Name))
            {
                throw new CliException("Each verification.sqlObjects entry requires a name.");
            }
            var type = (expected.Type ?? "table").ToLowerInvariant();
            var typeCode = type == "table" ? "U" : type == "view" ? "V" : type == "procedure" || type == "stored-procedure" ? "P" : null;
            if (typeCode == null) throw new CliException("Unsupported SQL object type: " + type);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM sys.objects o INNER JOIN sys.schemas s ON s.schema_id=o.schema_id WHERE o.name=@name AND s.name=@schema AND o.type=@type";
                command.Parameters.AddWithValue("@name", expected.Name);
                command.Parameters.AddWithValue("@schema", string.IsNullOrWhiteSpace(expected.Schema) ? "dbo" : expected.Schema);
                command.Parameters.AddWithValue("@type", typeCode);
                if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                {
                    throw new CliException("Missing SQL " + type + ": " + (expected.Schema ?? "dbo") + "." + expected.Name);
                }
            }
        }

        private void VerifyQuery(SqlConnection connection, QueryExpectation expected)
        {
            if (expected == null || string.IsNullOrWhiteSpace(expected.Sql))
            {
                throw new CliException("Each verification.queries entry requires sql.");
            }
            using (var command = connection.CreateCommand())
            {
                command.CommandText = expected.Sql;
                command.CommandTimeout = _manifest.Database.CommandTimeoutSeconds;
                var actual = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                if (expected.ExpectedScalar != null && !string.Equals(actual, expected.ExpectedScalar, StringComparison.Ordinal))
                {
                    throw new CliException("Query '" + (expected.Name ?? expected.Sql) + "' expected '" + expected.ExpectedScalar + "' but returned '" + actual + "'.");
                }
            }
        }

        private SqlConnection OpenConnection(string database)
        {
            var options = _manifest.Database;
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = options.Server,
                InitialCatalog = database,
                IntegratedSecurity = options.IntegratedSecurity,
                ConnectTimeout = 15,
                ApplicationName = "k2sql"
            };
            if (!options.IntegratedSecurity)
            {
                builder.UserID = options.UserName;
                builder.Password = ReadRequiredEnvironmentVariable(options.PasswordEnvironmentVariable);
            }
            var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
            return connection;
        }

        private static string ReadRequiredEnvironmentVariable(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value)) throw new CliException("Required environment variable is not set: " + name);
            return value;
        }

        private static string QuoteIdentifier(string name)
        {
            return "[" + name.Replace("]", "]]" ) + "]";
        }

        private static List<SqlBatch> SplitBatches(string sql)
        {
            var result = new List<SqlBatch>();
            var builder = new StringBuilder();
            var go = new Regex(@"^\s*GO(?:\s+(\d+))?\s*(?:--.*)?$", RegexOptions.IgnoreCase);
            using (var reader = new StringReader(sql))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var match = go.Match(line);
                    if (!match.Success)
                    {
                        builder.AppendLine(line);
                        continue;
                    }
                    AddBatch(result, builder.ToString(), match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 1);
                    builder.Length = 0;
                }
            }
            AddBatch(result, builder.ToString(), 1);
            return result;
        }

        private static void AddBatch(List<SqlBatch> batches, string sql, int repeatCount)
        {
            if (!string.IsNullOrWhiteSpace(sql)) batches.Add(new SqlBatch { Sql = sql, RepeatCount = repeatCount });
        }

        private sealed class SqlBatch
        {
            public string Sql { get; set; }
            public int RepeatCount { get; set; }
        }
    }
}
