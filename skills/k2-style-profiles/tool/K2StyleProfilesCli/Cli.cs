using System;
using System.Collections.Generic;
using System.Linq;

namespace K2StyleProfilesCli
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
            if (command == "version") { PrintVersion(); return 0; }
            if (command == "selftest") { SelfTests.Run(); return 0; }

            var manifest = StyleProfileManifest.Load(GetOption(options, "manifest", true));
            var manager = new StyleProfileManager(manifest);
            switch (command)
            {
                case "doctor":
                    PrintVersion();
                    Console.WriteLine("K2 install: " + RuntimeAssemblyResolver.InstallDirectory);
                    manager.Doctor();
                    Console.WriteLine("DOCTOR SUCCEEDED: " + manifest.Name);
                    return 0;
                case "plan":
                    manager.Doctor();
                    PrintPlan(manifest, manager.GetState());
                    return 0;
                case "deploy":
                    RequireConfirmation(options, "deploy");
                    manager.Doctor();
                    manager.Deploy();
                    Console.WriteLine("DEPLOYMENT SUCCEEDED: " + manifest.Name);
                    return 0;
                case "verify":
                    manager.Doctor();
                    manager.Verify();
                    Console.WriteLine("VERIFICATION SUCCEEDED: " + manifest.Name);
                    return 0;
                case "inspect":
                    manager.Inspect(HasFlag(options, "definition"));
                    return 0;
                case "cleanup":
                    RequireConfirmation(options, "cleanup");
                    manager.Doctor();
                    manager.Cleanup();
                    if (HasFlag(options, "assets")) manager.CleanupAssets();
                    Console.WriteLine("CLEANUP SUCCEEDED: " + manifest.Name);
                    return 0;
                default:
                    throw new CliException("Unknown command: " + command);
            }
        }

        private static void PrintPlan(StyleProfileManifest manifest, ArtifactState state)
        {
            Console.WriteLine("Plan: " + manifest.Name);
            Console.WriteLine("  Style Profile: " + manifest.StyleProfile.DisplayName + " [" + manifest.StyleProfile.SystemName + "]");
            Console.WriteLine("  Category: " + manifest.StyleProfile.CategoryPath);
            if (!state.Exists) Console.WriteLine("  K2 action: create");
            else if (manifest.StyleProfile.ReplaceExisting)
                Console.WriteLine("  K2 action: update " + state.Guid + " (v" + state.Version + ", checkedOut=" + state.IsCheckedOut.ToString().ToLowerInvariant() + ", consumers=" + state.ConsumerCount + ")");
            else Console.WriteLine("  K2 action: conflict (artifact exists and replacement is disabled)");
            if (manifest.Hosting.Enabled)
            {
                Console.WriteLine("  IIS virtual directory: " + manifest.Hosting.ApplicationPath.TrimEnd('/') + manifest.Hosting.VirtualPath + " -> " + Environment.ExpandEnvironmentVariables(manifest.Hosting.PhysicalPath));
                Console.WriteLine("  Hosting action: copy " + manifest.GetHostedAssets().Count(x => !string.IsNullOrWhiteSpace(x.Source)) + " asset(s)");
            }
            else Console.WriteLine("  Hosting action: none (external URLs)");
            for (var index = 0; index < manifest.StyleProfile.Files.Count; index++)
            {
                var file = manifest.StyleProfile.Files[index];
                Console.WriteLine("  File " + (index + 1) + ": " + file.Type.ToUpperInvariant() + " " + manifest.ResolveUrl(file) +
                    (string.IsNullOrWhiteSpace(file.Source) ? string.Empty : " <= " + manifest.ResolveSource(file)));
            }
            for (var index = 0; index < manifest.Hosting.AdditionalFiles.Count; index++)
            {
                var file = manifest.Hosting.AdditionalFiles[index];
                Console.WriteLine("  Additional file " + (index + 1) + ": " + file.Type.ToUpperInvariant() + " " + manifest.ResolveUrl(file) +
                    (string.IsNullOrWhiteSpace(file.Source) ? string.Empty : " <= " + manifest.ResolveSource(file)));
            }
            Console.WriteLine("  Verification: checked-in metadata + ordered file contract" + (manifest.Verification.VerifyHttp ? " + HTTP bytes" : string.Empty));
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
                    result[name] = args[++index];
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
            return options.TryGetValue(name, out value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static void RequireConfirmation(Dictionary<string, string> options, string command)
        {
            if (!HasFlag(options, "confirm"))
                throw new CliException("The " + command + " command changes IIS/K2 state. Review 'plan' and rerun with --confirm.");
        }

        private static bool IsHelp(string value)
        {
            return value == "help" || value == "--help" || value == "-h" || value == "/?";
        }

        private static void PrintVersion() { Console.WriteLine("k2style 0.3.0"); }

        private static void PrintHelp()
        {
            Console.WriteLine("k2style - host custom CSS/JS and manage K2 Style Profiles");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  k2style doctor  --manifest <path>");
            Console.WriteLine("  k2style plan    --manifest <path>");
            Console.WriteLine("  k2style deploy  --manifest <path> --confirm");
            Console.WriteLine("  k2style verify  --manifest <path>");
            Console.WriteLine("  k2style inspect --manifest <path> [--definition]");
            Console.WriteLine("  k2style cleanup --manifest <path> --confirm [--assets]");
            Console.WriteLine("  k2style version");
            Console.WriteLine("  k2style selftest");
        }
    }
}
