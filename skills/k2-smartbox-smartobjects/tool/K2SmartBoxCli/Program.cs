using System;

namespace K2SmartBoxCli
{
    internal sealed class CliException : Exception
    {
        public CliException(string message) : base(message) { }
    }

    internal static class Program
    {
        public static int Main(string[] args)
        {
            RuntimeAssemblyResolver.Install();
            // K2 numeric wire values use '.' regardless of the Windows locale.
            // The SDK reader otherwise parses them with CurrentCulture (e.g.
            // en-DE rejects 6.25), including values in unselected List columns.
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            try { return Cli.Run(args); }
            catch (CliException ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                if (Environment.GetEnvironmentVariable("K2SMARTBOX_DEBUG") == "1")
                    Console.Error.WriteLine(ex);
                return 1;
            }
        }
    }
}
