using System;
using System.Reflection;
using SourceCode.Forms.Authoring;
using SourceCode.Forms.Management;

namespace K2StyleProfilesCli
{
    internal interface IStyleProfileAuthoringSession
    {
        string Load(Guid guid);
        void Deploy(string definitionXml, string categoryPath, bool checkIn);
        void CheckIn(Guid guid);
    }

    internal sealed class FormsManagerStyleProfileAuthoringSession :
        IStyleProfileAuthoringSession
    {
        private readonly FormsManager _manager;

        public FormsManagerStyleProfileAuthoringSession(FormsManager manager)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            _manager = manager;
        }

        public string Load(Guid guid)
        {
            var definition = _manager.GetStyleProfileDefinition(guid);
            if (string.IsNullOrWhiteSpace(definition))
                throw new CliException(
                    "K2 returned an empty Style Profile definition: " + guid);
            return definition;
        }

        public void Deploy(string definitionXml, string categoryPath, bool checkIn)
        {
            _manager.Deploy(definitionXml, categoryPath, checkIn);
        }

        public void CheckIn(Guid guid)
        {
            _manager.CheckInStyleProfile(guid);
        }
    }

    internal static class AuthenticatedStyleProfileGateway
    {
        public static string Load(
            IStyleProfileAuthoringSession session,
            Guid guid)
        {
            if (session == null) throw new ArgumentNullException("session");
            return session.Load(guid);
        }

        public static void AssertInstalledContract()
        {
            RequireMethod(
                "GetStyleProfileDefinition",
                typeof(Guid));
            RequireMethod(
                "Deploy",
                typeof(string),
                typeof(string),
                typeof(bool));
            RequireMethod(
                "CheckInStyleProfile",
                typeof(Guid));
        }

        public static Guid DeployAndCheckIn(
            IStyleProfileAuthoringSession session,
            StyleProfile definition,
            string categoryPath)
        {
            if (session == null) throw new ArgumentNullException("session");
            if (definition == null) throw new ArgumentNullException("definition");
            if (definition.Guid == Guid.Empty)
                throw new CliException(
                    "Style Profile authoring definition has no deployment GUID.");
            session.Deploy(definition.ToXml(), categoryPath, false);
            session.CheckIn(definition.Guid);
            return definition.Guid;
        }

        private static void RequireMethod(string name, params Type[] parameterTypes)
        {
            if (typeof(FormsManager).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null) == null)
            {
                throw new CliException(
                    "This K2 version does not provide the authenticated Style "
                    + "Profile authoring method: "
                    + name);
            }
        }
    }
}
