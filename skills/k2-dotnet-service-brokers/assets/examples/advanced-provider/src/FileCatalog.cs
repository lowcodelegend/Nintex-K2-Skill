using System;
using System.IO;
using System.Linq;
using A = SourceCode.SmartObjects.Services.ServiceSDK.Attributes;
using SourceCode.SmartObjects.Services.ServiceSDK.Objects;
using SourceCode.SmartObjects.Services.ServiceSDK.Types;

namespace K2Skills.Examples.AdvancedBroker
{
    [A.ServiceObject("FileCatalog", "Bounded File Catalog", "Lists metadata under one configured root without returning file content.")]
    public sealed class FileCatalog
    {
        public ServiceConfiguration ServiceConfiguration { get; set; }

        [A.Property("Pattern", SoType.Text, "Pattern", "Optional simple filename pattern such as *.txt.")]
        public string Pattern { get; set; }
        [A.Property("Name", SoType.Text, "Name", "File name.")]
        public string Name { get; set; }
        [A.Property("RelativePath", SoType.Text, "Relative Path", "Path relative to the configured root.")]
        public string RelativePath { get; set; }
        [A.Property("Length", SoType.Number, "Length", "File length in bytes.")]
        public int Length { get; set; }
        [A.Property("ModifiedUtc", SoType.DateTime, "Modified UTC", "Last modified UTC timestamp.")]
        public DateTime ModifiedUtc { get; set; }

        [A.Method("ListFiles", MethodType.List, "List Files", "List at most 50 files under the configured root.",
            new string[0], new[] { "Pattern" }, new[] { "Name", "RelativePath", "Length", "ModifiedUtc" })]
        public FileCatalog[] ListFiles()
        {
            var root = Path.GetFullPath(Convert.ToString(ServiceConfiguration["AllowedRoot"]));
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException("The configured AllowedRoot does not exist.");
            var pattern = string.IsNullOrWhiteSpace(Pattern) ? "*" : Pattern.Trim();
            if (pattern.Contains("..") || pattern.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
                throw new InvalidOperationException("Pattern must be a simple filename pattern.");
            return Directory.GetFiles(root, pattern, SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Take(50)
                .Select(path => {
                    var file = new FileInfo(path);
                    return new FileCatalog {
                        Name = file.Name,
                        RelativePath = file.Name,
                        Length = file.Length > int.MaxValue ? int.MaxValue : (int)file.Length,
                        ModifiedUtc = file.LastWriteTimeUtc
                    };
                }).ToArray();
        }
    }
}
