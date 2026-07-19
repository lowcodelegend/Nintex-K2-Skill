using System;

namespace K2WorkflowCli
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            RuntimeAssemblyResolver.Install();
            try { return Cli.Run(args); }
            catch (CliException ex) { Console.Error.WriteLine("ERROR: " + ex.Message); return 2; }
            catch (Exception ex) { Console.Error.WriteLine("ERROR: " + ex.GetBaseException().Message); return 1; }
        }
    }

    internal sealed class CliException : Exception
    {
        public CliException(string message) : base(message) { }
    }
}
