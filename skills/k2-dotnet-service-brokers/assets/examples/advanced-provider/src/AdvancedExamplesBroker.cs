using System;
using SourceCode.SmartObjects.Services.ServiceSDK;
using SourceCode.SmartObjects.Services.ServiceSDK.Objects;
using SourceCode.SmartObjects.Services.ServiceSDK.Types;

namespace K2Skills.Examples.AdvancedBroker
{
    public sealed class AdvancedExamplesBroker : ServiceAssemblyBase
    {
        public override string GetConfigSection()
        {
            try { Service.ServiceConfiguration.Add("AllowedRoot", true, string.Empty); }
            catch (Exception exception) { Fail(exception); }
            return base.GetConfigSection();
        }

        public override string DescribeSchema()
        {
            try
            {
                Service.ServiceObjects.Create(new ServiceObject(typeof(TextToolkit)));
                Service.ServiceObjects.Create(new ServiceObject(typeof(EnvironmentProbe)));
                Service.ServiceObjects.Create(new ServiceObject(typeof(FileCatalog)));
                Service.Name = "K2SkillsAdvancedExamples";
                Service.MetaData.DisplayName = "K2 Skills Advanced .NET Examples";
                Service.MetaData.Description = "Text/crypto, host diagnostics, and bounded filesystem examples.";
                ServicePackage.IsSuccessful = true;
            }
            catch (Exception exception) { Fail(exception); }
            return base.DescribeSchema();
        }

        public override void Extend()
        {
            ServicePackage.ServiceMessages.Add("Schema extension is not supported.", MessageSeverity.Error);
            ServicePackage.IsSuccessful = false;
        }

        private void Fail(Exception exception)
        {
            ServicePackage.ServiceMessages.Add(exception.Message, MessageSeverity.Error);
            ServicePackage.IsSuccessful = false;
        }
    }
}
