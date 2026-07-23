using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace K2StyleProfilesCli
{
    internal static class RuntimeAssemblyResolver
    {
        private static bool _installed;
        private static string _installDirectory;

        public static string InstallDirectory
        {
            get
            {
                if (_installDirectory == null) _installDirectory = FindInstallDirectory();
                return _installDirectory;
            }
        }

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
            var root = InstallDirectory;
            yield return Path.Combine(root, "Bin");
            yield return Path.Combine(root, "Host Server", "Bin");
            yield return Path.Combine(root, "K2 smartforms Designer", "bin");
            yield return Path.Combine(root, "K2 smartforms Runtime", "bin");
        }

        private static string FindInstallDirectory()
        {
            var configured = Environment.GetEnvironmentVariable("K2_INSTALL_DIR");
            if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
                return configured.TrimEnd(Path.DirectorySeparatorChar);

            foreach (var registryPath in new[]
            {
                @"SOFTWARE\SourceCode\blackpearl\blackpearl Core",
                @"SOFTWARE\WOW6432Node\SourceCode\blackpearl\blackpearl Core"
            })
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
            throw new InvalidOperationException("K2 installation not found. Set K2_INSTALL_DIR.");
        }
    }
}
