using System;
using System.Linq;
using System.Xml.Linq;

namespace K2SqlCli
{
    internal static class SelfTests
    {
        public static void Run()
        {
            var xml =
                "<smartobjectroot><methods><method name='List' type='list'>" +
                "<serviceinstances><serviceinstance><objects><object><methods><method name='List' type='list'>" +
                "<parameters><parameter name='UserFQN' type='System.String' sotype='text'><mappings>" +
                "<mapping type='parameter'><parameter name='UserFQN'/></mapping></mappings></parameter></parameters>" +
                "</method></methods></object></objects></serviceinstance></serviceinstances>" +
                "<parameters><parameter name='UserFQN' type='text'/></parameters>" +
                "<validation><requiredproperties><property name='UserFQN'/></requiredproperties></validation>" +
                "<input><property name='UserFQN'/></input><return/></method></methods></smartobjectroot>";
            var mapping = new SmartObjectSystemValueMapping
            {
                SmartObject = "APP_CommandSuggestion",
                Method = "List",
                Target = "UserFQN",
                Value = "ConnectedUserFQN"
            };
            var transformed = K2Manager.ApplySystemValueMapping(xml, mapping);
            var document = XDocument.Parse(transformed);
            var rootMethod = document.Root.Elements("methods").Elements("method").Single();
            Assert(!rootMethod.Elements("parameters").Elements("parameter").Any(), "system value is not exposed as a caller parameter");
            Assert(!rootMethod.Elements("input").Elements("property").Any(), "system value is not exposed as caller input");
            Assert(!rootMethod.Descendants("requiredproperties").Elements("property").Any(), "system value is not caller-required");
            var serviceParameter = rootMethod.Descendants("parameter").Single();
            var system = serviceParameter.Descendants("mapping").Single();
            Assert((string)system.Attribute("type") == "system" &&
                (string)system.Element("value") == "ConnectedUserFQN",
                "service parameter receives ConnectedUserFQN");
            Assert(K2Manager.ApplySystemValueMapping(transformed, mapping) == transformed,
                "system-value transformation is idempotent");
            var leafOnly = K2Manager.CleanupCategoryPaths(@"K2 Skills\APP.Application", false);
            Assert(leafOnly.SequenceEqual(new[] { @"K2 Skills\APP.Application\Data" }),
                "standalone cleanup owns only the Data leaf");
            var complete = K2Manager.CleanupCategoryPaths(@"K2 Skills\APP.Application\\", true);
            Assert(complete.SequenceEqual(new[] { @"K2 Skills\APP.Application\Data", @"K2 Skills\APP.Application" }),
                "builder cleanup deletes Data before the application root");
            Assert(K2Manager.CleanupCategoryPaths(null, true).Count == 0,
                "category cleanup is disabled without a configured application root");
            Console.WriteLine("SELFTEST SUCCEEDED: server-side ConnectedUserFQN SmartObject method mapping and bounded category cleanup paths");
        }

        private static void Assert(bool condition, string description)
        {
            if (!condition) throw new Exception("Self-test failed: " + description);
        }
    }
}
