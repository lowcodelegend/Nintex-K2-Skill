using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using SourceCode.Hosting.Client.BaseAPI;
using SourceCode.SmartObjects.Client;

namespace K2CaseOperationsCli
{
    internal static class Program
    {
        private const string Version = "0.2.0";
        private const int MaximumRows = 500;

        private sealed class OperationDefinition
        {
            public string SmartObject { get; set; }
            public string Method { get; set; }
            public string[] AllowedInputs { get; set; }
        }

        private static readonly IDictionary<string, OperationDefinition> OperationContract =
            new Dictionary<string, OperationDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "search_cases",
                    Define(null, "List")
                },
                {
                    "get_case",
                    Define(null, "List", "CaseId")
                },
                {
                    "get_case_timeline",
                    Define(null, "List", "CaseId")
                },
                {
                    "list_case_evidence",
                    Define(null, "List", "CaseId")
                },
                {
                    "get_allowed_case_actions",
                    Define(null, "List", "CaseId")
                },
                {
                    "get_submission_readiness",
                    Define(null, "List", "CaseId")
                },
                {
                    "get_case_action_status",
                    Define(
                        null,
                        "List",
                        "CaseId",
                        "CommandId",
                        "IdempotencyKey",
                        "CorrelationId")
                },
                {
                    "get_case_record",
                    Define(null, "List", "CaseId")
                },
                {
                    "list_stage_transitions",
                    Define(null, "List")
                }
            };

        private sealed class MappingDocument
        {
            public int SchemaVersion { get; set; }
            public Dictionary<string, OperationDefinition> Operations { get; set; }
        }

        public static int Main(string[] args)
        {
            if (args.Length == 1 &&
                string.Equals(args[0], "version", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("k2caseops " + Version);
                return 0;
            }

            RuntimeAssemblyResolver.Install();
            try
            {
                var validateMapping =
                    args.Length > 0 &&
                    string.Equals(
                        args[0],
                        "validate-mapping",
                        StringComparison.OrdinalIgnoreCase);
                var options = ParseArguments(
                    validateMapping ? args.Skip(1).ToArray() : args);
                var operations = LoadMapping(Required(options, "mapping"));
                if (validateMapping)
                {
                    Console.WriteLine(
                        "Valid case operations mapping: " +
                        Path.GetFullPath(options["mapping"]));
                    return 0;
                }
                var operation = Required(options, "operation");
                OperationDefinition definition;
                if (!operations.TryGetValue(operation, out definition))
                {
                    throw new ArgumentException("Unsupported case operation: " + operation);
                }

                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var inputs = options.ContainsKey("inputs-json")
                    ? serializer.Deserialize<Dictionary<string, object>>(options["inputs-json"])
                    : new Dictionary<string, object>();
                ValidateInputs(definition, inputs);

                var rows = Execute(
                    definition,
                    inputs,
                    options.ContainsKey("host") ? options["host"] : "localhost",
                    options.ContainsKey("port")
                        ? int.Parse(options["port"], CultureInfo.InvariantCulture)
                        : 5555,
                    options.ContainsKey("security-label")
                        ? options["security-label"]
                        : "K2");

                Console.WriteLine(serializer.Serialize(new Dictionary<string, object>
                {
                    { "operation", operation },
                    { "rows", rows },
                    { "bounded", true },
                    { "maximumRows", MaximumRows }
                }));
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    exception.GetType().Name + ": " + exception.Message +
                    (exception.InnerException == null
                        ? string.Empty
                        : " Inner: " + exception.InnerException.Message));
                return 1;
            }
        }

        private static IList<IDictionary<string, object>> Execute(
            OperationDefinition definition,
            IDictionary<string, object> inputs,
            string host,
            int port,
            string securityLabel)
        {
            var server = new SmartObjectClientServer();
            try
            {
                server.CreateConnection();
                var connection = new SCConnectionStringBuilder
                {
                    Authenticate = true,
                    Host = host,
                    Port = (uint)port,
                    Integrated = true,
                    IsPrimaryLogin = true,
                    SecurityLabelName = securityLabel
                };
                server.Connection.Open(connection.ConnectionString);

                var smartObject = server.GetSmartObject(definition.SmartObject);
                smartObject.MethodToExecute = definition.Method;
                var selectedMethod = smartObject.ListMethods.Cast<SmartListMethod>()
                    .FirstOrDefault(value =>
                        string.Equals(
                            value.Name,
                            definition.Method,
                            StringComparison.OrdinalIgnoreCase));
                foreach (var input in inputs)
                {
                    var inputValue =
                        Convert.ToString(input.Value, CultureInfo.InvariantCulture);
                    var properties = smartObject.Properties.Cast<SmartProperty>()
                        .Where(value =>
                            string.Equals(
                                value.Name,
                                input.Key,
                                StringComparison.OrdinalIgnoreCase) ||
                            value.Name.StartsWith(
                                input.Key + "_",
                                StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (properties.Count == 0)
                    {
                        properties = new List<SmartProperty>();
                    }
                    foreach (var property in properties)
                    {
                        property.Value = inputValue;
                    }
                    var requiredProperties = selectedMethod == null
                        ? new List<SmartProperty>()
                        : selectedMethod.RequiredProperties.Cast<SmartProperty>()
                            .Where(value =>
                                string.Equals(
                                    value.Name,
                                    input.Key,
                                    StringComparison.OrdinalIgnoreCase) ||
                                value.Name.StartsWith(
                                    input.Key + "_",
                                    StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    foreach (var property in requiredProperties)
                    {
                        property.Value = inputValue;
                    }
                    if (properties.Count == 0 && requiredProperties.Count == 0)
                    {
                        throw new ArgumentException(
                            "The deployed SmartObject does not expose input " + input.Key + ".");
                    }
                }

                var table = server.ExecuteListDataTable(smartObject, 1, MaximumRows);
                return ToRecords(table);
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

        private static IList<IDictionary<string, object>> ToRecords(DataTable table)
        {
            var records = new List<IDictionary<string, object>>();
            foreach (DataRow row in table.Rows)
            {
                var record = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (DataColumn column in table.Columns)
                {
                    record[column.ColumnName] = JsonValue(row[column]);
                }
                records.Add(record);
            }
            return records;
        }

        private static object JsonValue(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }
            var bytes = value as byte[];
            if (bytes != null)
            {
                return Convert.ToBase64String(bytes);
            }
            if (value is DateTime)
            {
                return ((DateTime)value).ToString("o", CultureInfo.InvariantCulture);
            }
            if (value is Guid)
            {
                return value.ToString();
            }
            return value;
        }

        private static OperationDefinition Define(
            string smartObject,
            string method,
            params string[] allowedInputs)
        {
            return new OperationDefinition
            {
                SmartObject = smartObject,
                Method = method,
                AllowedInputs = allowedInputs
            };
        }

        private static IDictionary<string, OperationDefinition> LoadMapping(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    "Case operations mapping was not found.",
                    fullPath);
            }
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var document = serializer.Deserialize<MappingDocument>(
                File.ReadAllText(fullPath));
            if (document == null || document.SchemaVersion != 1)
            {
                throw new ArgumentException(
                    "Case operations mapping schemaVersion must be 1.");
            }
            if (document.Operations == null)
            {
                throw new ArgumentException(
                    "Case operations mapping must contain operations.");
            }
            var unexpected = document.Operations.Keys
                .Where(value => !OperationContract.ContainsKey(value))
                .ToArray();
            if (unexpected.Length > 0)
            {
                throw new ArgumentException(
                    "Case operations mapping contains unsupported operations: " +
                    string.Join(", ", unexpected));
            }
            foreach (var contract in OperationContract)
            {
                OperationDefinition configured;
                if (!document.Operations.TryGetValue(contract.Key, out configured) ||
                    configured == null)
                {
                    throw new ArgumentException(
                        "Case operations mapping is missing operation: " + contract.Key);
                }
                if (string.IsNullOrWhiteSpace(configured.SmartObject))
                {
                    throw new ArgumentException(
                        "Case operation " + contract.Key +
                        " must name a SmartObject.");
                }
                if (!string.Equals(
                    configured.Method,
                    contract.Value.Method,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        "Case operation " + contract.Key +
                        " must use the read-only List method.");
                }
                var configuredInputs = configured.AllowedInputs ?? new string[0];
                var expectedInputs = contract.Value.AllowedInputs ?? new string[0];
                if (configuredInputs.Length != expectedInputs.Length ||
                    configuredInputs.Any(
                        value => !expectedInputs.Contains(
                            value,
                            StringComparer.OrdinalIgnoreCase)) ||
                    expectedInputs.Any(
                        value => !configuredInputs.Contains(
                            value,
                            StringComparer.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException(
                        "Case operation " + contract.Key +
                        " has an invalid allowedInputs contract.");
                }
            }
            return new Dictionary<string, OperationDefinition>(
                document.Operations,
                StringComparer.OrdinalIgnoreCase);
        }

        private static void ValidateInputs(
            OperationDefinition definition,
            IDictionary<string, object> inputs)
        {
            foreach (var key in inputs.Keys)
            {
                if (!definition.AllowedInputs.Contains(
                    key,
                    StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("Unsupported operation input: " + key);
                }
            }
        }

        private static IDictionary<string, string> ParseArguments(string[] args)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < args.Length; index += 2)
            {
                if (!args[index].StartsWith("--", StringComparison.Ordinal) ||
                    index + 1 >= args.Length)
                {
                    throw new ArgumentException(
                        "Arguments must be supplied as --name value pairs.");
                }
                result[args[index].Substring(2)] = args[index + 1];
            }
            return result;
        }

        private static string Required(IDictionary<string, string> values, string key)
        {
            string value;
            if (!values.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("--" + key + " is required.");
            }
            return value;
        }
    }
}
