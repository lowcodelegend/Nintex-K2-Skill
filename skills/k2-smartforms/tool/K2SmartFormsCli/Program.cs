using System;

namespace K2SmartFormsCli
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            RuntimeAssemblyResolver.Install();
            // Match the invariant numeric format of K2's SmartObject wire values.
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            try
            {
                return Cli.Run(args);
            }
            catch (CliException ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                if (Environment.GetEnvironmentVariable("K2FORMS_DEBUG") == "1")
                {
                    Console.Error.WriteLine(ex.ToString());
                }
                return 1;
            }
        }
    }
}
