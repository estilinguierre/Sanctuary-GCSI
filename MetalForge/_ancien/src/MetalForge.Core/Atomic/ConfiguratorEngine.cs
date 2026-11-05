using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jint;

namespace MetalForge.Core.Atomic
{
    /// <summary>
    /// Moteur de configuration atomique : exécute <c>engine.js</c> (le même fichier que le configurateur web)
    /// avec l'interpréteur JavaScript Jint. Une seule source de vérité pour le web et SolidWorks.
    /// </summary>
    public sealed class ConfiguratorEngine
    {
        private static readonly Lazy<string> Script = new Lazy<string>(() =>
        {
            using (var s = typeof(ConfiguratorEngine).Assembly.GetManifestResourceStream("MetalForge.Core.engine.js"))
            {
                if (s == null) throw new InvalidOperationException("engine.js introuvable dans les ressources de MetalForge.Core.");
                using (var r = new StreamReader(s)) return r.ReadToEnd();
            }
        });

        private readonly Engine _js;
        private readonly object _lock = new object();

        public ConfiguratorEngine()
        {
            _js = new Engine(o => o.LimitRecursion(1024).TimeoutInterval(TimeSpan.FromSeconds(30)));
            _js.Execute(Script.Value);
            Version = _js.Evaluate("MetalForgeEngine.version").AsString();
            Families = ParseSchema(_js.Evaluate("MetalForgeEngine.schemaJson()").AsString());
        }

        public string Version { get; }
        public IReadOnlyList<FamilyInfo> Families { get; }

        /// <summary>Construit un ouvrage ; <paramref name="paramsJson"/> peut ne contenir que les paramètres modifiés.</summary>
        public string BuildJson(string family, string paramsJson = "{}")
        {
            lock (_lock)
            {
                _js.SetValue("__fam", family);
                _js.SetValue("__p", string.IsNullOrWhiteSpace(paramsJson) ? "{}" : paramsJson);
                return _js.Evaluate("MetalForgeEngine.buildJson(__fam, __p)").AsString();
            }
        }

        public AtomicResult Build(string family, IDictionary<string, object> parameters = null) =>
            AtomicResult.FromJson(BuildJson(family, ToJson(parameters)), this);

        public string DxfFromResultJson(string resultJson)
        {
            lock (_lock) { _js.SetValue("__r", resultJson); return _js.Evaluate("MetalForgeEngine.dxfJson(__r)").AsString(); }
        }

        public string CsvFromResultJson(string resultJson)
        {
            lock (_lock) { _js.SetValue("__r", resultJson); return _js.Evaluate("MetalForgeEngine.csvJson(__r)").AsString(); }
        }

        /// <summary>Masse linéique (kg/m) d'un profilé selon le moteur JavaScript.</summary>
        public double ProfileKgPerMeter(string id)
        {
            lock (_lock) { _js.SetValue("__id", id); return _js.Evaluate("MetalForgeEngine.profile(__id).kgm").AsNumber(); }
        }

        /// <summary>Analyse un JSON quelconque en objets .NET (dictionnaires, tableaux, double, bool, string).</summary>
        public object ParseJson(string json)
        {
            lock (_lock) { _js.SetValue("__j", json); return _js.Evaluate("JSON.parse(__j)").ToObject(); }
        }

        public string ToJson(object value)
        {
            if (value == null) return "{}";
            lock (_lock) { _js.SetValue("__v", value); return _js.Evaluate("JSON.stringify(__v)").AsString(); }
        }

        private IReadOnlyList<FamilyInfo> ParseSchema(string json)
        {
            _js.SetValue("__j", json);
            var arr = (object[])_js.Evaluate("JSON.parse(__j)").ToObject();
            return arr.Cast<IDictionary<string, object>>().Select(FamilyInfo.From).ToList();
        }
    }

    /// <summary>Description d'un ouvrage du configurateur (schéma de paramètres).</summary>
    public sealed class FamilyInfo
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string Label { get; set; }
        public List<ParamInfo> Params { get; } = new List<ParamInfo>();
        public override string ToString() => Category + " › " + Label;

        internal static FamilyInfo From(IDictionary<string, object> d)
        {
            var f = new FamilyInfo { Id = (string)d["id"], Category = (string)d["cat"], Label = (string)d["label"] };
            foreach (IDictionary<string, object> p in (object[])d["params"])
            {
                var q = new ParamInfo
                {
                    Key = (string)p["k"], Label = (string)p["l"], Type = (string)p["t"], Default = p["d"], Group = (string)p["g"],
                    Unit = p.TryGetValue("u", out var u) ? (string)u : null, ProfileList = p.TryGetValue("list", out var l) ? (string)l : null
                };
                if (p.TryGetValue("o", out var o))
                    foreach (object[] opt in (object[])o) q.Options.Add(new KeyValuePair<string, string>(Convert.ToString(opt[0], System.Globalization.CultureInfo.InvariantCulture), (string)opt[1]));
                if (p.TryGetValue("show", out var sh)) q.ShowIf = ((object[])sh).ToArray();
                if (p.TryGetValue("auto", out var au) && au != null) { q.AutoRule = Convert.ToString(au, System.Globalization.CultureInfo.InvariantCulture); q.Options.Add(new KeyValuePair<string, string>("__auto", "Auto — le plus léger conforme")); }
                if (p.TryGetValue("groups", out var gr) && gr is object[] groups)
                    foreach (IDictionary<string, object> g in groups)
                    {
                        var gname = (string)g["g"]; q.Groups.Add(gname);
                        foreach (object[] opt in (object[])g["o"]) q.Options.Add(new KeyValuePair<string, string>((string)opt[0], gname + " › " + (string)opt[1]));
                    }
                f.Params.Add(q);
            }
            return f;
        }
    }

    public sealed class ParamInfo
    {
        public string Key, Label, Type, Group, Unit, ProfileList;
        public object Default;
        public object[] ShowIf;
        public string AutoRule;
        public List<string> Groups { get; } = new List<string>();
        public List<KeyValuePair<string, string>> Options { get; } = new List<KeyValuePair<string, string>>();

        public bool IsVisible(IDictionary<string, object> values)
        {
            if (ShowIf == null || ShowIf.Length < 2) return true;
            values.TryGetValue((string)ShowIf[0], out var v);
            return ShowIf.Skip(1).Any(x => Equals(x, v) || string.Equals(Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture),
                                                                   Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase));
        }
    }
}
