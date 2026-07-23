using System;
using System.Collections.Generic;
using System.Linq;
using SourceCode.Forms.Authoring;

namespace K2StyleProfilesCli
{
    internal static class SelfTests
    {
        public static void Run()
        {
            var profile = new StyleProfile("K2Skills_Test", "Self-test");
            profile.DisplayName = "K2 Skills Test";
            profile.Files.Add(new CustomFile { Type = CustomFileType.Css, Url = Uri.EscapeDataString("https://example.invalid/style.css") });
            profile.Files.Add(new CustomFile { Type = CustomFileType.Js, Url = Uri.EscapeDataString("https://example.invalid/style.js") });
            var roundTrip = new StyleProfile(profile.ToJson(), true);
            var files = roundTrip.Files.Cast<CustomFile>().ToList();
            Assert(files.Count == 2, "Style Profile file count");
            Assert(files[0].Type == CustomFileType.Css && files[1].Type == CustomFileType.Js, "Style Profile file order");
            Assert(Uri.UnescapeDataString(files[0].Url) == "https://example.invalid/style.css", "Style Profile URL encoding");

            var manifest = new StyleProfileManifest
            {
                Name = "Sidebar assets",
                StyleProfile = new StyleProfileOptions
                {
                    SystemName = "APP Sidebar",
                    DisplayName = "APP Sidebar",
                    CategoryPath = "APP\\UX",
                    Files = new List<StyleFileOptions>
                    {
                        new StyleFileOptions { Type = "css", Source = "critical.css", Target = "critical.css" },
                        new StyleFileOptions { Type = "js", Source = "boot.js", Target = "boot.js" }
                    }
                },
                Hosting = new HostingOptions
                {
                    Enabled = true,
                    SiteName = "K2",
                    ApplicationPath = "K2/",
                    VirtualPath = "/APPAssets",
                    PhysicalPath = @"C:\inetpub\app-assets",
                    BaseUrl = "https://example.invalid/APPAssets",
                    AdditionalFiles = new List<StyleFileOptions>
                    {
                        new StyleFileOptions { Type = "css", Source = "application.css", Target = "application.css" }
                    }
                }
            };
            manifest.NormalizeAndValidate();
            Assert(manifest.GetHostedAssets().Count() == 3, "Additional hosted asset contract");
            Assert(manifest.StyleProfile.Files.Count == 2, "Additional hosted asset excluded from K2 file order");
            Console.WriteLine("SELFTEST SUCCEEDED");
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new CliException("Self-test failed: " + name);
        }
    }
}
