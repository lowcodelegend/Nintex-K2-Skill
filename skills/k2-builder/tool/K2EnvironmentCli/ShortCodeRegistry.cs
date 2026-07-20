using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace K2EnvironmentCli
{
    internal static class ShortCodeRegistry
    {
        public static string NormalizeCode(string value)
        {
            value = (value ?? string.Empty).Trim();
            if (!Regex.IsMatch(value, "^[A-Z]{3,4}$"))
                throw new CliException("--code must contain exactly three or four uppercase letters.");
            return value;
        }

        public static SolutionCodeRegistration Find(EnvironmentProfile profile, string code)
        {
            return Registrations(profile).FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
        }

        public static string NormalizeSolutionName(string code, string value, bool required)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                if (required) throw new CliException("--solution is required.");
                return null;
            }
            if (!value.StartsWith(code + ".", StringComparison.Ordinal))
                throw new CliException("--solution must start with the selected short-code prefix '" + code + ".'.");
            return value;
        }

        public static ObservedSolutionCode FindObserved(EnvironmentProfile profile, string code)
        {
            return Observations(profile).FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
        }

        public static List<SolutionCodeRegistration> Registrations(EnvironmentProfile profile)
        {
            if (profile.SolutionCodes == null) profile.SolutionCodes = new List<SolutionCodeRegistration>();
            return profile.SolutionCodes;
        }

        public static List<ObservedSolutionCode> Observations(EnvironmentProfile profile)
        {
            if (profile.ObservedSolutionCodes == null) profile.ObservedSolutionCodes = new List<ObservedSolutionCode>();
            return profile.ObservedSolutionCodes;
        }

        public static bool SameSolution(SolutionCodeRegistration registration, string solutionName)
        {
            return registration != null && !string.IsNullOrWhiteSpace(solutionName) &&
                string.Equals(registration.SolutionName, solutionName.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeOptionalPath(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim()));
        }
    }
}
