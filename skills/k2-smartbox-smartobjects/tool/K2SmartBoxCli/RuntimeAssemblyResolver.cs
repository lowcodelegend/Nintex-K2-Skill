using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace K2SmartBoxCli
{
    internal static class RuntimeAssemblyResolver
    {
        private static bool _installed;
        private static string _installDirectory;
        public static string InstallDirectory { get { return _installDirectory ?? (_installDirectory = FindInstallDirectory()); } }

        public static void Install()
        {
            if (_installed) return;
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
            _installed = true;
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var file = new AssemblyName(args.Name).Name + ".dll";
            foreach (var directory in CandidateDirectories())
            {
                var path = Path.Combine(directory, file);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        }

        private static IEnumerable<string> CandidateDirectories()
        {
            yield return Path.Combine(InstallDirectory, "Bin");
            yield return Path.Combine(InstallDirectory, "ServiceBroker");
            yield return Path.Combine(InstallDirectory, "Host Server", "Bin");
        }

        private static string FindInstallDirectory()
        {
            var explicitPath = Environment.GetEnvironmentVariable("K2_INSTALL_DIR");
            if (!string.IsNullOrWhiteSpace(explicitPath) && Directory.Exists(explicitPath))
                return explicitPath.TrimEnd(Path.DirectorySeparatorChar);
            foreach (var registryPath in new[] {
                @"SOFTWARE\SourceCode\blackpearl\blackpearl Core",
                @"SOFTWARE\WOW6432Node\SourceCode\blackpearl\blackpearl Core" })
            {
                using (var key = Registry.LocalMachine.OpenSubKey(registryPath))
                {
                    var value = key == null ? null : key.GetValue("InstallDir") as string;
                    if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
                        return value.TrimEnd(Path.DirectorySeparatorChar);
                }
            }
            const string fallback = @"C:\Program Files\K2";
            if (Directory.Exists(fallback)) return fallback;
            throw new CliException("K2 installation not found. Set K2_INSTALL_DIR.");
        }
    }
}
