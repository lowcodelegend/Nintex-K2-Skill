using System;

namespace K2EnvironmentCli
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            try { return Cli.Run(args); }
            catch (CliException ex) { Console.Error.WriteLine("ERROR: " + ex.Message); return 2; }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                if (Environment.GetEnvironmentVariable("K2ENV_DEBUG") == "1") Console.Error.WriteLine(ex);
                return 1;
            }
        }
    }

    internal sealed class CliException : Exception
    {
        public CliException(string message) : base(message) { }
    }
}
