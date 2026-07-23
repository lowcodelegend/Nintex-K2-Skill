using System;
using System.IO;
using System.Linq;

namespace K2WorkflowCli
{
    internal static class Cli
    {
        public static int Run(string[] args)
        {
            if (args.Length == 0 || Has(args, "--help") || Has(args, "-h")) { Help(); return 0; }
            var command = args[0].ToLowerInvariant();
            if (command == "version") { Console.WriteLine("k2wf 0.13.0"); return 0; }
            if (command == "selftest") { SelfTests.Run(); return 0; }
            if (command == "doctor") { WorkflowManager.Doctor(); return 0; }
            if (args.Length < 2) throw new CliException("A manifest path is required.");
            var manifest = WorkflowManifest.Load(args[1]);
            using (var manager = new WorkflowManager(manifest))
            {
                switch (command)
                {
                    case "plan": manager.Plan(); return 0;
                    case "render":
                        var output = Value(args, "--output");
                        if (string.IsNullOrWhiteSpace(output)) throw new CliException("render requires --output <file>.");
                        var fullPath = Path.GetFullPath(output);
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                        File.WriteAllText(fullPath, manager.Render());
                        Console.WriteLine("Rendered: " + fullPath);
                        manager.ReportTaskAssignmentPolicy();
                        return 0;
                    case "export":
                        var exportOutput = Value(args, "--output");
                        if (string.IsNullOrWhiteSpace(exportOutput)) throw new CliException("export requires --output <file>.");
                        manager.Export(Path.GetFullPath(exportOutput));
                        return 0;
                    case "deploy": RequireConfirm(args); manager.Deploy(); manager.Verify(); return 0;
                    case "start": RequireConfirm(args); manager.Start(ParseData(args)); return 0;
                    case "status": manager.RuntimeStatus(); return 0;
                    case "instance-data":
                        int instanceId;
                        if (!int.TryParse(Value(args, "--id"), out instanceId)) throw new CliException("instance-data requires --id <process-instance-id>.");
                        manager.ReportInstanceData(instanceId); return 0;
                    case "worklist": manager.ReportWorklist(); return 0;
                    case "action":
                        RequireConfirm(args);
                        var serial = Value(args, "--serial"); var action = Value(args, "--action");
                        if (string.IsNullOrWhiteSpace(serial) || string.IsNullOrWhiteSpace(action)) throw new CliException("action requires --serial <serial-number> --action <action-name>.");
                        manager.ExecuteAction(serial, action); return 0;
                    case "inspect": manager.Inspect(); return 0;
                    case "verify": manager.Verify(); return 0;
                    case "unlock": RequireConfirm(args); manager.Unlock(); return 0;
                    case "cleanup": RequireConfirm(args); manager.Cleanup(Has(args, "--delete-deployed"), Has(args, "--defer-smartforms-integration")); return 0;
                    default: throw new CliException("Unknown command: " + command);
                }
            }
        }

        private static bool Has(string[] args, string option) { return args.Any(x => string.Equals(x, option, StringComparison.OrdinalIgnoreCase)); }
        private static string Value(string[] args, string option)
        {
            for (var i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], option, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
        private static void RequireConfirm(string[] args) { if (!Has(args, "--confirm")) throw new CliException("This command changes K2. Re-run with --confirm after reviewing plan."); }
        private static System.Collections.Generic.IDictionary<string, string> ParseData(string[] args)
        {
            var values = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 2; i < args.Length - 1; i++) if (string.Equals(args[i], "--data", StringComparison.OrdinalIgnoreCase))
            {
                var pair = args[++i]; var split = pair.IndexOf('=');
                if (split < 1) throw new CliException("--data requires Name=Value.");
                values[pair.Substring(0, split)] = pair.Substring(split + 1);
            }
            return values;
        }
        private static void Help()
        {
            Console.WriteLine("k2wf 0.13.0 - K2 Five HTML5 Workflow Designer JSON CLI");
            Console.WriteLine("Commands:");
            Console.WriteLine("  doctor");
            Console.WriteLine("  plan <manifest.json>");
            Console.WriteLine("  render <manifest.json> --output <workflow.json>");
            Console.WriteLine("  export <manifest.json> --output <workflow.json>");
            Console.WriteLine("  deploy <manifest.json> --confirm");
            Console.WriteLine("  start <manifest.json> --data Name=Value [--data Name=Value] --confirm");
            Console.WriteLine("  status <manifest.json>");
            Console.WriteLine("  instance-data <manifest.json> --id <process-instance-id>");
            Console.WriteLine("  worklist <manifest.json>");
            Console.WriteLine("  action <manifest.json> --serial <serial-number> --action <action-name> --confirm");
            Console.WriteLine("  inspect <manifest.json>");
            Console.WriteLine("  verify <manifest.json>");
            Console.WriteLine("  unlock <manifest.json> --confirm");
            Console.WriteLine("  cleanup <manifest.json> --confirm [--delete-deployed] [--defer-smartforms-integration]");
            Console.WriteLine("  version");
            Console.WriteLine("  selftest");
        }
    }
}
