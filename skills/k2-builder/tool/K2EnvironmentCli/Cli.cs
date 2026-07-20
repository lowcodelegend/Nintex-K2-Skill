using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace K2EnvironmentCli
{
    internal static class Cli
    {
        public const string Version = "0.1.0";

        public static int Run(string[] args)
        {
            if (args.Length == 0 || IsHelp(args[0])) { Help(); return 0; }
            var command = args[0].ToLowerInvariant();
            if (command == "version") { Console.WriteLine("k2env " + Version); return 0; }
            var options = Options.Parse(args.Skip(1).ToArray());
            var store = new ProfileStore(options.Get("root"));
            if (command == "list") return List(store, options);
            if (command == "discover") return Discover(store, options, false);
            if (command == "refresh") return Discover(store, options, true);
            if (command == "show") return Show(store, options);
            if (command == "validate") return Validate(store, options);
            if (command == "set-default") return SetDefault(store, options);
            throw new CliException("Unknown command: " + command);
        }

        private static int Discover(ProfileStore store, Options options, bool overwrite)
        {
            var name = options.Require("name");
            if (!overwrite && File.Exists(store.ProfilePath(name)))
                throw new CliException("Environment profile already exists: " + store.ProfilePath(name) + ". Use refresh to replace it.");
            var profile = Discovery.Discover(name, options.Get("install-dir"), options.Get("host"), options.Get("base-url"));
            var validation = Validator.Validate(profile, store.ProfilePath(profile.Name));
            profile.LastValidatedUtc = validation.Valid ? validation.ValidatedUtc : null;
            if (options.IsJson) Console.WriteLine(PrettyJson.Serialize(new { profile = profile, validation = validation, persisted = validation.Valid }));
            else { WriteProfile(profile, store.ProfilePath(profile.Name), false); WriteValidation(validation, false); }
            if (!validation.Valid)
            {
                if (!options.IsJson) Console.WriteLine("Profile was not written because validation failed.");
                return 1;
            }
            store.Write(profile, overwrite);
            if (options.Has("default") || store.ReadIndex() == null) store.SetDefault(profile.Name);
            return 0;
        }

        private static int Show(ProfileStore store, Options options)
        {
            var name = store.ResolveName(options.Get("name"));
            WriteProfile(store.Read(name), store.ProfilePath(name), options.IsJson);
            return 0;
        }

        private static int Validate(ProfileStore store, Options options)
        {
            var name = store.ResolveName(options.Get("name"));
            var profile = store.Read(name);
            var result = Validator.Validate(profile, store.ProfilePath(name));
            if (result.Valid)
            {
                profile.LastValidatedUtc = result.ValidatedUtc;
                store.Write(profile, true);
            }
            WriteValidation(result, options.IsJson);
            return result.Valid ? 0 : 1;
        }

        private static int List(ProfileStore store, Options options)
        {
            var index = store.ReadIndex();
            var names = Directory.Exists(store.EnvironmentsRoot)
                ? Directory.GetFiles(store.EnvironmentsRoot, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x).ToArray()
                : new string[0];
            if (options.IsJson)
                Console.WriteLine(PrettyJson.Serialize(new { root = store.Root, defaultEnvironment = index == null ? null : index.DefaultEnvironment, environments = names }));
            else
            {
                Console.WriteLine("Profile root: " + store.Root);
                Console.WriteLine("Default: " + (index == null || string.IsNullOrWhiteSpace(index.DefaultEnvironment) ? "(none)" : index.DefaultEnvironment));
                if (names.Length == 0) Console.WriteLine("No K2 environment profiles found.");
                else foreach (var name in names) Console.WriteLine((index != null && name == index.DefaultEnvironment ? "* " : "  ") + name);
            }
            return 0;
        }

        private static int SetDefault(ProfileStore store, Options options)
        {
            var name = options.Require("name"); store.SetDefault(name);
            Console.WriteLine("Default K2 environment: " + name); return 0;
        }

        private static void WriteProfile(EnvironmentProfile profile, string path, bool json)
        {
            if (json) { Console.WriteLine(PrettyJson.Serialize(profile)); return; }
            Console.WriteLine("K2 environment: " + profile.Name);
            Console.WriteLine("Profile: " + path);
            Console.WriteLine("K2: " + profile.K2.Host + ":" + profile.K2.ManagementPort + " (integrated, label=" + profile.K2.SecurityLabel + ")");
            Console.WriteLine("Version: " + profile.K2.Version);
            Console.WriteLine("Install: " + profile.K2.InstallDirectory);
            Console.WriteLine("Designer: " + profile.Urls.Designer);
            Console.WriteLine("Runtime: " + profile.Urls.Runtime);
        }

        private static void WriteValidation(ValidationResult result, bool json)
        {
            if (json) { Console.WriteLine(PrettyJson.Serialize(result)); return; }
            Console.WriteLine("Validation: " + (result.Valid ? "passed" : "failed"));
            foreach (var check in result.Checks) Console.WriteLine("  " + check.Status.ToUpperInvariant() + " " + check.Name + ": " + check.Message);
        }

        private static bool IsHelp(string value) { return value == "help" || value == "--help" || value == "-h" || value == "/?"; }
        private static void Help()
        {
            Console.WriteLine("k2env " + Version + " - durable K2 Five environment profiles");
            Console.WriteLine("Commands:");
            Console.WriteLine("  discover --name NAME [--default] [--install-dir PATH] [--host HOST] [--base-url URL]");
            Console.WriteLine("  refresh  --name NAME [--install-dir PATH] [--host HOST] [--base-url URL]");
            Console.WriteLine("  show [--name NAME] [--output json]");
            Console.WriteLine("  validate [--name NAME] [--output json]");
            Console.WriteLine("  list [--output json]");
            Console.WriteLine("  set-default --name NAME");
            Console.WriteLine("Common: --root PATH overrides the default %CODEX_HOME%\\k2 store.");
        }
    }

    internal sealed class Options
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _switches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool IsJson { get { return string.Equals(Get("output"), "json", StringComparison.OrdinalIgnoreCase); } }
        public string Get(string name) { string value; return _values.TryGetValue(name, out value) ? value : null; }
        public bool Has(string name) { return _switches.Contains(name); }
        public string Require(string name) { var value = Get(name); if (string.IsNullOrWhiteSpace(value)) throw new CliException("--" + name + " is required."); return value; }

        public static Options Parse(string[] args)
        {
            var result = new Options();
            for (var i = 0; i < args.Length; i++)
            {
                var token = args[i];
                if (!token.StartsWith("--")) throw new CliException("Unexpected argument: " + token);
                var name = token.Substring(2);
                if (name == "default") { result._switches.Add(name); continue; }
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--")) throw new CliException("Missing value for --" + name + ".");
                result._values[name] = args[++i];
            }
            var output = result.Get("output");
            if (output != null && output != "text" && output != "json") throw new CliException("--output must be text or json.");
            return result;
        }
    }
}
