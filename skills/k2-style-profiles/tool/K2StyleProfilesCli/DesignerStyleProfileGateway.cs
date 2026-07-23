using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace K2StyleProfilesCli
{
    internal sealed class DesignerStyleProfileGateway
    {
        private readonly Type _handlerType;
        private readonly object _handler;

        public DesignerStyleProfileGateway()
        {
            var path = Path.Combine(RuntimeAssemblyResolver.InstallDirectory, @"K2 smartforms Designer\bin\SourceCode.Forms.dll");
            if (!File.Exists(path)) throw new CliException("K2 Designer assembly not found: " + path);
            var assembly = Assembly.LoadFrom(path);
            _handlerType = assembly.GetType("SourceCode.Forms.StyleProfiles.AJAXCall", false);
            if (_handlerType == null) throw new CliException("This K2 version does not expose the installed Designer Style Profile handler.");
            _handler = Activator.CreateInstance(_handlerType);
            RequireMethod("LoadStyleProfile", typeof(Guid));
            RequireMethod("SaveStyleProfile", typeof(string), typeof(string));
            RequireMethod("GetCategoryInfoForStyleProfile", typeof(Guid));
        }

        public string Load(Guid guid)
        {
            return Invoke("LoadStyleProfile", guid);
        }

        public string GetCategoryInfo(Guid guid)
        {
            return Invoke("GetCategoryInfoForStyleProfile", guid);
        }

        public SaveResult Save(string definitionJson, string categoryPath)
        {
            var response = Invoke("SaveStyleProfile", definitionJson, categoryPath);
            Dictionary<string, object> root;
            try { root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(response); }
            catch (Exception ex) { throw new CliException("K2 Designer returned invalid save JSON: " + ex.Message); }
            object successValue;
            if (!root.TryGetValue("success", out successValue) || !(successValue is bool) || !(bool)successValue)
                throw new CliException("K2 Designer rejected the Style Profile: " + response);
            object styleValue;
            if (!root.TryGetValue("styleProfile", out styleValue))
                throw new CliException("K2 Designer save response omitted styleProfile.");
            var style = styleValue as Dictionary<string, object>;
            if (style == null) throw new CliException("K2 Designer save response has an invalid styleProfile.");
            object idValue;
            Guid id;
            if (!style.TryGetValue("ID", out idValue) || !Guid.TryParse(Convert.ToString(idValue), out id))
                throw new CliException("K2 Designer save response omitted a valid Style Profile ID.");
            return new SaveResult { Guid = id, RawResponse = response };
        }

        private void RequireMethod(string name, params Type[] parameterTypes)
        {
            if (_handlerType.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic, null, parameterTypes, null) == null)
                throw new CliException("This K2 version does not provide the expected Designer Style Profile method: " + name);
        }

        private string Invoke(string name, params object[] arguments)
        {
            try
            {
                var types = Array.ConvertAll(arguments, x => x.GetType());
                var method = _handlerType.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic, null, types, null);
                return Convert.ToString(method.Invoke(_handler, arguments));
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                throw new CliException("K2 Designer Style Profile operation failed: " + inner.Message, inner);
            }
        }
    }

    internal sealed class SaveResult
    {
        public Guid Guid { get; set; }
        public string RawResponse { get; set; }
    }
}
