using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace K2EnvironmentCli
{
    internal static class Cli
    {
        public const string Version = "0.7.0";

        public static int Run(string[] args)
        {
            if (args.Length == 0 || IsHelp(args[0])) { Help(); return 0; }
            var command = args[0].ToLowerInvariant();
            if (command == "version") { Console.WriteLine("k2env " + Version); return 0; }
            var options = Options.Parse(args.Skip(1).ToArray());
            var store = new ProfileStore(options.Get("root"));
            if (command == "list") return List(store, options);
            if (command == "discover") return Discover(store, options, false);
            if (command == "refresh") return Discover(store, options, true);
            if (command == "show") return Show(store, options);
            if (command == "validate") return Validate(store, options);
            if (command == "set-default") return SetDefault(store, options);
            if (command == "set-style-profile") return SetStyleProfile(store, options);
            if (command == "inspect-header") return InspectHeader(store, options);
            if (command == "inspect-framework") return InspectHeader(store, options);
            if (command == "set-common-header") return SetCommonHeader(store, options);
            if (command == "check-short-code") return CheckShortCode(store, options);
            if (command == "reserve-short-code") return ReserveShortCode(store, options);
            if (command == "list-short-codes") return ListShortCodes(store, options);
            if (command == "release-short-code") return ReleaseShortCode(store, options);
            throw new CliException("Unknown command: " + command);
        }

        private static int Discover(ProfileStore store, Options options, bool overwrite)
        {
            var name = options.Require("name");
            if (!overwrite && File.Exists(store.ProfilePath(name)))
                throw new CliException("Environment profile already exists: " + store.ProfilePath(name) + ". Use refresh to replace it.");
            EnvironmentProfile previous = null;
            if (overwrite && File.Exists(store.ProfilePath(name))) previous = store.Read(name);
            var profile = Discovery.Discover(name, options.Get("install-dir"), options.Get("host"), options.Get("base-url"));
            PreserveStyleProfileSelection(previous, profile);
            PreserveCommonHeaderSelection(previous, profile);
            PreserveSolutionCodeRegistrations(previous, profile);
            var validation = Validator.Validate(profile, store.ProfilePath(profile.Name));
            profile.LastValidatedUtc = validation.Valid ? validation.ValidatedUtc : null;
            if (options.IsJson) Console.WriteLine(PrettyJson.Serialize(new { profile = profile, validation = validation, persisted = validation.Valid }));
            else { WriteProfile(profile, store.ProfilePath(profile.Name), false); WriteValidation(validation, false); }
            if (!validation.Valid)
            {
                if (!options.IsJson) Console.WriteLine("Profile was not written because validation failed.");
                return 1;
            }
            store.Write(profile, overwrite);
            if (options.Has("default") || store.ReadIndex() == null) store.SetDefault(profile.Name);
            return 0;
        }

        private static int Show(ProfileStore store, Options options)
        {
            var name = store.ResolveName(options.Get("name"));
            WriteProfile(store.Read(name), store.ProfilePath(name), options.IsJson);
            return 0;
        }

        private static int Validate(ProfileStore store, Options options)
        {
            var name = store.ResolveName(options.Get("name"));
            var profile = store.Read(name);
            var result = Validator.Validate(profile, store.ProfilePath(name));
            if (result.Valid)
            {
                profile.LastValidatedUtc = result.ValidatedUtc;
                store.Write(profile, true);
            }
            WriteValidation(result, options.IsJson);
            return result.Valid ? 0 : 1;
        }

        private static int List(ProfileStore store, Options options)
        {
            var index = store.ReadIndex();
            var names = Directory.Exists(store.EnvironmentsRoot)
                ? Directory.GetFiles(store.EnvironmentsRoot, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x).ToArray()
                : new string[0];
            if (options.IsJson)
                Console.WriteLine(PrettyJson.Serialize(new { root = store.Root, defaultEnvironment = index == null ? null : index.DefaultEnvironment, environments = names }));
            else
            {
                Console.WriteLine("Profile root: " + store.Root);
                Console.WriteLine("Default: " + (index == null || string.IsNullOrWhiteSpace(index.DefaultEnvironment) ? "(none)" : index.DefaultEnvironment));
                if (names.Length == 0) Console.WriteLine("No K2 environment profiles found.");
                else foreach (var name in names) Console.WriteLine((index != null && name == index.DefaultEnvironment ? "* " : "  ") + name);
            }
            return 0;
        }

        private static int SetDefault(ProfileStore store, Options options)
        {
            var name = options.Require("name"); store.SetDefault(name);
            Console.WriteLine("Default K2 environment: " + name); return 0;
        }

        private static int CheckShortCode(ProfileStore store, Options options)
        {
            var name = store.ResolveName(options.Get("name"));
            var profile = store.Read(name);
            var code = ShortCodeRegistry.NormalizeCode(options.Require("code"));
            var solution = ShortCodeRegistry.NormalizeSolutionName(code, options.Get("solution"), false);
            var registration = ShortCodeRegistry.Find(profile, code);
            if (registration != null)
            {
                if (ShortCodeRegistry.SameSolution(registration, solution))
                {
                    Console.WriteLine("Short code " + code + " is reserved for this solution: " + registration.SolutionName);
                    return 0;
                }
                throw new CliException("Short code " + code + " is already reserved for '" + registration.SolutionName + "' in environment '" + name + "'.");
            }
            var observed = ShortCodeRegistry.FindObserved(profile, code);
            if (observed != null)
                throw new CliException("Short code " + code + " is already visible on " + observed.ArtifactCount + " K2 Form/View artifact(s) in environment '" + name + "'. Adopt it explicitly only when continuing that existing solution.");
            Console.WriteLine("Short code " + code + " is available in environment '" + name + "'.");
            return 0;
        }

        private static int ReserveShortCode(ProfileStore store, Options options)
        {
            var name = store.ResolveName(options.Get("name"));
            var profile = store.Read(name);
            var code = ShortCodeRegistry.NormalizeCode(options.Require("code"));
            var solution = ShortCodeRegistry.NormalizeSolutionName(code, options.Require("solution"), true);
            var registration = ShortCodeRegistry.Find(profile, code);
            if (registration != null && !ShortCodeRegistry.SameSolution(registration, solution))
                throw new CliException("Short code " + code + " is already reserved for '" + registration.SolutionName + "' in environment '" + name + "'.");
            var observed = ShortCodeRegistry.FindObserved(profile, code);
            if (registration == null && observed != null && !options.Has("adopt-existing"))
                throw new CliException("Short code " + code + " is already visible on " + observed.ArtifactCount + " K2 Form/View artifact(s). Use --adopt-existing only when '" + solution + "' is that existing solution.");
            var now = DateTime.UtcNow.ToString("o");
            if (registration == null)
            {
                registration = new SolutionCodeRegistration { Code = code, SolutionName = solution, RegisteredUtc = now };
                ShortCodeRegistry.Registrations(profile).Add(registration);
            }
            registration.SolutionName = solution;
            registration.RootCategoryPath = options.Get("root-category") ?? registration.RootCategoryPath;
            registration.ManifestPath = options.Get("manifest") == null ? registration.ManifestPath : ShortCodeRegistry.NormalizeOptionalPath(options.Get("manifest"));
            registration.UpdatedUtc = now;
            store.Write(profile, true);
            Console.WriteLine("Short code " + code + " reserved for '" + solution + "' in environment '" + name + "'" + (observed == null ? "." : " (existing K2 use adopted)."));
            return 0;
        }

        private static int ListShortCodes(ProfileStore store, Options options)
        {
            var name = store.ResolveName(options.Get("name"));
            var profile = store.Read(name);
            var registrations = ShortCodeRegistry.Registrations(profile).OrderBy(x => x.Code).ToList();
            var observations = ShortCodeRegistry.Observations(profile).OrderBy(x => x.Code).ToList();
            if (options.IsJson)
            {
                Console.WriteLine(PrettyJson.Serialize(new { environment = name, reserved = registrations, observed = observations }));
                return 0;
            }
            Console.WriteLine("Solution short codes for environment '" + name + "':");
            if (registrations.Count == 0) Console.WriteLine("  Reserved: (none)");
            else foreach (var item in registrations) Console.WriteLine("  RESERVED " + item.Code + " - " + item.SolutionName + (string.IsNullOrWhiteSpace(item.RootCategoryPath) ? "" : " @ " + item.RootCategoryPath));
            var unclaimed = observations.Where(x => registrations.All(y => !string.Equals(y.Code, x.Code, StringComparison.OrdinalIgnoreCase))).ToList();
            if (unclaimed.Count == 0) Console.WriteLine("  Observed but unreserved: (none)");
            else foreach (var item in unclaimed) Console.WriteLine("  OBSERVED " + item.Code + " - " + item.ArtifactCount + " K2 Form/View artifact(s)");
            return 0;
        }

        private static int ReleaseShortCode(ProfileStore store, Options options)
        {
            if (!options.Has("confirm")) throw new CliException("release-short-code requires --confirm.");
            var name = store.ResolveName(options.Get("name"));
            var profile = store.Read(name);
            var code = ShortCodeRegistry.NormalizeCode(options.Require("code"));
            var registration = ShortCodeRegistry.Find(profile, code);
            if (registration == null) throw new CliException("Short code " + code + " is not reserved in environment '" + name + "'.");
            var solution = ShortCodeRegistry.NormalizeSolutionName(code, options.Require("solution"), true);
            if (!ShortCodeRegistry.SameSolution(registration, solution))
                throw new CliException("Short code " + code + " belongs to '" + registration.SolutionName + "', not '" + solution + "'.");
            ShortCodeRegistry.Registrations(profile).Remove(registration);
            store.Write(profile, true);
            Console.WriteLine("Short code " + code + " reservation released for '" + solution + "'. Existing K2 artifacts may still keep the code observed and unavailable.");
            return 0;
        }

        private static int SetStyleProfile(ProfileStore store, Options options)
        {
            var name = store.ResolveName(options.Get("name"));
            var profile = store.Read(name);
            if (profile.SmartForms == null || profile.SmartForms.StyleProfiles == null)
                throw new CliException("Profile has no discovered SmartForms style profiles. Run refresh first: " + name);
            if (options.Has("no-style-profile"))
            {
                if (!string.IsNullOrWhiteSpace(options.Get("style-profile"))) throw new CliException("Use either --style-profile or --no-style-profile, not both.");
                profile.SmartForms.StyleProfileSelection = "none";
                profile.SmartForms.DefaultStyleProfile = null;
            }
            else
            {
                var value = options.Require("style-profile");
                Guid guid;
                var matches = profile.SmartForms.StyleProfiles.Where(x =>
                    (Guid.TryParse(value, out guid) && x.Guid == guid) ||
                    string.Equals(x.Name, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.DisplayName, value, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 0) throw new CliException("Style profile not found: " + value + ". Available: " + string.Join(", ", profile.SmartForms.StyleProfiles.Select(x => x.DisplayName + " [" + x.Name + "]").ToArray()));
                if (matches.Count > 1) throw new CliException("Style profile selection is ambiguous; use its GUID: " + value);
                profile.SmartForms.StyleProfileSelection = "selected";
                profile.SmartForms.DefaultStyleProfile = matches[0];
            }
            store.Write(profile, true);
            Console.WriteLine("Default SmartForms style profile: " + (profile.SmartForms.DefaultStyleProfile == null ? "(none)" : profile.SmartForms.DefaultStyleProfile.DisplayName + " [" + profile.SmartForms.DefaultStyleProfile.Name + "]"));
            return 0;
        }

        private static void PreserveStyleProfileSelection(EnvironmentProfile previous, EnvironmentProfile current)
        {
            if (previous == null || previous.SmartForms == null || current.SmartForms == null) return;
            if (string.Equals(previous.SmartForms.StyleProfileSelection, "none", StringComparison.OrdinalIgnoreCase))
            {
                current.SmartForms.StyleProfileSelection = "none";
                return;
            }
            var selected = previous.SmartForms.DefaultStyleProfile;
            if (!string.Equals(previous.SmartForms.StyleProfileSelection, "selected", StringComparison.OrdinalIgnoreCase) || selected == null) return;
            var refreshed = current.SmartForms.StyleProfiles.FirstOrDefault(x => x.Guid == selected.Guid);
            if (refreshed == null) return;
            current.SmartForms.StyleProfileSelection = "selected";
            current.SmartForms.DefaultStyleProfile = refreshed;
        }

        private static int InspectHeader(ProfileStore store, Options options)
        {
            var name = store.ResolveName(options.Get("name"));
            var matches = Discovery.InspectHeaders(store.Read(name).K2, options.Require("hint"));
            if (options.IsJson) Console.WriteLine(PrettyJson.Serialize(new { environment = name, matches = matches }));
            else
            {
                Console.WriteLine("Common framework view matches: " + matches.Count);
                foreach (var item in matches) WriteHeader(item, true);
            }
            return matches.Count == 0 ? 1 : 0;
        }

        private static int SetCommonHeader(ProfileStore store, Options options)
        {
            var name = store.ResolveName(options.Get("name"));
            var profile = store.Read(name);
            if (profile.SmartForms == null) throw new CliException("Profile has no SmartForms metadata. Run refresh first: " + name);
            if (options.Has("no-common-header"))
            {
                if (!string.IsNullOrWhiteSpace(options.Get("view"))) throw new CliException("Use either --view or --no-common-header, not both.");
                profile.SmartForms.CommonHeaderSelection = "none";
                profile.SmartForms.DefaultCommonHeader = null;
            }
            else
            {
                var value = options.Require("view");
                var matches = Discovery.InspectHeaders(profile.K2, value);
                var exact = matches.Where(x => string.Equals(x.Guid.ToString(), value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Name, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.DisplayName, value, StringComparison.OrdinalIgnoreCase)).ToList();
                if (exact.Count == 1) matches = exact;
                if (matches.Count == 0) throw new CliException("Header view not found: " + value + ". Run inspect-header with a broader --hint.");
                if (matches.Count > 1) throw new CliException("Header view selection is ambiguous; use its GUID. Matches: " + string.Join(", ", matches.Select(x => x.DisplayName + " [" + x.Name + "] " + x.Guid).ToArray()));
                var selected = matches[0];
                var initializeEvent = options.Get("initialize-event");
                if (string.IsNullOrWhiteSpace(initializeEvent))
                {
                    var init = selected.Events.FirstOrDefault(x => string.Equals(x.Type, "User", StringComparison.OrdinalIgnoreCase) &&
                        (string.Equals(x.Name, "Init", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Name, "Initialize", StringComparison.OrdinalIgnoreCase)));
                    initializeEvent = init == null ? null : init.Name;
                }
                if (!string.IsNullOrWhiteSpace(initializeEvent) && !selected.Events.Any(x => string.Equals(x.Name, initializeEvent, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Type, "User", StringComparison.OrdinalIgnoreCase)))
                    throw new CliException("Header view has no callable user initialization rule named '" + initializeEvent + "'. Available user rules: " + string.Join(", ", selected.Events.Where(x => string.Equals(x.Type, "User", StringComparison.OrdinalIgnoreCase)).Select(x => x.Name).ToArray()));
                var serverRules = options.GetAll("server-rule").ToList();
                foreach (var serverRule in serverRules)
                    if (!selected.Events.Any(x => string.Equals(x.Name, serverRule, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Type, "User", StringComparison.OrdinalIgnoreCase)))
                        throw new CliException("Header view has no callable user server rule named '" + serverRule + "'. Available user rules: " + string.Join(", ", selected.Events.Where(x => string.Equals(x.Type, "User", StringComparison.OrdinalIgnoreCase)).Select(x => x.Name).ToArray()));
                if (serverRules.Distinct(StringComparer.OrdinalIgnoreCase).Count() != serverRules.Count)
                    throw new CliException("Duplicate --server-rule values are not allowed.");
                var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var assignment in options.GetAll("parameter"))
                {
                    var separator = assignment.IndexOf('=');
                    if (separator < 1) throw new CliException("--parameter must use NAME=VALUE: " + assignment);
                    var parameterName = assignment.Substring(0, separator).Trim();
                    var parameterValue = assignment.Substring(separator + 1);
                    if (!selected.Parameters.Any(x => string.Equals(x.Name, parameterName, StringComparison.OrdinalIgnoreCase)))
                        throw new CliException("Header view has no parameter named '" + parameterName + "'. Available: " + string.Join(", ", selected.Parameters.Select(x => x.Name).ToArray()));
                    parameters[parameterName] = parameterValue;
                }
                var transfers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var assignment in options.GetAll("control-transfer"))
                {
                    var separator = assignment.IndexOf('=');
                    if (separator < 1) throw new CliException("--control-transfer must use CONTROL=VALUE: " + assignment);
                    var controlName = assignment.Substring(0, separator).Trim();
                    var controlValue = assignment.Substring(separator + 1);
                    if (!selected.Controls.Any(x => string.Equals(x.Name, controlName, StringComparison.OrdinalIgnoreCase) || string.Equals(x.DisplayName, controlName, StringComparison.OrdinalIgnoreCase)))
                        throw new CliException("Header view has no control named '" + controlName + "'. Available: " + string.Join(", ", selected.Controls.Select(x => x.Name).ToArray()));
                    transfers[controlName] = controlValue;
                }
                bool? isCollapsible = null;
                var collapsibleValue = options.Get("collapsible");
                if (!string.IsNullOrWhiteSpace(collapsibleValue))
                {
                    bool parsedCollapsible;
                    if (!bool.TryParse(collapsibleValue, out parsedCollapsible)) throw new CliException("--collapsible must be true or false.");
                    isCollapsible = parsedCollapsible;
                }
                var serverLoadOrder = options.Get("server-load-order") ?? "transfers-then-rules";
                if (!string.Equals(serverLoadOrder, "transfers-then-rules", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(serverLoadOrder, "rules-then-transfers", StringComparison.OrdinalIgnoreCase))
                    throw new CliException("--server-load-order must be transfers-then-rules or rules-then-transfers.");
                CommonFooterSettings footer = null;
                var footerValue = options.Get("footer");
                if (!string.IsNullOrWhiteSpace(footerValue))
                {
                    var footerMatches = Discovery.InspectHeaders(profile.K2, footerValue);
                    var footerExact = footerMatches.Where(x => string.Equals(x.Guid.ToString(), footerValue, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Name, footerValue, StringComparison.OrdinalIgnoreCase) || string.Equals(x.DisplayName, footerValue, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (footerExact.Count == 1) footerMatches = footerExact;
                    if (footerMatches.Count == 0) throw new CliException("Common footer view not found: " + footerValue + ". Run inspect-framework with a broader --hint.");
                    if (footerMatches.Count > 1) throw new CliException("Common footer selection is ambiguous; use its GUID.");
                    var selectedFooter = footerMatches[0];
                    footer = new CommonFooterSettings
                    {
                        ViewGuid = selectedFooter.Guid, ViewName = selectedFooter.Name, ViewDisplayName = selectedFooter.DisplayName,
                        CategoryPath = selectedFooter.CategoryPath, ViewVersion = selectedFooter.Version,
                        Title = options.Get("footer-title") ?? string.Empty, Inspection = selectedFooter
                    };
                }
                profile.SmartForms.CommonHeaderSelection = "selected";
                profile.SmartForms.DefaultCommonHeader = new CommonHeaderSettings
                {
                    ViewGuid = selected.Guid, ViewName = selected.Name, ViewDisplayName = selected.DisplayName,
                    CategoryPath = selected.CategoryPath, ViewVersion = selected.Version,
                    Title = options.Get("title") ?? string.Empty, InstanceName = options.Get("instance-name"), IsCollapsible = isCollapsible,
                    InitializeEvent = initializeEvent, ServerRules = serverRules,
                    ServerRulesBeforeControlTransfers = string.Equals(serverLoadOrder, "rules-then-transfers", StringComparison.OrdinalIgnoreCase),
                    Parameters = parameters, ServerLoadControlTransfers = transfers,
                    Footer = footer, Inspection = selected
                };
            }
            store.Write(profile, true);
            var header = profile.SmartForms.DefaultCommonHeader;
            Console.WriteLine("Default SmartForms common header: " + (header == null ? "(none)" : header.ViewDisplayName + " [" + header.ViewName + "]"));
            if (header != null) Console.WriteLine("  Instance: name=" + (header.InstanceName ?? "(generated)") + ", title='" + (header.Title ?? string.Empty) + "', collapsible=" + (header.IsCollapsible.HasValue ? header.IsCollapsible.Value.ToString().ToLowerInvariant() : "generated") + "; initialize event: " + (header.InitializeEvent ?? "(none)") + "; server rules: " + (header.ServerRules == null || header.ServerRules.Count == 0 ? "(none)" : string.Join(", ", header.ServerRules.ToArray())) + "; server-load order: " + (header.ServerRulesBeforeControlTransfers ? "rules-then-transfers" : "transfers-then-rules") + "; parameters: " + string.Join(", ", header.Parameters.Select(x => x.Key + "=" + x.Value).ToArray()) + "; server-load transfers: " + string.Join(", ", (header.ServerLoadControlTransfers ?? new Dictionary<string, string>()).Select(x => x.Key + "=" + x.Value).ToArray()) + "; footer: " + (header.Footer == null ? "(none)" : header.Footer.ViewName));
            return 0;
        }

        private static void PreserveCommonHeaderSelection(EnvironmentProfile previous, EnvironmentProfile current)
        {
            if (previous == null || previous.SmartForms == null || current.SmartForms == null) return;
            if (string.Equals(previous.SmartForms.CommonHeaderSelection, "none", StringComparison.OrdinalIgnoreCase))
            {
                current.SmartForms.CommonHeaderSelection = "none";
                return;
            }
            var selected = previous.SmartForms.DefaultCommonHeader;
            if (!string.Equals(previous.SmartForms.CommonHeaderSelection, "selected", StringComparison.OrdinalIgnoreCase) || selected == null) return;
            var refreshed = current.SmartForms.HeaderViewCandidates == null ? null : current.SmartForms.HeaderViewCandidates.FirstOrDefault(x => x.Guid == selected.ViewGuid);
            if (refreshed == null)
            {
                var live = Discovery.InspectHeaders(current.K2, selected.ViewGuid.ToString());
                refreshed = live.Count == 1 ? live[0] : null;
                if (refreshed != null && current.SmartForms.HeaderViewCandidates != null) current.SmartForms.HeaderViewCandidates.Add(refreshed);
            }
            if (refreshed == null) return;
            CommonFooterSettings footer = null;
            if (selected.Footer != null)
            {
                var liveFooter = Discovery.InspectHeaders(current.K2, selected.Footer.ViewGuid.ToString());
                var refreshedFooter = liveFooter.Count == 1 ? liveFooter[0] : null;
                if (refreshedFooter == null) return;
                if (current.SmartForms.HeaderViewCandidates != null && !current.SmartForms.HeaderViewCandidates.Any(x => x.Guid == refreshedFooter.Guid)) current.SmartForms.HeaderViewCandidates.Add(refreshedFooter);
                footer = new CommonFooterSettings
                {
                    ViewGuid = refreshedFooter.Guid, ViewName = refreshedFooter.Name, ViewDisplayName = refreshedFooter.DisplayName,
                    CategoryPath = refreshedFooter.CategoryPath, ViewVersion = refreshedFooter.Version,
                    Title = selected.Footer.Title, Inspection = refreshedFooter
                };
            }
            current.SmartForms.CommonHeaderSelection = "selected";
            current.SmartForms.DefaultCommonHeader = new CommonHeaderSettings
            {
                ViewGuid = refreshed.Guid, ViewName = refreshed.Name, ViewDisplayName = refreshed.DisplayName,
                CategoryPath = refreshed.CategoryPath, ViewVersion = refreshed.Version,
                Title = selected.Title, InstanceName = selected.InstanceName, IsCollapsible = selected.IsCollapsible,
                InitializeEvent = selected.InitializeEvent,
                ServerRules = selected.ServerRules ?? new List<string>(),
                ServerRulesBeforeControlTransfers = selected.ServerRulesBeforeControlTransfers,
                Parameters = selected.Parameters ?? new Dictionary<string, string>(),
                ServerLoadControlTransfers = selected.ServerLoadControlTransfers ?? new Dictionary<string, string>(),
                Footer = footer, Inspection = refreshed
            };
        }

        private static void PreserveSolutionCodeRegistrations(EnvironmentProfile previous, EnvironmentProfile current)
        {
            current.SolutionCodes = previous == null || previous.SolutionCodes == null
                ? new List<SolutionCodeRegistration>()
                : previous.SolutionCodes;
        }

        private static void WriteProfile(EnvironmentProfile profile, string path, bool json)
        {
            if (json) { Console.WriteLine(PrettyJson.Serialize(profile)); return; }
            Console.WriteLine("K2 environment: " + profile.Name);
            Console.WriteLine("Profile: " + path);
            Console.WriteLine("K2: " + profile.K2.Host + ":" + profile.K2.ManagementPort + " (integrated, label=" + profile.K2.SecurityLabel + ")");
            Console.WriteLine("Version: " + profile.K2.Version);
            Console.WriteLine("Install: " + profile.K2.InstallDirectory);
            Console.WriteLine("Designer: " + profile.Urls.Designer);
            Console.WriteLine("Runtime: " + profile.Urls.Runtime);
            if (profile.SmartForms != null)
            {
                Console.WriteLine("Themes: " + string.Join(", ", (profile.SmartForms.Themes ?? new List<string>()).ToArray()));
                Console.WriteLine("Style profiles:");
                foreach (var item in profile.SmartForms.StyleProfiles ?? new List<StyleProfileSettings>())
                    Console.WriteLine("  " + item.DisplayName + " [" + item.Name + "] - " + item.CategoryPath + " (" + item.Guid + ")");
                var selected = profile.SmartForms.DefaultStyleProfile;
                Console.WriteLine("Default style profile: " + (profile.SmartForms.StyleProfileSelection == "unselected" ? "(selection required)" : selected == null ? "(none)" : selected.DisplayName + " [" + selected.Name + "]"));
                Console.WriteLine("Header-view candidates:");
                foreach (var item in profile.SmartForms.HeaderViewCandidates ?? new List<HeaderViewCandidate>()) WriteHeader(item, false);
                var header = profile.SmartForms.DefaultCommonHeader;
                Console.WriteLine("Default common header: " + (profile.SmartForms.CommonHeaderSelection == "unselected" ? "(selection required)" : header == null ? "(none)" : header.ViewDisplayName + " [" + header.ViewName + "]"));
            }
        }

        private static void WriteHeader(HeaderViewCandidate item, bool detailed)
        {
            Console.WriteLine("  " + item.DisplayName + " [" + item.Name + "] - " + item.CategoryPath + " (" + item.Guid + ", v" + item.Version + ", " + item.ConsumerFormCount + " consumer form(s))");
            Console.WriteLine("    parameters: " + (item.Parameters.Count == 0 ? "(none)" : string.Join(", ", item.Parameters.Select(x => x.Name + ":" + x.DataType + (string.IsNullOrEmpty(x.DefaultValue) ? "" : "=" + x.DefaultValue)).ToArray())));
            Console.WriteLine("    view events: " + (item.Events.Count == 0 ? "(none)" : string.Join(", ", item.Events.Select(x => x.Name + " [" + x.Type + ", " + x.ActionCount + " action(s)]").ToArray())));
            Console.WriteLine("    controls: " + (item.Controls == null || item.Controls.Count == 0 ? "(none)" : string.Join(", ", item.Controls.Select(x => x.Name + ":" + x.Type).ToArray())));
            if (!detailed) return;
            foreach (var consumer in item.Consumers)
                Console.WriteLine("    consumer: " + consumer.FormDisplayName + " [" + consumer.FormName + "] - " + consumer.CategoryPath + "; mappings=" +
                    (consumer.InitializeBindings.Count == 0 ? "(none)" : string.Join(", ", consumer.InitializeBindings.Select(x => x.TargetParameter + "<-" + x.SourceType + ":" + (x.SourceName ?? x.Value ?? "")).ToArray())));
        }

        private static void WriteValidation(ValidationResult result, bool json)
        {
            if (json) { Console.WriteLine(PrettyJson.Serialize(result)); return; }
            Console.WriteLine("Validation: " + (result.Valid ? "passed" : "failed"));
            foreach (var check in result.Checks) Console.WriteLine("  " + check.Status.ToUpperInvariant() + " " + check.Name + ": " + check.Message);
        }

        private static bool IsHelp(string value) { return value == "help" || value == "--help" || value == "-h" || value == "/?"; }
        private static void Help()
        {
            Console.WriteLine("k2env " + Version + " - durable K2 Five environment profiles");
            Console.WriteLine("Commands:");
            Console.WriteLine("  discover --name NAME [--default] [--install-dir PATH] [--host HOST] [--base-url URL]");
            Console.WriteLine("  refresh  --name NAME [--install-dir PATH] [--host HOST] [--base-url URL]");
            Console.WriteLine("  show [--name NAME] [--output json]");
            Console.WriteLine("  validate [--name NAME] [--output json]");
            Console.WriteLine("  list [--output json]");
            Console.WriteLine("  set-default --name NAME");
            Console.WriteLine("  set-style-profile [--name NAME] (--style-profile NAME_OR_GUID | --no-style-profile)");
            Console.WriteLine("  inspect-header [--name NAME] --hint TEXT [--output json]");
            Console.WriteLine("  inspect-framework [--name NAME] --hint TEXT [--output json]");
            Console.WriteLine("  set-common-header [--name NAME] (--view NAME_OR_GUID [--footer NAME_OR_GUID] [--instance-name TEXT] [--title TEXT] [--collapsible true|false] [--initialize-event EVENT] [--server-rule EVENT ...] [--server-load-order transfers-then-rules|rules-then-transfers] [--footer-title TEXT] [--parameter NAME=VALUE ...] [--control-transfer CONTROL=VALUE ...] | --no-common-header)");
            Console.WriteLine("  check-short-code [--name NAME] --code ABC [--solution 'ABC.Solution Name']");
            Console.WriteLine("  reserve-short-code [--name NAME] --code ABC --solution 'ABC.Solution Name' [--root-category PATH] [--manifest PATH] [--adopt-existing]");
            Console.WriteLine("  list-short-codes [--name NAME] [--output json]");
            Console.WriteLine("  release-short-code [--name NAME] --code ABC --solution 'ABC.Solution Name' --confirm");
            Console.WriteLine("Common: --root PATH overrides the default %CODEX_HOME%\\k2 store.");
        }
    }

    internal sealed class Options
    {
        private readonly Dictionary<string, List<string>> _values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _switches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool IsJson { get { return string.Equals(Get("output"), "json", StringComparison.OrdinalIgnoreCase); } }
        public string Get(string name) { List<string> values; return _values.TryGetValue(name, out values) && values.Count > 0 ? values[values.Count - 1] : null; }
        public IEnumerable<string> GetAll(string name) { List<string> values; return _values.TryGetValue(name, out values) ? values : Enumerable.Empty<string>(); }
        public bool Has(string name) { return _switches.Contains(name); }
        public string Require(string name) { var value = Get(name); if (string.IsNullOrWhiteSpace(value)) throw new CliException("--" + name + " is required."); return value; }

        public static Options Parse(string[] args)
        {
            var result = new Options();
            for (var i = 0; i < args.Length; i++)
            {
                var token = args[i];
                if (!token.StartsWith("--")) throw new CliException("Unexpected argument: " + token);
                var name = token.Substring(2);
                if (name == "default" || name == "no-style-profile" || name == "no-common-header" || name == "adopt-existing" || name == "confirm") { result._switches.Add(name); continue; }
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--")) throw new CliException("Missing value for --" + name + ".");
                List<string> values;
                if (!result._values.TryGetValue(name, out values)) { values = new List<string>(); result._values[name] = values; }
                values.Add(args[++i]);
            }
            var output = result.Get("output");
            if (output != null && output != "text" && output != "json") throw new CliException("--output must be text or json.");
            return result;
        }
    }
}
