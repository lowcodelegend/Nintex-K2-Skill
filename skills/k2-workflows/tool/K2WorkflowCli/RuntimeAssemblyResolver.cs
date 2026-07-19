using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace K2WorkflowCli
{
    internal static class RuntimeAssemblyResolver
    {
        private static bool _installed;
        private static string _installDirectory;

        public static string InstallDirectory
        {
            get { return _installDirectory ?? (_installDirectory = FindInstallDirectory()); }
        }

        public static string WorkflowDesignerBin
        {
            get { return Path.Combine(InstallDirectory, "K2 smartforms Designer", "K2 workflow Designer", "bin"); }
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
                var candidate = Path.Combine(directory, file);
                if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
            }
            return null;
        }

        private static IEnumerable<string> CandidateDirectories()
        {
            yield return WorkflowDesignerBin;
            yield return Path.Combine(InstallDirectory, "Bin");
            yield return Path.Combine(InstallDirectory, "Host Server", "Bin");
            yield return Path.Combine(InstallDirectory, "K2 smartforms Designer", "bin");
            yield return Path.Combine(InstallDirectory, "K2 smartforms Runtime", "bin");
        }

        private static string FindInstallDirectory()
        {
            var explicitPath = Environment.GetEnvironmentVariable("K2_INSTALL_DIR");
            if (!string.IsNullOrWhiteSpace(explicitPath) && Directory.Exists(explicitPath)) return explicitPath.TrimEnd('\\');
            foreach (var registryPath in new[] { @"SOFTWARE\SourceCode\blackpearl\blackpearl Core", @"SOFTWARE\WOW6432Node\SourceCode\blackpearl\blackpearl Core" })
            using (var key = Registry.LocalMachine.OpenSubKey(registryPath))
            {
                var value = key == null ? null : key.GetValue("InstallDir") as string;
                if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value)) return value.TrimEnd('\\');
            }
            const string fallback = @"C:\Program Files\K2";
            if (Directory.Exists(fallback)) return fallback;
            throw new CliException("K2 installation not found. Set K2_INSTALL_DIR.");
        }
    }
}
