using System;
using System.Collections.Generic;
using System.Linq;

namespace K2SmartFormsCli
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

            var manifest = SmartFormsManifest.Load(GetOption(options, "manifest", true));
            var manager = new SmartFormsManager(manifest);

            switch (command)
            {
                case "doctor":
                    PrintVersion();
                    Console.WriteLine("K2 install: " + RuntimeAssemblyResolver.InstallDirectory);
                    manager.CheckConnectionAndInputs();
                    Console.WriteLine("DOCTOR SUCCEEDED: " + manifest.Name);
                    return 0;

                case "plan":
                    PrintPlan(manifest, manager);
                    return 0;

                case "deploy":
                    RequireConfirmation(options, "deploy");
                    manager.Deploy();
                    manager.Verify();
                    Console.WriteLine("DEPLOYMENT SUCCEEDED: " + manifest.Name);
                    return 0;

                case "verify":
                    manager.CheckConnectionAndInputs();
                    manager.Verify();
                    Console.WriteLine("VERIFICATION SUCCEEDED: " + manifest.Name);
                    return 0;

                case "inspect":
                    Inspect(manager);
                    return 0;

                case "cleanup":
                    RequireConfirmation(options, "cleanup");
                    manager.Cleanup();
                    Console.WriteLine("CLEANUP SUCCEEDED: " + manifest.Name);
                    return 0;

                default:
                    throw new CliException("Unknown command: " + command);
            }
        }

        private static void PrintPlan(SmartFormsManifest manifest, SmartFormsManager manager)
        {
            manager.CheckConnectionAndInputs();
            var states = manager.GetArtifactStates();
            var dependencies = manager.GetExternalDependencies();

            Console.WriteLine("Plan: " + manifest.Name);
            Console.WriteLine("  Application category: " + manifest.Application.RootCategoryPath);
            Console.WriteLine("  Views category: " + manifest.Application.ViewsCategoryPath);
            Console.WriteLine("  Forms category: " + manifest.Application.FormsCategoryPath);
            Console.WriteLine("  Theme: " + manifest.Application.Theme);
            foreach (var state in states)
            {
                string action;
                if (!state.Exists) action = "create";
                else if (manifest.Application.ReplaceExisting) action = "replace (" + state.Guid + ", v" + state.Version + ")";
                else action = "conflict: exists and replacement is disabled (" + state.Guid + ", v" + state.Version + ")";
                Console.WriteLine("  " + state.Kind + ": " + action + " " + state.Name);
            }
            foreach (var view in manifest.Application.Views)
            {
                Console.WriteLine("    " + view.Type + " <= " + view.SmartObject + " [" + string.Join(",", view.Properties.ToArray()) + "]");
            }
            foreach (var form in manifest.Application.Forms)
            {
                Console.WriteLine("    form views: " + form.Name + " <= [" + string.Join(", ", form.Views.ToArray()) + "], legacyTheme=" + form.UseLegacyTheme.ToString().ToLowerInvariant());
            }
            if (dependencies.Count > 0)
            {
                foreach (var dependency in dependencies)
                    Console.WriteLine("  BLOCKED external dependency: " + dependency.Key + " -> " + string.Join(", ", dependency.Value.ToArray()));
            }
            Console.WriteLine("  Verify: " + manifest.Verification.ExpectedViews.Count + " view(s), " + manifest.Verification.ExpectedForms.Count + " form(s), runtime=" + manifest.Verification.SmokeTestRuntime);
        }

        private static void Inspect(SmartFormsManager manager)
        {
            foreach (var state in manager.GetArtifactStates().OrderBy(x => x.Kind).ThenBy(x => x.Name))
            {
                if (!state.Exists)
                {
                    Console.WriteLine(state.Kind + ": absent (" + state.Name + ")");
                    continue;
                }
                var themeMode = state.Kind == "Form" ? ", legacyTheme=" + (state.UseLegacyTheme.HasValue ? state.UseLegacyTheme.Value.ToString().ToLowerInvariant() : "<unset>") : string.Empty;
                Console.WriteLine(state.Kind + ": " + state.Name + " (" + state.Guid + ", v" + state.Version + ", " + state.Type + ", category " + state.CategoryPath + ", checkedOut=" + state.CheckedOut + themeMode + ")");
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
                    result[name] = args[++index];
                else
                    result[name] = "true";
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
                throw new CliException("The " + command + " command changes K2 state. Review 'plan' and rerun with --confirm.");
        }

        private static bool IsHelp(string value)
        {
            return value == "help" || value == "--help" || value == "-h" || value == "/?";
        }

        private static void PrintVersion()
        {
            Console.WriteLine("k2forms 0.1.2");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("k2forms - generate CRUD SmartForms over K2 SmartObjects");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  k2forms doctor  --manifest <path>");
            Console.WriteLine("  k2forms plan    --manifest <path>");
            Console.WriteLine("  k2forms deploy  --manifest <path> --confirm");
            Console.WriteLine("  k2forms verify  --manifest <path>");
            Console.WriteLine("  k2forms inspect --manifest <path>");
            Console.WriteLine("  k2forms cleanup --manifest <path> --confirm");
            Console.WriteLine("  k2forms version");
            Console.WriteLine();
            Console.WriteLine("Set K2FORMS_DEBUG=1 for exception details. Passwords must be supplied through manifest-named environment variables.");
        }
    }
}
