using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;

namespace K2StyleProfilesCli
{
    internal sealed class AssetHost
    {
        private readonly StyleProfileManifest _manifest;

        public AssetHost(StyleProfileManifest manifest) { _manifest = manifest; }

        public void CheckInputs()
        {
            foreach (var file in _manifest.GetHostedAssets())
            {
                var source = _manifest.ResolveSource(file);
                if (!string.IsNullOrWhiteSpace(source) && !File.Exists(source))
                    throw new CliException("Style asset source not found: " + source);
                if (_manifest.Verification.RequireDesignerIsolation && !string.IsNullOrWhiteSpace(source))
                    ValidateDesignerIsolation(file, File.ReadAllText(source));
            }
            if (_manifest.Hosting.Enabled && _manifest.Hosting.ConfigureIis && !File.Exists(AppCmdPath))
                throw new CliException("IIS appcmd was not found: " + AppCmdPath);
        }

        public void Deploy()
        {
            if (!_manifest.Hosting.Enabled) return;
            CheckInputs();
            var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(_manifest.Hosting.PhysicalPath));
            Directory.CreateDirectory(root);
            if (_manifest.Hosting.ConfigureIis) EnsureVirtualDirectory(root);
            foreach (var file in _manifest.GetHostedAssets().Where(x => !string.IsNullOrWhiteSpace(x.Source)))
            {
                var destination = ResolveDestination(root, file.Target);
                File.Copy(_manifest.ResolveSource(file), destination, true);
                Console.WriteLine("Hosted " + file.Type.ToUpperInvariant() + ": " + destination);
            }
        }

        public void Verify()
        {
            CheckInputs();
            if (!_manifest.Verification.VerifyHttp) return;
            foreach (var file in _manifest.GetHostedAssets())
            {
                var url = _manifest.ResolveUrl(file);
                byte[] remote;
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Timeout = _manifest.Verification.HttpTimeoutSeconds * 1000;
                    request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var stream = response.GetResponseStream())
                    using (var memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        remote = memory.ToArray();
                        var expectedType = file.Type == "css" ? "css" : "javascript";
                        if ((response.ContentType ?? string.Empty).IndexOf(expectedType, StringComparison.OrdinalIgnoreCase) < 0)
                            throw new CliException("Hosted " + file.Type + " has unexpected content type '" + response.ContentType + "': " + url);
                    }
                }
                catch (WebException ex)
                {
                    var response = ex.Response as HttpWebResponse;
                    var suffix = response == null ? ex.Message : "HTTP " + (int)response.StatusCode;
                    throw new CliException("Hosted asset verification failed (" + suffix + "): " + url);
                }
                var source = _manifest.ResolveSource(file);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    var local = File.ReadAllBytes(source);
                    if (!HashesEqual(local, remote))
                        throw new CliException("Hosted asset bytes do not match source: " + url);
                }
                Console.WriteLine("Asset HTTP verification: OK (" + url + ", " + remote.Length + " bytes)");
            }
        }

        public void CleanupFiles()
        {
            if (!_manifest.Hosting.Enabled) return;
            var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(_manifest.Hosting.PhysicalPath));
            foreach (var file in _manifest.GetHostedAssets().Where(x => !string.IsNullOrWhiteSpace(x.Target)))
            {
                var destination = ResolveDestination(root, file.Target);
                if (File.Exists(destination))
                {
                    File.Delete(destination);
                    Console.WriteLine("Removed hosted asset: " + destination);
                }
            }
        }

        private void EnsureVirtualDirectory(string physicalPath)
        {
            var key = _manifest.Hosting.ApplicationPath.TrimEnd('/') + _manifest.Hosting.VirtualPath;
            var existing = RunAppCmd("list vdir /app.name:\"" + _manifest.Hosting.ApplicationPath + "\" /path:" + _manifest.Hosting.VirtualPath + " /text:physicalPath", true).Trim();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                if (!string.Equals(Path.GetFullPath(Environment.ExpandEnvironmentVariables(existing)), physicalPath, StringComparison.OrdinalIgnoreCase))
                    throw new CliException("IIS virtual directory '" + key + "' already points to a different path: " + existing);
                Console.WriteLine("IIS virtual directory: existing (" + key + ")");
                return;
            }
            RunAppCmd("add vdir /app.name:\"" + _manifest.Hosting.ApplicationPath + "\" /path:" + _manifest.Hosting.VirtualPath + " /physicalPath:\"" + physicalPath + "\"", false);
            Console.WriteLine("IIS virtual directory: created (" + key + " -> " + physicalPath + ")");
        }

        private static string ResolveDestination(string root, string target)
        {
            var destination = Path.GetFullPath(Path.Combine(root, target));
            var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new CliException("Hosted target escapes hosting.physicalPath: " + target);
            return destination;
        }

        private static void ValidateDesignerIsolation(StyleFileOptions file, string content)
        {
            if (file.Type == "css")
            {
                if (content.IndexOf("html:not(.designer)", StringComparison.OrdinalIgnoreCase) < 0 &&
                    content.IndexOf("html:not([data-designer", StringComparison.OrdinalIgnoreCase) < 0 &&
                    content.IndexOf("k2style: designer-safe", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new CliException("CSS must be runtime-scoped (for example html:not(.designer)) or carry a reviewed 'k2style: designer-safe' marker: " + file.Source);
            }
            else if (content.IndexOf("k2style: designer-guard", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new CliException("JavaScript must carry the 'k2style: designer-guard' marker beside its early Designer-mode return: " + file.Source);
            }
        }

        private static bool HashesEqual(byte[] left, byte[] right)
        {
            using (var hash = SHA256.Create())
                return hash.ComputeHash(left).SequenceEqual(hash.ComputeHash(right));
        }

        private static string AppCmdPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\inetsrv\appcmd.exe"); }
        }

        private static string RunAppCmd(string arguments, bool allowNotFound)
        {
            var start = new ProcessStartInfo
            {
                FileName = AppCmdPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(start))
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0 && !allowNotFound)
                    throw new CliException("IIS configuration failed: " + (string.IsNullOrWhiteSpace(error) ? output : error).Trim());
                return output;
            }
        }
    }
}
