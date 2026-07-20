using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace K2EnvironmentCli
{
    internal static class RuntimeAssemblyResolver
    {
        private static bool _installed;
        private static string _installOverride;

        public static void Install(string installOverride)
        {
            if (_installed) return;
            _installOverride = installOverride;
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
            _installed = true;
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var fileName = new AssemblyName(args.Name).Name + ".dll";
            foreach (var directory in CandidateDirectories())
            {
                var path = Path.Combine(directory, fileName);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        }

        private static IEnumerable<string> CandidateDirectories()
        {
            var root = FindInstallDirectory();
            yield return Path.Combine(root, "Bin");
            yield return Path.Combine(root, "Host Server", "Bin");
            yield return Path.Combine(root, "K2 smartforms Designer", "bin");
            yield return Path.Combine(root, "K2 smartforms Runtime", "bin");
        }

        private static string FindInstallDirectory()
        {
            if (!string.IsNullOrWhiteSpace(_installOverride) && Directory.Exists(_installOverride)) return _installOverride;
            var configured = Environment.GetEnvironmentVariable("K2_INSTALL_DIR");
            if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured)) return configured;
            foreach (var keyName in new[] { @"SOFTWARE\SourceCode\blackpearl\blackpearl Core", @"SOFTWARE\WOW6432Node\SourceCode\blackpearl\blackpearl Core" })
            using (var key = Registry.LocalMachine.OpenSubKey(keyName))
            {
                var value = key == null ? null : Convert.ToString(key.GetValue("InstallDir"));
                if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value)) return value;
            }
            return @"C:\Program Files\K2";
        }
    }
}
