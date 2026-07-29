using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace K2StyleProfilesCli
{
    internal static class CssValidationContract
    {
        private sealed class Rule
        {
            public int Position;
            public string Selector;
            public int Specificity;
            public HashSet<string> Treatments;
            public HashSet<string> ImportantTreatments;
        }

        private static readonly Regex RulePattern = new Regex(
            @"(?s)([^{}]+)\{([^{}]*)\}", RegexOptions.Compiled);
        private static readonly Regex DeclarationPattern = new Regex(
            @"(?im)(?:^|;)\s*(border(?:-[\w-]+)?|background(?:-[\w-]+)?)\s*:\s*([^;}]*)",
            RegexOptions.Compiled);
        private static readonly Regex InputSelectorPattern = new Regex(
            @"(?i)(?:^|[\s>+~,])(?:input|textarea|select)(?=$|[\s>+~,.:\[])|" +
            @"(?i)\.(?:input-control|input-control-m|input-control-m-c|input-control-wrapper|" +
            @"select-box|calendar|checkbox|file-wrapper|text-input|textarea)(?=$|[\s>+~,.:\[])",
            RegexOptions.Compiled);
        private static readonly Regex InvalidSelectorPattern = new Regex(
            @"(?i)(?:\.invalid\b|\[aria-invalid(?:\s*=\s*['""]?true['""]?)?\])",
            RegexOptions.Compiled);
        private static readonly Regex UniversalSelectorPattern = new Regex(
            @"(?i)(?:^|[\s>+~,])\*(?=$|[\s>+~,.:#\[])",
            RegexOptions.Compiled);
        private static readonly Regex MotionDeclarationPattern = new Regex(
            @"(?im)(?:^|;)\s*(transition(?:-duration)?|animation(?:-duration|-iteration-count)?|scroll-behavior)\s*:",
            RegexOptions.Compiled);

        public static void Validate(string css, string sourceName)
        {
            if (string.IsNullOrWhiteSpace(css)) return;
            RejectBroadMotionOverrides(css, sourceName);
            var rules = Parse(css);
            foreach (var rule in rules.Where(x =>
                x.Treatments.Count > 0 &&
                InputSelectorPattern.IsMatch(x.Selector) &&
                !InvalidSelectorPattern.IsMatch(x.Selector)))
            {
                foreach (var treatment in rule.Treatments)
                {
                    var requiresImportant = rule.ImportantTreatments.Contains(treatment);
                    var protectedLater = rules.Any(candidate =>
                        candidate.Position > rule.Position &&
                        candidate.Specificity >= rule.Specificity &&
                        candidate.Treatments.Contains(treatment) &&
                        (!requiresImportant || candidate.ImportantTreatments.Contains(treatment)) &&
                        InvalidSelectorPattern.IsMatch(candidate.Selector) &&
                        FamiliesOverlap(rule.Selector, candidate.Selector));
                    if (!protectedLater)
                    {
                        throw new CliException(
                            "CSS validation contract failed in '" + sourceName +
                            "': selector '" + Collapse(rule.Selector) + "' overrides " +
                            treatment + (requiresImportant ? " with !important" : string.Empty) +
                            " but has no later .invalid or " +
                            "[aria-invalid=true] treatment with equal or greater specificity.");
                    }
                }
            }
        }

        private static void RejectBroadMotionOverrides(string css, string sourceName)
        {
            var clean = Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            foreach (Match match in RulePattern.Matches(clean))
            {
                var motion = MotionDeclarationPattern.Match(match.Groups[2].Value);
                if (!motion.Success) continue;
                foreach (var selector in match.Groups[1].Value.Split(','))
                {
                    var trimmed = selector.Trim();
                    if (trimmed.Length == 0 ||
                        trimmed.StartsWith("@", StringComparison.Ordinal) ||
                        !UniversalSelectorPattern.IsMatch(trimmed))
                        continue;
                    throw new CliException(
                        "CSS validation contract failed in '" + sourceName +
                        "': selector '" + Collapse(trimmed) + "' applies broad " +
                        motion.Groups[1].Value + " behavior through a universal selector. " +
                        "Scope reduced-motion and other motion overrides only to explicitly " +
                        "owned Style Profile elements; universal overrides can break native " +
                        "K2 dropdown positioning and cause Runtime to jump to the top.");
                }
            }
        }

        private static List<Rule> Parse(string css)
        {
            var clean = Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            var result = new List<Rule>();
            foreach (Match match in RulePattern.Matches(clean))
            {
                HashSet<string> important;
                var declarations = Treatments(match.Groups[2].Value, out important);
                if (declarations.Count == 0) continue;
                foreach (var selector in match.Groups[1].Value.Split(','))
                {
                    var trimmed = selector.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("@", StringComparison.Ordinal)) continue;
                    result.Add(new Rule
                    {
                        Position = match.Index,
                        Selector = trimmed,
                        Specificity = Specificity(trimmed),
                        Treatments = declarations,
                        ImportantTreatments = important
                    });
                }
            }
            return result;
        }

        private static HashSet<string> Treatments(string declarations,
            out HashSet<string> important)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            important = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match declaration in DeclarationPattern.Matches(declarations))
            {
                var treatment = declaration.Groups[1].Value.StartsWith("border",
                    StringComparison.OrdinalIgnoreCase) ? "border" : "background";
                result.Add(treatment);
                if (declaration.Groups[2].Value.IndexOf("!important",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                    important.Add(treatment);
            }
            return result;
        }

        private static int Specificity(string selector)
        {
            var withoutNotContent = Regex.Replace(selector, @"(?i):not\(([^)]*)\)", "$1");
            var ids = Regex.Matches(withoutNotContent, @"#[\w-]+").Count;
            var classes = Regex.Matches(withoutNotContent,
                @"\.[\w-]+|\[[^\]]+\]|:(?!:)[\w-]+(?:\([^)]*\))?").Count;
            var stripped = Regex.Replace(withoutNotContent,
                @"#[\w-]+|\.[\w-]+|\[[^\]]+\]|:{1,2}[\w-]+(?:\([^)]*\))?|\*", " ");
            var elements = Regex.Matches(stripped, @"(?:^|[\s>+~])([a-zA-Z][\w-]*)").Count;
            return ids * 100 + classes * 10 + elements;
        }

        private static bool FamiliesOverlap(string styled, string invalid)
        {
            var styledFamilies = Families(styled);
            var invalidFamilies = Families(invalid);
            if (styledFamilies.Contains("generic") || invalidFamilies.Contains("generic")) return true;
            return styledFamilies.Overlaps(invalidFamilies);
        }

        private static HashSet<string> Families(string selector)
        {
            var value = selector.ToLowerInvariant();
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (value.Contains("file-wrapper") || value.Contains("file-post")) result.Add("file");
            if (value.Contains("checkbox")) result.Add("checkbox");
            if (value.Contains("calendar")) result.Add("calendar");
            if (value.Contains("select-box") || Regex.IsMatch(value, @"\bselect\b")) result.Add("select");
            if (value.Contains("textarea") || value.Contains("text-input") ||
                value.Contains("textbox") || Regex.IsMatch(value, @"\binput\b")) result.Add("text");
            if (result.Count == 0 || value.Contains(".input-control.invalid")) result.Add("generic");
            return result;
        }

        private static string Collapse(string value)
        {
            var collapsed = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
            return collapsed.Length <= 180 ? collapsed : collapsed.Substring(0, 177) + "...";
        }
    }
}
