using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartBoxCli
{
    internal static class Cli
    {
        public static int Run(string[] args)
        {
            if (args == null || args.Length == 0 || IsHelp(args[0])) { PrintHelp(); return 0; }
            var command = args[0].ToLowerInvariant();
            if (command == "version") { Console.WriteLine("k2smartbox 0.1.1"); return 0; }
            if (command == "selftest") { SelfTest(); return 0; }
            var options = ParseOptions(args.Skip(1).ToArray());
            var manifest = DeploymentManifest.Load(GetOption(options, "manifest", true));
            var manager = new K2Manager(manifest);
            switch (command)
            {
                case "doctor": manager.CheckConnection(); return 0;
                case "plan": manager.PrintPlan(); return 0;
                case "deploy":
                    Confirm(options, "deploy");
                    manager.Deploy();
                    Console.WriteLine("DEPLOYMENT SUCCEEDED: " + manifest.Name);
                    return 0;
                case "verify":
                    manager.Verify();
                    Console.WriteLine("VERIFICATION SUCCEEDED: " + manifest.Name);
                    return 0;
                case "inspect": manager.Inspect(); return 0;
                case "cleanup":
                    Confirm(options, "cleanup");
                    manager.Cleanup(HasFlag(options, "delete-root-category"));
                    Console.WriteLine("CLEANUP SUCCEEDED: " + manifest.Name);
                    return 0;
                default: throw new CliException("Unknown command: " + command);
            }
        }

        private static void SelfTest()
        {
            var definition = new SmartObjectDefinition
            {
                SystemName = "ABC_Request", DisplayName = "ABC.Request",
                Properties = new List<PropertyDefinition>
                {
                    new PropertyDefinition { Name = "Id", DisplayName = "ID", Type = "autonumber", Key = true },
                    new PropertyDefinition { Name = "Title", DisplayName = "Title", Type = "text", Required = true, MaxLength = 200 }
                }
            };
            var xml = SmartBoxDefinitionBuilder.Build(definition, Guid.NewGuid(), Guid.NewGuid(),
                "SmartBoxService", "ABC_Request_12345678", Guid.NewGuid());
            var root = XDocument.Parse(xml).Root;
            if (root.Element("properties").Elements("property").Count() != 2)
                throw new CliException("Self-test property generation failed.");
            var methods = root.Element("methods").Elements("method").Select(x => (string)x.Attribute("name")).ToArray();
            if (!new[] { "Create", "Save", "Delete", "Load", "GetList" }.SequenceEqual(methods))
                throw new CliException("Self-test method generation failed.");
            Console.WriteLine("SELFTEST SUCCEEDED");
        }

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < args.Length; i++)
            {
                var token = args[i];
                if (!token.StartsWith("--")) throw new CliException("Unexpected argument: " + token);
                var name = token.Substring(2);
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) result[name] = args[++i];
                else result[name] = "true";
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
            return options.TryGetValue(name, out value) && value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        private static void Confirm(Dictionary<string, string> options, string command)
        {
            if (!HasFlag(options, "confirm"))
                throw new CliException(command + " changes K2 state. Review plan and rerun with --confirm.");
        }
        private static bool IsHelp(string value) { return value == "help" || value == "--help" || value == "-h" || value == "/?"; }
        private static void PrintHelp()
        {
            Console.WriteLine("k2smartbox - deploy native SmartBox-backed SmartObjects to K2 Five");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  k2smartbox doctor  --manifest <path>");
            Console.WriteLine("  k2smartbox plan    --manifest <path>");
            Console.WriteLine("  k2smartbox deploy  --manifest <path> --confirm");
            Console.WriteLine("  k2smartbox verify  --manifest <path>");
            Console.WriteLine("  k2smartbox inspect --manifest <path>");
            Console.WriteLine("  k2smartbox cleanup --manifest <path> --confirm [--delete-root-category]");
            Console.WriteLine("  k2smartbox version");
        }
    }
}
