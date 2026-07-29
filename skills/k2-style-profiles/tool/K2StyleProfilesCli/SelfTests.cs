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
            CssValidationContract.Validate(
                ".journey input { border:1px solid #ddd !important; background:#fff !important; }" +
                ".journey input.invalid { border-color:#c00 !important; background:#fee !important; }",
                "good.css");
            AssertThrows(delegate
            {
                CssValidationContract.Validate(
                    ".journey input { border:1px solid #ddd !important; background:#fff !important; }" +
                    ".journey .invalid { color:#c00 !important; }",
                    "bad.css");
            }, "no later .invalid");
            AssertThrows(delegate
            {
                CssValidationContract.Validate(
                    ".journey .card input { border:1px solid #ddd; }" +
                    "input.invalid { border-color:#c00; }",
                    "weak-invalid.css");
            }, "equal or greater specificity");
            CssValidationContract.Validate(
                "@media (prefers-reduced-motion: reduce) {" +
                ".journey-transition, .journey-transition::before {" +
                "transition-duration:1ms !important;" +
                "animation-duration:1ms !important; }}",
                "scoped-motion.css");
            AssertThrows(delegate
            {
                CssValidationContract.Validate(
                    "@media (prefers-reduced-motion: reduce) {" +
                    "html:not(.designer) * {" +
                    "transition-duration:.01ms !important; }}",
                    "broad-transition.css");
            }, "universal selector");
            AssertThrows(delegate
            {
                CssValidationContract.Validate(
                    "@media (prefers-reduced-motion: reduce) {" +
                    "*::before, *::after {" +
                    "animation-duration:1ms !important; }}",
                    "broad-animation.css");
            }, "universal selector");

            var nonIntegrated = new K2ConnectionOptions
            {
                Integrated = false,
                SecurityLabel = "K2SQL",
                UserName = "K2Admin"
            };
            Assert(
                StyleProfileManager.DescribeAuthorContext(
                    nonIntegrated,
                    @"NINTEX\Administrator")
                == "K2SQL:K2Admin",
                "Non-integrated author context");
            var integrated = new K2ConnectionOptions
            {
                Integrated = true,
                SecurityLabel = "K2"
            };
            Assert(
                StyleProfileManager.DescribeAuthorContext(
                    integrated,
                    @"NINTEX\Administrator")
                == @"K2:NINTEX\Administrator",
                "Integrated author context");

            var deploymentProfile = new StyleProfile(
                "K2Skills_AuthenticatedAuthor",
                "Authenticated authoring self-test");
            deploymentProfile.DisplayName = "K2 Skills Authenticated Author";
            var session = new RecordingAuthoringSession(
                deploymentProfile.ToXml());
            var loaded = AuthenticatedStyleProfileGateway.Load(
                session,
                deploymentProfile.Guid);
            var deployedGuid =
                AuthenticatedStyleProfileGateway.DeployAndCheckIn(
                    session,
                    deploymentProfile,
                    "K2 Skills\\Tests");
            Assert(
                loaded == deploymentProfile.ToXml(),
                "Authenticated definition load");
            Assert(
                deployedGuid == deploymentProfile.Guid,
                "Authenticated deployment GUID");
            Assert(
                session.Calls.SequenceEqual(
                    new[] { "load", "deploy:false", "check-in" }),
                "Single authenticated load/deploy/check-in session");
            Assert(
                session.DeployedDefinition == deploymentProfile.ToXml(),
                "Authenticated deployment XML");
            AuthenticatedStyleProfileGateway.AssertInstalledContract();
            Console.WriteLine("SELFTEST SUCCEEDED");
        }

        private sealed class RecordingAuthoringSession :
            IStyleProfileAuthoringSession
        {
            private readonly string _definition;

            public RecordingAuthoringSession(string definition)
            {
                _definition = definition;
                Calls = new List<string>();
            }

            public List<string> Calls { get; private set; }
            public string DeployedDefinition { get; private set; }

            public string Load(Guid guid)
            {
                Calls.Add("load");
                return _definition;
            }

            public void Deploy(
                string definitionXml,
                string categoryPath,
                bool checkIn)
            {
                Calls.Add("deploy:" + checkIn.ToString().ToLowerInvariant());
                DeployedDefinition = definitionXml;
            }

            public void CheckIn(Guid guid)
            {
                Calls.Add("check-in");
            }
        }

        private static void AssertThrows(Action action, string messagePart)
        {
            try { action(); }
            catch (CliException ex)
            {
                if (ex.Message.IndexOf(messagePart, StringComparison.OrdinalIgnoreCase) >= 0) return;
                throw new CliException("Self-test expected error containing '" + messagePart +
                    "' but received: " + ex.Message);
            }
            throw new CliException("Self-test expected an error containing '" + messagePart + "'.");
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new CliException("Self-test failed: " + name);
        }
    }
}
