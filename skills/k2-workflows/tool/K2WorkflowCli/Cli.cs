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
            if (command == "version") { Console.WriteLine("k2wf 0.1.1"); return 0; }
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
                        return 0;
                    case "deploy": RequireConfirm(args); manager.Deploy(); return 0;
                    case "inspect": manager.Inspect(); return 0;
                    case "verify": manager.Verify(); return 0;
                    case "unlock": RequireConfirm(args); manager.Unlock(); return 0;
                    case "cleanup": RequireConfirm(args); manager.Cleanup(Has(args, "--delete-deployed")); return 0;
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
        private static void Help()
        {
            Console.WriteLine("k2wf 0.1.1 - K2 Five HTML5 Workflow Designer JSON CLI");
            Console.WriteLine("Commands:");
            Console.WriteLine("  doctor");
            Console.WriteLine("  plan <manifest.json>");
            Console.WriteLine("  render <manifest.json> --output <workflow.json>");
            Console.WriteLine("  deploy <manifest.json> --confirm");
            Console.WriteLine("  inspect <manifest.json>");
            Console.WriteLine("  verify <manifest.json>");
            Console.WriteLine("  unlock <manifest.json> --confirm");
            Console.WriteLine("  cleanup <manifest.json> --confirm [--delete-deployed]");
            Console.WriteLine("  version");
        }
    }
}
