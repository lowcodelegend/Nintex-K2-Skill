using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;
using SourceCode.Hosting.Client.BaseAPI;
using SourceCode.SmartObjects.Client;

namespace K2CaseOperationsCli
{
    internal static class Program
    {
        private const int MaximumRows = 500;

        private sealed class OperationDefinition
        {
            public string SmartObject { get; set; }
            public string Method { get; set; }
            public string[] AllowedInputs { get; set; }
        }

        private static readonly IDictionary<string, OperationDefinition> Operations =
            new Dictionary<string, OperationDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "search_cases",
                    Define("RQB_Raqeeb_Sql_RQBRPT_CaseQueue", "List")
                },
                {
                    "get_case",
                    Define("RQB_Raqeeb_Sql_RQBRPT_CaseWorkspace", "List", "CaseId")
                },
                {
                    "get_case_timeline",
                    Define("RQB_Raqeeb_Sql_RQBRPT_CaseTimeline", "List", "CaseId")
                },
                {
                    "list_case_evidence",
                    Define("RQB_Raqeeb_Sql_RQBRPT_EvidenceMatrix", "List", "CaseId")
                },
                {
                    "get_allowed_case_actions",
                    Define("RQB_Raqeeb_Sql_RQB_AllowedAction_List", "List", "CaseId")
                },
                {
                    "get_submission_readiness",
                    Define("RQB_Raqeeb_Sql_RQB_SubmissionReadiness_Get", "List", "CaseId")
                },
                {
                    "get_case_action_status",
                    Define(
                        "RQB_Raqeeb_Sql_RQB_CaseCommand",
                        "List",
                        "CaseId",
                        "CommandId",
                        "IdempotencyKey",
                        "CorrelationId")
                },
                {
                    "get_case_record",
                    Define("RQB_Raqeeb_Sql_RQB_Case", "List", "CaseId")
                },
                {
                    "list_stage_transitions",
                    Define("RQB_Raqeeb_Sql_RQB_AllowedStageTransition", "List")
                }
            };

        public static int Main(string[] args)
        {
            RuntimeAssemblyResolver.Install();
            try
            {
                var options = ParseArguments(args);
                var operation = Required(options, "operation");
                OperationDefinition definition;
                if (!Operations.TryGetValue(operation, out definition))
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
