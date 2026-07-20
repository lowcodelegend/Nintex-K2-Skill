using System;

namespace K2EnvironmentCli
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            RuntimeAssemblyResolver.Install(OptionValue(args, "--install-dir"));
            try { return Cli.Run(args); }
            catch (CliException ex) { Console.Error.WriteLine("ERROR: " + ex.Message); return 2; }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                if (Environment.GetEnvironmentVariable("K2ENV_DEBUG") == "1") Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static string OptionValue(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }

    internal sealed class CliException : Exception
    {
        public CliException(string message) : base(message) { }
    }
}
