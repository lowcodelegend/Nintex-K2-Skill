using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using A = SourceCode.SmartObjects.Services.ServiceSDK.Attributes;
using SourceCode.SmartObjects.Services.ServiceSDK.Types;

namespace K2Skills.Examples.AdvancedBroker
{
    [A.ServiceObject("TextToolkit", "Advanced Text Toolkit", "Normalizes, slugifies, and hashes text using server-side .NET.")]
    public sealed class TextToolkit
    {
        [A.Property("InputText", SoType.Memo, "Input Text", "Text to transform.")]
        public string InputText { get; set; }
        [A.Property("NormalizedText", SoType.Memo, "Normalized Text", "Trimmed text with repeated whitespace collapsed.")]
        public string NormalizedText { get; set; }
        [A.Property("Slug", SoType.Text, "Slug", "Lower-case ASCII-safe slug.")]
        public string Slug { get; set; }
        [A.Property("Sha256", SoType.Text, "SHA-256", "Upper-case SHA-256 hexadecimal digest.")]
        public string Sha256 { get; set; }

        [A.Method("Transform", MethodType.Read, "Transform Text", "Normalize, slugify, and hash text.",
            new[] { "InputText" }, new[] { "InputText" }, new[] { "InputText", "NormalizedText", "Slug", "Sha256" })]
        public TextToolkit Transform()
        {
            if (string.IsNullOrWhiteSpace(InputText)) throw new InvalidOperationException("Input Text is required.");
            var normalized = Regex.Replace(InputText.Trim(), @"\s+", " ");
            var slug = Regex.Replace(normalized.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            using (var hash = SHA256.Create())
            {
                var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                return new TextToolkit {
                    InputText = InputText, NormalizedText = normalized, Slug = slug,
                    Sha256 = BitConverter.ToString(bytes).Replace("-", string.Empty)
                };
            }
        }
    }
}
