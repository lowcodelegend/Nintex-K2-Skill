using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using SourceCode.Forms.Authoring;
using SourceCode.Forms.Management;
using SourceCode.Forms.Utilities;
using SourceCode.Hosting.Client.BaseAPI;
using SourceCode.SmartObjects.Client;
using AuthoringViewType = SourceCode.Forms.Authoring.ViewType;

namespace K2SmartFormsCli
{
    internal sealed class SmartFormsManager
    {
        private readonly SmartFormsManifest _manifest;

        public SmartFormsManager(SmartFormsManifest manifest)
        {
            _manifest = manifest;
        }

        public void CheckConnectionAndInputs()
        {
            WithFormsManager(delegate(FormsManager manager)
            {
                var elapsed = manager.Ping();
                var themes = manager.GetThemes().Themes.Cast<Theme>().Select(x => x.Name).OrderBy(x => x).ToList();
                if (!themes.Contains(_manifest.Application.Theme, StringComparer.OrdinalIgnoreCase))
                    throw new CliException("K2 theme not found: " + _manifest.Application.Theme + ". Available: " + string.Join(", ", themes.ToArray()));
                Console.WriteLine("K2 SmartForms connection: OK (" + elapsed.TotalMilliseconds.ToString("0") + " ms, theme " + _manifest.Application.Theme + ")");
                return 0;
            });

            WithSmartObjectServer(delegate(SmartObjectClientServer server)
            {
                foreach (var view in _manifest.Application.Views)
                {
                    SmartObject smartObject;
                    try
                    {
                        smartObject = server.GetSmartObject(view.SmartObject);
                    }
                    catch (Exception ex)
                    {
                        throw new CliException("SmartObject '" + view.SmartObject + "' for view '" + view.Name + "' is unavailable: " + ex.Message);
                    }

                    var properties = new HashSet<string>(smartObject.Properties.Cast<SmartProperty>().Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
                    foreach (var property in view.Properties)
                        if (!properties.Contains(property)) throw new CliException("SmartObject '" + view.SmartObject + "' has no property '" + property + "' requested by view '" + view.Name + "'.");

                    var methods = new HashSet<string>(smartObject.AllMethods.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
                    foreach (var method in view.Methods)
                        if (!methods.Contains(method)) throw new CliException("SmartObject '" + view.SmartObject + "' has no method '" + method + "' requested by view '" + view.Name + "'.");
                    if (!string.IsNullOrWhiteSpace(view.DefaultListMethod) && !methods.Contains(view.DefaultListMethod))
                        throw new CliException("SmartObject '" + view.SmartObject + "' has no default List method '" + view.DefaultListMethod + "' requested by view '" + view.Name + "'.");

                    ValidateRequiredMethodInputs(view, smartObject);

                    Console.WriteLine("SmartObject input: OK (" + view.SmartObject + ", " + properties.Count + " properties, " + methods.Count + " methods)");
                }
                return 0;
            });
        }

        private static void ValidateRequiredMethodInputs(ViewDefinition view, SmartObject smartObject)
        {
            if (view.Type != "capture" && view.Type != "capture-list") return;

            var effectiveProperties = new HashSet<string>(view.Properties, StringComparer.OrdinalIgnoreCase);
            if (view.Options.Contains("all-properties", StringComparer.OrdinalIgnoreCase))
                effectiveProperties.UnionWith(smartObject.Properties.Cast<SmartProperty>().Select(x => x.Name));

            var selectedMethods = view.Options.Contains("all-methods", StringComparer.OrdinalIgnoreCase)
                ? smartObject.AllMethods.ToList()
                : smartObject.AllMethods.Where(x => view.Methods.Contains(x.Name, StringComparer.OrdinalIgnoreCase)).ToList();

            foreach (var method in selectedMethods)
            {
                var missing = method.RequiredProperties.Cast<SmartProperty>()
                    .Select(x => x.Name)
                    .Where(x => !effectiveProperties.Contains(x))
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (missing.Count == 0) continue;

                throw new CliException(
                    "View '" + view.Name + "' selects method '" + method.Name + "' on SmartObject '" + view.SmartObject +
                    "' but omits required input properties: " + string.Join(", ", missing.ToArray()) +
                    ". Add them to view.properties, use the all-properties option, or change the SmartObject method contract so those values are supplied outside the generated form. " +
                    "SQL DEFAULT constraints do not make generated K2 method inputs optional.");
            }
        }

        public IList<ArtifactState> GetArtifactStates()
        {
            return WithFormsManager(delegate(FormsManager manager)
            {
                var result = new List<ArtifactState>();
                foreach (var view in _manifest.Application.Views)
                {
                    if (!manager.CheckViewExists(view.Name))
                    {
                        result.Add(ArtifactState.Absent("View", view.Name));
                        continue;
                    }
                    var info = manager.GetView(view.Name);
                    result.Add(new ArtifactState
                    {
                        Kind = "View",
                        Name = info.Name,
                        Exists = true,
                        Guid = info.Guid,
                        CategoryPath = info.CategoryPath,
                        Version = info.Version,
                        CheckedOut = info.IsCheckedOut,
                        Type = info.Type.ToString()
                    });
                }
                foreach (var form in _manifest.Application.Forms)
                {
                    if (!manager.CheckFormExists(form.Name))
                    {
                        result.Add(ArtifactState.Absent("Form", form.Name));
                        continue;
                    }
                    var info = manager.GetForm(form.Name);
                    var useLegacyTheme = FormThemeDefinition.ReadUseLegacyTheme(manager.GetFormDefinition(info.Guid));
                    result.Add(new ArtifactState
                    {
                        Kind = "Form",
                        Name = info.Name,
                        Exists = true,
                        Guid = info.Guid,
                        CategoryPath = info.CategoryPath,
                        Version = info.Version,
                        CheckedOut = info.IsCheckedOut,
                        Type = info.Type.ToString(),
                        UseLegacyTheme = useLegacyTheme
                    });
                }
                return result;
            });
        }

        public IDictionary<string, IList<string>> GetExternalDependencies()
        {
            return WithFormsManager(delegate(FormsManager manager)
            {
                var declaredForms = new HashSet<string>(_manifest.Application.Forms.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
                var result = new Dictionary<string, IList<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var view in _manifest.Application.Views)
                {
                    if (!manager.CheckViewExists(view.Name)) continue;
                    var info = manager.GetView(view.Name);
                    var external = manager.GetFormsForView(info.Guid).Forms.Cast<FormInfo>()
                        .Select(x => x.Name)
                        .Where(x => !declaredForms.Contains(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList();
                    if (external.Count > 0) result[view.Name] = external;
                }
                return result;
            });
        }

        public void Deploy()
        {
            CheckConnectionAndInputs();
            var states = GetArtifactStates();
            var existing = states.Where(x => x.Exists).ToList();
            if (existing.Count > 0 && !_manifest.Application.ReplaceExisting)
                throw new CliException("Artifact(s) already exist and application.replaceExisting is false: " + string.Join(", ", existing.Select(x => x.Kind + " " + x.Name).ToArray()));

            var dependencies = GetExternalDependencies();
            if (dependencies.Count > 0)
            {
                var details = dependencies.Select(x => x.Key + " -> " + string.Join(", ", x.Value.ToArray()));
                throw new CliException("Cannot replace views used by forms outside this manifest: " + string.Join("; ", details.ToArray()));
            }

            WithFormsManager(delegate(FormsManager manager)
            {
                if (existing.Count > 0)
                {
                    foreach (var form in _manifest.Application.Forms)
                    {
                        if (!manager.CheckFormExists(form.Name)) continue;
                        var info = manager.GetForm(form.Name);
                        manager.DeleteForm(info.Guid);
                        Console.WriteLine("Form: removed for replacement (" + form.Name + ", " + info.Guid + ")");
                    }
                    foreach (var view in _manifest.Application.Views)
                    {
                        if (!manager.CheckViewExists(view.Name)) continue;
                        var info = manager.GetView(view.Name);
                        manager.DeleteView(info.Guid);
                        Console.WriteLine("View: removed for replacement (" + view.Name + ", " + info.Guid + ")");
                    }
                }

                using (var generator = new AutoGenerator(manager.Connection))
                {
                    foreach (var view in _manifest.Application.Views)
                    {
                        var viewGenerator = new ViewGenerator(ParseViewType(view.Type), ParseViewOptions(view.Options));
                        if (view.Type == "capture" || view.Type == "capture-list")
                            viewGenerator.InputProperties.AddRange(view.Properties);
                        else
                            viewGenerator.DisplayProperties.AddRange(view.Properties);
                        viewGenerator.InstanceMethods.AddRange(view.Methods);
                        if (!string.IsNullOrWhiteSpace(view.DefaultListMethod))
                            viewGenerator.DefaultListMethod = view.DefaultListMethod;

                        var generated = generator.Generate(viewGenerator, view.SmartObject, view.Name);
                        manager.DeployViews(generated.ToXml(), _manifest.Application.ViewsCategoryPath, _manifest.Application.CheckIn);
                        var info = manager.GetView(view.Name);
                        Console.WriteLine("View: deployed (" + view.Name + ", " + info.Guid + ", " + info.Type + ")");
                    }

                    foreach (var form in _manifest.Application.Forms)
                    {
                        var formGenerator = new FormGenerator(ParseFormOptions(form.Options), ParseFormBehaviors(form.Behaviors), _manifest.Application.Theme);
                        var generated = generator.Generate(formGenerator, form.Views.ToArray(), form.Name);
                        var definition = FormThemeDefinition.SetUseLegacyTheme(generated.ToXml(), form.UseLegacyTheme);
                        manager.DeployForms(definition, _manifest.Application.FormsCategoryPath, _manifest.Application.CheckIn);
                        var info = manager.GetForm(form.Name);
                        Console.WriteLine("Form: deployed (" + form.Name + ", " + info.Guid + ", theme " + info.Theme.Name + ", legacyTheme=" + form.UseLegacyTheme.ToString().ToLowerInvariant() + ")");
                    }
                }
                return 0;
            });
        }

        public void Verify()
        {
            var runtimeForms = new List<string>();
            WithFormsManager(delegate(FormsManager manager)
            {
                foreach (var expected in _manifest.Verification.ExpectedViews)
                {
                    if (!manager.CheckViewExists(expected)) throw new CliException("Expected K2 View is missing: " + expected);
                    var info = manager.GetView(expected);
                    var definition = manager.GetViewDefinition(info.Guid);
                    if (string.IsNullOrWhiteSpace(definition)) throw new CliException("K2 View has an empty definition: " + expected);
                    if (!string.Equals(info.CategoryPath, _manifest.Application.ViewsCategoryPath, StringComparison.OrdinalIgnoreCase))
                        throw new CliException("K2 View is in category '" + info.CategoryPath + "', expected '" + _manifest.Application.ViewsCategoryPath + "': " + expected);
                    if (_manifest.Application.CheckIn && info.IsCheckedOut) throw new CliException("K2 View remains checked out: " + expected);
                    Console.WriteLine("View verification: OK (" + expected + ", " + info.Guid + ", v" + info.Version + ", " + info.Type + ")");
                }

                foreach (var expected in _manifest.Verification.ExpectedForms)
                {
                    if (!manager.CheckFormExists(expected)) throw new CliException("Expected K2 Form is missing: " + expected);
                    var info = manager.GetForm(expected);
                    var definition = manager.GetFormDefinition(info.Guid);
                    if (string.IsNullOrWhiteSpace(definition)) throw new CliException("K2 Form has an empty definition: " + expected);
                    if (!string.Equals(info.CategoryPath, _manifest.Application.FormsCategoryPath, StringComparison.OrdinalIgnoreCase))
                        throw new CliException("K2 Form is in category '" + info.CategoryPath + "', expected '" + _manifest.Application.FormsCategoryPath + "': " + expected);
                    if (_manifest.Application.CheckIn && info.IsCheckedOut) throw new CliException("K2 Form remains checked out: " + expected);
                    var declaredForm = _manifest.Application.Forms.SingleOrDefault(x => string.Equals(x.Name, expected, StringComparison.OrdinalIgnoreCase));
                    if (declaredForm == null) throw new CliException("Expected K2 Form is not declared in application.forms: " + expected);
                    var useLegacyTheme = FormThemeDefinition.ReadUseLegacyTheme(definition);
                    if (!useLegacyTheme.HasValue)
                        throw new CliException("K2 Form does not explicitly set UseLegacyTheme: " + expected);
                    if (useLegacyTheme.Value != declaredForm.UseLegacyTheme)
                        throw new CliException("K2 Form UseLegacyTheme is " + useLegacyTheme.Value.ToString().ToLowerInvariant() + ", expected " + declaredForm.UseLegacyTheme.ToString().ToLowerInvariant() + ": " + expected);
                    foreach (var viewName in declaredForm.Views)
                    {
                        var viewGuid = manager.GetView(viewName).Guid.ToString();
                        if (definition.IndexOf(viewGuid, StringComparison.OrdinalIgnoreCase) < 0)
                            throw new CliException("K2 Form '" + expected + "' does not reference expected view '" + viewName + "'.");
                    }
                    Console.WriteLine("Form verification: OK (" + expected + ", " + info.Guid + ", v" + info.Version + ", theme " + info.Theme.Name + ", legacyTheme=" + useLegacyTheme.Value.ToString().ToLowerInvariant() + ")");
                    runtimeForms.Add(expected);
                }
                return 0;
            });

            if (_manifest.Verification.SmokeTestRuntime)
            {
                foreach (var form in runtimeForms)
                    SmokeTestRuntime(form);
            }
            Console.WriteLine("K2 SmartForms verification: OK (" + _manifest.Verification.ExpectedViews.Count + " view(s), " + _manifest.Verification.ExpectedForms.Count + " form(s))");
        }

        public void Cleanup()
        {
            var dependencies = GetExternalDependencies();
            if (dependencies.Count > 0)
            {
                var details = dependencies.Select(x => x.Key + " -> " + string.Join(", ", x.Value.ToArray()));
                throw new CliException("Cannot delete views used by forms outside this manifest: " + string.Join("; ", details.ToArray()));
            }

            WithFormsManager(delegate(FormsManager manager)
            {
                foreach (var form in _manifest.Application.Forms)
                {
                    if (!manager.CheckFormExists(form.Name))
                    {
                        Console.WriteLine("Form: already absent (" + form.Name + ")");
                        continue;
                    }
                    var info = manager.GetForm(form.Name);
                    manager.DeleteForm(info.Guid);
                    Console.WriteLine("Form: deleted (" + form.Name + ", " + info.Guid + ")");
                }
                foreach (var view in _manifest.Application.Views)
                {
                    if (!manager.CheckViewExists(view.Name))
                    {
                        Console.WriteLine("View: already absent (" + view.Name + ")");
                        continue;
                    }
                    var info = manager.GetView(view.Name);
                    manager.DeleteView(info.Guid);
                    Console.WriteLine("View: deleted (" + view.Name + ", " + info.Guid + ")");
                }
                return 0;
            });
        }

        private void SmokeTestRuntime(string formName)
        {
            var url = _manifest.Verification.RuntimeBaseUrl.TrimEnd('/') + "/Runtime/Form/" + System.Web.HttpUtility.UrlEncode(formName) + "/";
            var stopwatch = Stopwatch.StartNew();
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UseDefaultCredentials = true;
            request.AllowAutoRedirect = false;
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.UserAgent = "k2forms/0.1.2";
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    stopwatch.Stop();
                    var code = (int)response.StatusCode;
                    if (code >= 300 && code < 400)
                    {
                        var location = response.Headers[HttpResponseHeader.Location];
                        if (string.IsNullOrWhiteSpace(location)) throw new CliException("K2 runtime returned a redirect without a location for form " + formName + ".");
                        Console.WriteLine("Runtime route test: OK (" + formName + ", HTTP " + code + " authentication redirect, " + stopwatch.ElapsedMilliseconds + " ms; interactive render skipped)");
                        return;
                    }
                    using (var stream = response.GetResponseStream())
                    {
                        if (stream != null)
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                var content = reader.ReadToEnd();
                                if (content.IndexOf("form could not be found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    content.IndexOf("resource cannot be found", StringComparison.OrdinalIgnoreCase) >= 0)
                                    throw new CliException("K2 runtime reported that form was not found: " + formName);
                            }
                        }
                    }
                    if (code < 200 || code >= 400)
                        throw new CliException("K2 runtime returned HTTP " + code + " for form " + formName + ".");
                    Console.WriteLine("Runtime render smoke test: OK (" + formName + ", HTTP " + code + ", " + stopwatch.ElapsedMilliseconds + " ms)");
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response == null) throw;
                using (response)
                {
                    throw new CliException("K2 runtime returned HTTP " + (int)response.StatusCode + " for form " + formName + ".");
                }
            }
        }

        private T WithFormsManager<T>(Func<FormsManager, T> action)
        {
            var manager = new FormsManager();
            try
            {
                manager.CreateConnection();
                manager.Connection.Open(BuildConnectionString());
                return action(manager);
            }
            finally
            {
                if (manager.Connection != null)
                {
                    manager.Connection.Close();
                    manager.DeleteConnection();
                }
                manager.Dispose();
            }
        }

        private T WithSmartObjectServer<T>(Func<SmartObjectClientServer, T> action)
        {
            var server = new SmartObjectClientServer();
            try
            {
                server.CreateConnection();
                server.Connection.Open(BuildConnectionString());
                return action(server);
            }
            finally
            {
                if (server.Connection != null)
                {
                    server.Connection.Close();
                    server.DeleteConnection();
                }
            }
        }

        private string BuildConnectionString()
        {
            var builder = new SCConnectionStringBuilder
            {
                Authenticate = true,
                Host = _manifest.K2.Host,
                Port = (uint)_manifest.K2.Port,
                Integrated = _manifest.K2.Integrated,
                IsPrimaryLogin = true,
                SecurityLabelName = _manifest.K2.SecurityLabel
            };
            if (!_manifest.K2.Integrated)
            {
                builder.WindowsDomain = _manifest.K2.Domain;
                builder.UserID = _manifest.K2.UserName;
                builder.Password = ReadRequiredEnvironmentVariable(_manifest.K2.PasswordEnvironmentVariable);
                builder.CachePassword = false;
            }
            return builder.ConnectionString;
        }

        private static AuthoringViewType ParseViewType(string value)
        {
            switch (value)
            {
                case "capture": return AuthoringViewType.Capture;
                case "list": return AuthoringViewType.List;
                case "content": return AuthoringViewType.Content;
                case "capture-list": return AuthoringViewType.CaptureList;
                default: throw new CliException("Unsupported view type: " + value);
            }
        }

        private static ViewCreationOption ParseViewOptions(IEnumerable<string> values)
        {
            var result = ViewCreationOption.None;
            foreach (var value in values)
            {
                switch (value.ToLowerInvariant())
                {
                    case "display-controls": result |= ViewCreationOption.FormDisplayControls; break;
                    case "all-properties": result |= ViewCreationOption.UseAllProperties; break;
                    case "all-methods": result |= ViewCreationOption.UseAllInstanceMethods; break;
                    case "labels-left": result |= ViewCreationOption.LabelsToLeftOfControls; break;
                    case "colon-labels": result |= ViewCreationOption.AddColonSuffixToLabels; break;
                    case "toolbar": result |= ViewCreationOption.CreateToolbar; break;
                    case "editable": result |= ViewCreationOption.IsEditable; break;
                }
            }
            return result;
        }

        private static FormGenerationOption ParseFormOptions(IEnumerable<string> values)
        {
            var result = FormGenerationOption.None;
            foreach (var value in values)
                if (value.Equals("no-tabs", StringComparison.OrdinalIgnoreCase)) result |= FormGenerationOption.NoTabs;
            return result;
        }

        private static FormBehaviorOption ParseFormBehaviors(IEnumerable<string> values)
        {
            var result = FormBehaviorOption.None;
            foreach (var value in values)
            {
                switch (value.ToLowerInvariant())
                {
                    case "load-form-list-click": result |= FormBehaviorOption.LoadFormListClick; break;
                    case "refresh-list-form-submit": result |= FormBehaviorOption.RefreshListFormSubmit; break;
                    case "refresh-list-form-load": result |= FormBehaviorOption.RefreshListFormLoad; break;
                }
            }
            return result;
        }

        private static string ReadRequiredEnvironmentVariable(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value)) throw new CliException("Required environment variable is not set: " + name);
            return value;
        }
    }

    internal sealed class ArtifactState
    {
        public string Kind { get; set; }
        public string Name { get; set; }
        public bool Exists { get; set; }
        public Guid Guid { get; set; }
        public string CategoryPath { get; set; }
        public int Version { get; set; }
        public bool CheckedOut { get; set; }
        public string Type { get; set; }
        public bool? UseLegacyTheme { get; set; }

        public static ArtifactState Absent(string kind, string name)
        {
            return new ArtifactState { Kind = kind, Name = name, Exists = false };
        }
    }
}
