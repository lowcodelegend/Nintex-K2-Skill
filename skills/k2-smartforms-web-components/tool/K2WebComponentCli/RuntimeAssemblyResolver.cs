using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace K2WebComponentCli
{
    internal static class RuntimeAssemblyResolver
    {
        private static bool _installed;
        public static void Install()
        {
            if (_installed) return;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            _installed = true;
        }
        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name + ".dll";
            foreach (var directory in Directories())
            {
                var path = Path.Combine(directory, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        }
        private static IEnumerable<string> Directories()
        {
            var root = FindRoot();
            yield return Path.Combine(root, "Bin");
            yield return Path.Combine(root, "Host Server", "Bin");
            yield return Path.Combine(root, "K2 smartforms Designer", "bin");
            yield return Path.Combine(root, "K2 smartforms Runtime", "bin");
        }
        private static string FindRoot()
        {
            var configured = Environment.GetEnvironmentVariable("K2_INSTALL_DIR");
            if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured)) return configured;
            foreach (var keyPath in new[] { @"SOFTWARE\SourceCode\blackpearl\blackpearl Core", @"SOFTWARE\WOW6432Node\SourceCode\blackpearl\blackpearl Core" })
            using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
            {
                var value = key == null ? null : key.GetValue("InstallDir") as string;
                if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value)) return value;
            }
            const string fallback = @"C:\Program Files\K2";
            if (Directory.Exists(fallback)) return fallback;
            throw new CliException("K2 installation not found. Set K2_INSTALL_DIR.");
        }
    }
}
