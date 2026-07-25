using System;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class ControlRuleDefinition
    {
        public static XElement BuildSystemEvent(XNamespace ns, string controlId, string eventName)
        {
            return new XElement(ns + "Event",
                new XAttribute("ID", NewId()),
                new XAttribute("DefinitionID", NewId()),
                new XAttribute("Type", "System"),
                new XAttribute("SourceID", controlId),
                new XAttribute("SourceType", "Control"),
                new XElement(ns + "Name", eventName),
                new XElement(ns + "Handlers",
                    new XElement(ns + "Handler",
                        new XAttribute("ID", NewId()),
                        new XAttribute("DefinitionID", NewId()),
                        new XElement(ns + "Actions",
                            new XElement(ns + "Action",
                                new XAttribute("ID", NewId()),
                                new XAttribute("DefinitionID", NewId()),
                                new XAttribute("Type", "ApplyStyle"),
                                new XAttribute("ExecutionType", "Synchronous"))))));
        }

        public static void VerifySystemEvent(XElement scope, string controlId, string eventName, string owner)
        {
            var matches = scope.Descendants().Where(x => x.Name.LocalName == "Event" &&
                string.Equals((string)x.Attribute("Type"), "System", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("SourceType"), "Control", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("SourceID"), controlId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ChildValue(x, "Name"), eventName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1)
                throw new CliException(owner + " must contain exactly one K2 system " + eventName +
                    " declaration for control '" + controlId + "' so the Rule Designer can hydrate its user rule.");

            var systemEvent = matches[0];
            var handlers = systemEvent.Elements().Where(x => x.Name.LocalName == "Handlers")
                .SelectMany(x => x.Elements()).Where(x => x.Name.LocalName == "Handler").ToList();
            var actions = handlers.SelectMany(x => x.Elements().Where(y => y.Name.LocalName == "Actions"))
                .SelectMany(x => x.Elements()).Where(x => x.Name.LocalName == "Action").ToList();
            if (!HasGuidIdentity(systemEvent) || handlers.Count != 1 || !HasGuidIdentity(handlers[0]) ||
                actions.Count != 1 || !HasGuidIdentity(actions[0]) ||
                !string.Equals((string)actions[0].Attribute("Type"), "ApplyStyle", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals((string)actions[0].Attribute("ExecutionType"), "Synchronous", StringComparison.OrdinalIgnoreCase))
                throw new CliException(owner + " has a malformed K2 system " + eventName +
                    " declaration for control '" + controlId + "'.");
        }

        private static bool HasGuidIdentity(XElement element)
        {
            Guid id;
            Guid definitionId;
            return Guid.TryParse((string)element.Attribute("ID"), out id) &&
                Guid.TryParse((string)element.Attribute("DefinitionID"), out definitionId);
        }

        private static string ChildValue(XElement parent, string childName)
        {
            var child = parent.Elements().FirstOrDefault(x => x.Name.LocalName == childName);
            return child == null ? null : child.Value;
        }

        private static string NewId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
