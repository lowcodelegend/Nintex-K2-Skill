using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace K2SqlCli
{
    internal static class Cli
    {
        public static int Run(string[] args)
        {
            if (args == null || args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp();
                return 0;
            }

            var command = args[0].ToLowerInvariant();
            var options = ParseOptions(args.Skip(1).ToArray());
            if (command == "version")
            {
                PrintVersion();
                return 0;
            }

            var manifestPath = GetOption(options, "manifest", true);
            var manifest = DeploymentManifest.Load(manifestPath);
            var sql = new SqlDeployer(manifest);
            var k2 = new K2Manager(manifest);

            switch (command)
            {
                case "doctor":
                    PrintVersion();
                    Console.WriteLine("K2 install: " + RuntimeAssemblyResolver.InstallDirectory);
                    sql.CheckConnection();
                    k2.CheckConnection();
                    return 0;

                case "plan":
                    PrintPlan(manifest, sql, k2);
                    return 0;

                case "deploy":
                    RequireConfirmation(options, "deploy");
                    sql.EnsureDatabase();
                    sql.RunScripts();
                    sql.EnsureRuntimeAccess();
                    var instanceGuid = k2.EnsureServiceInstance();
                    k2.GenerateSmartObjects(instanceGuid);
                    sql.Verify();
                    k2.Verify();
                    Console.WriteLine("DEPLOYMENT SUCCEEDED: " + manifest.Name);
                    return 0;

                case "verify":
                    sql.Verify();
                    k2.Verify();
                    Console.WriteLine("VERIFICATION SUCCEEDED: " + manifest.Name);
                    return 0;

                case "inspect":
                    Inspect(k2);
                    return 0;

                case "cleanup":
                    RequireConfirmation(options, "cleanup");
                    k2.DeleteGeneratedSmartObjectsAndServiceInstance();
                    if (HasFlag(options, "drop-database"))
                    {
                        sql.DropDatabase();
                    }
                    Console.WriteLine("CLEANUP SUCCEEDED: " + manifest.Name);
                    return 0;

                default:
                    throw new CliException("Unknown command: " + command);
            }
        }

        private static void PrintPlan(DeploymentManifest manifest, SqlDeployer sql, K2Manager k2)
        {
            sql.CheckConnection();
            k2.CheckConnection();
            var databaseState = sql.DatabaseExists() ? "update existing" : "create";
            var instance = k2.GetServiceInstanceState();
            var serviceState = instance == null ? "create" : "update and refresh (" + instance.Guid + ")";
            Console.WriteLine("Plan: " + manifest.Name);
            Console.WriteLine("  Database: " + databaseState + " " + manifest.Database.Server + "/" + manifest.Database.Name);
            foreach (var script in manifest.Database.Scripts)
            {
                Console.WriteLine("  SQL script: apply " + manifest.ResolvePath(script));
            }
            if (!string.IsNullOrWhiteSpace(manifest.Database.RuntimePrincipal))
            {
                Console.WriteLine("  SQL runtime access: grant DML, EXECUTE, and VIEW DEFINITION to " + manifest.Database.RuntimePrincipal);
            }
            Console.WriteLine("  K2 Service Instance: " + serviceState + " " + manifest.K2.ServiceInstance.SystemName);
            Console.WriteLine(string.Format("  SmartObjects: generate createNew={0}, updateExisting={1}, deleteRemoved={2}", manifest.K2.SmartObjects.CreateNew, manifest.K2.SmartObjects.UpdateExisting, manifest.K2.SmartObjects.DeleteRemoved));
            Console.WriteLine("  Verify: " + manifest.Verification.SqlObjects.Count + " SQL object(s), " + manifest.Verification.Queries.Count + " query assertion(s), minimum " + manifest.Verification.MinimumGeneratedSmartObjects + " SmartObject(s)");
        }

        private static void Inspect(K2Manager k2)
        {
            var instance = k2.GetServiceInstanceState();
            if (instance == null)
            {
                Console.WriteLine("K2 Service Instance: absent");
                return;
            }
            Console.WriteLine("K2 Service Instance: " + instance.SystemName + " (" + instance.Guid + ")");
            foreach (var item in k2.GetGeneratedSmartObjects(instance.Guid).OrderBy(x => x.SystemName))
            {
                Console.WriteLine("  " + item.SystemName + " <= " + item.ServiceObjectName + " [" + string.Join(",", item.MethodNames.ToArray()) + "]");
            }
        }

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < args.Length; index++)
            {
                var token = args[index];
                if (!token.StartsWith("--", StringComparison.Ordinal)) throw new CliException("Unexpected argument: " + token);
                var name = token.Substring(2);
                if (name.Length == 0) throw new CliException("Invalid option: " + token);
                if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    result[name] = args[++index];
                }
                else
                {
                    result[name] = "true";
                }
            }
            return result;
        }

        private static string GetOption(Dictionary<string, string> options, string name, bool required)
        {
            string value;
            if (options.TryGetValue(name, out value)) return value;
            if (required) throw new CliException("Missing required option --" + name + ".");
            return null;
        }

        private static bool HasFlag(Dictionary<string, string> options, string name)
        {
            string value;
            return options.TryGetValue(name, out value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static void RequireConfirmation(Dictionary<string, string> options, string command)
        {
            if (!HasFlag(options, "confirm"))
            {
                throw new CliException("The " + command + " command changes external state. Review 'plan' and rerun with --confirm.");
            }
        }

        private static bool IsHelp(string value)
        {
            return value == "help" || value == "--help" || value == "-h" || value == "/?";
        }

        private static void PrintVersion()
        {
            Console.WriteLine("k2sql 0.1.2");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("k2sql - deploy SQL-backed SmartObjects to K2 Five");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  k2sql doctor  --manifest <path>");
            Console.WriteLine("  k2sql plan    --manifest <path>");
            Console.WriteLine("  k2sql deploy  --manifest <path> --confirm");
            Console.WriteLine("  k2sql verify  --manifest <path>");
            Console.WriteLine("  k2sql inspect --manifest <path>");
            Console.WriteLine("  k2sql cleanup --manifest <path> --confirm [--drop-database]");
            Console.WriteLine("  k2sql version");
            Console.WriteLine();
            Console.WriteLine("Set K2SQL_DEBUG=1 to print exception details. Passwords must be supplied through manifest-named environment variables.");
        }
    }
}
