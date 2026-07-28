using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace K2CaseOperationsCli
{
    internal static class RuntimeAssemblyResolver
    {
        private static string _installDirectory;

        public static void Install()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var simpleName = new AssemblyName(args.Name).Name + ".dll";
            foreach (var directory in CandidateDirectories())
            {
                var candidate = Path.Combine(directory, simpleName);
                if (File.Exists(candidate))
                {
                    return Assembly.LoadFrom(candidate);
                }
            }
            return null;
        }

        private static IEnumerable<string> CandidateDirectories()
        {
            var root = InstallDirectory();
            yield return Path.Combine(root, "Bin");
            yield return Path.Combine(root, "ServiceBroker");
            yield return Path.Combine(root, "Host Server", "Bin");
        }

        private static string InstallDirectory()
        {
            if (_installDirectory != null)
            {
                return _installDirectory;
            }
            var configured = Environment.GetEnvironmentVariable("K2_INSTALL_DIR");
            _installDirectory = string.IsNullOrWhiteSpace(configured)
                ? @"C:\Program Files\K2"
                : configured.TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(_installDirectory))
            {
                throw new InvalidOperationException(
                    "K2 installation was not found. Set K2_INSTALL_DIR.");
            }
            return _installDirectory;
        }
    }
}
