using System;
using System.Collections.Generic;
using System.IO;

namespace K2WebComponentCli
{
    internal sealed class CliException : Exception
    {
        public CliException(string message) : base(message) { }
    }

    internal sealed class Options
    {
        public string Command;
        public string Host = "localhost";
        public int Port = 5555;
        public bool Integrated = true;
        public string SecurityLabel = "K2";
        public string Domain;
        public string UserName;
        public string PasswordEnvironmentVariable;
        public string Package;
        public string Tag;
        public bool Confirm;
    }

    internal static class Program
    {
        private const string Version = "0.1.0";

        public static int Main(string[] args)
        {
            RuntimeAssemblyResolver.Install();
            try
            {
                var options = Parse(args);
                if (options.Command == "version") { Console.WriteLine("k2controls " + Version); return 0; }
                if (options.Command == "help") { PrintHelp(); return 0; }
                var manager = new ControlManager(options);
                switch (options.Command)
                {
                    case "doctor": manager.Doctor(); break;
                    case "list": manager.List(); break;
                    case "deploy":
                        Require(options.Package, "--package");
                        Require(options.Tag, "--tag");
                        if (!options.Confirm) throw new CliException("deploy changes K2 registration; pass --confirm.");
                        manager.Deploy(Path.GetFullPath(options.Package), options.Tag);
                        break;
                    case "verify": Require(options.Tag, "--tag"); manager.Verify(options.Tag); break;
                    case "cleanup":
                        Require(options.Tag, "--tag");
                        if (!options.Confirm) throw new CliException("cleanup deletes a registered control; pass --confirm.");
                        manager.Cleanup(options.Tag);
                        break;
                    default: throw new CliException("Unknown command '" + options.Command + "'. Run k2controls help.");
                }
                return 0;
            }
            catch (CliException ex) { Console.Error.WriteLine("ERROR: " + ex.Message); return 2; }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                if (Environment.GetEnvironmentVariable("K2CONTROLS_DEBUG") == "1") Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static Options Parse(string[] args)
        {
            var result = new Options();
            result.Command = args.Length == 0 ? "help" : args[0].Trim().ToLowerInvariant();
            var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--integrated", "--confirm" };
            for (var i = 1; i < args.Length; i++)
            {
                var key = args[i];
                if (flags.Contains(key))
                {
                    if (key == "--integrated") result.Integrated = true;
                    if (key == "--confirm") result.Confirm = true;
                    continue;
                }
                if (!key.StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
                    throw new CliException("Expected --name value, received '" + key + "'.");
                var value = args[++i];
                switch (key.ToLowerInvariant())
                {
                    case "--host": result.Host = value; break;
                    case "--port": int port; if (!int.TryParse(value, out port)) throw new CliException("--port must be an integer."); result.Port = port; break;
                    case "--security-label": result.SecurityLabel = value; break;
                    case "--domain": result.Domain = value; break;
                    case "--user": result.UserName = value; result.Integrated = false; break;
                    case "--password-env": result.PasswordEnvironmentVariable = value; result.Integrated = false; break;
                    case "--package": result.Package = value; break;
                    case "--tag": result.Tag = value; break;
                    default: throw new CliException("Unknown option '" + key + "'.");
                }
            }
            if (result.Port < 1 || result.Port > 65535) throw new CliException("--port must be between 1 and 65535.");
            return result;
        }

        private static void Require(string value, string option)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new CliException("Specify " + option + ".");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("k2controls " + Version);
            Console.WriteLine("Modern K2 5.9+ Web Component control registration.");
            Console.WriteLine("Commands: doctor | list | deploy --package control.zip --tag element-name --confirm | verify --tag element-name | cleanup --tag element-name --confirm");
            Console.WriteLine("Connection: --host localhost --port 5555 --integrated");
            Console.WriteLine("Non-integrated: --security-label K2 --domain DOMAIN --user name --password-env ENV_NAME");
        }
    }
}
