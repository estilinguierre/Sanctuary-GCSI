using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using MetalForge.Core.Model;

namespace MetalForge.Core.Atomic
{
    /// <summary>
    /// Assistant générique adossé au moteur atomique : toutes les familles du configurateur deviennent
    /// éditables dans une grille de propriétés (repli sans navigateur intégré) et réutilisent aperçu, débit, devis, note de calcul.
    /// </summary>
    public sealed class AtomicDesigner : IDesigner
    {
        private readonly ConfiguratorEngine _engine;
        public ParameterBag Bag { get; }
        public AtomicResult Last { get; private set; }

        public AtomicDesigner(ConfiguratorEngine engine, string family = "garde-corps")
        {
            _engine = engine;
            Bag = new ParameterBag(engine, family);
        }

        public string Title => "Configurateur atomique — " + Bag.Family.Label;
        public object Parameters => Bag;

        public DesignResult Compute()
        {
            Last = _engine.Build(Bag.Family.Id, Bag.Values);
            return Last.Design;
        }
    }

    /// <summary>Sac de propriétés dynamique (ICustomTypeDescriptor) construit à partir du schéma du moteur.</summary>
    public sealed class ParameterBag : ICustomTypeDescriptor
    {
        private readonly ConfiguratorEngine _engine;
        public FamilyInfo Family { get; private set; }
        public Dictionary<string, object> Values { get; } = new Dictionary<string, object>();

        public ParameterBag(ConfiguratorEngine engine, string family)
        {
            _engine = engine;
            SetFamily(family);
        }

        public void SetFamily(string id)
        {
            Family = _engine.Families.FirstOrDefault(f => f.Id == id) ?? _engine.Families[0];
            Values.Clear();
            foreach (var p in Family.Params) Values[p.Key] = p.Default;
        }

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes) => GetProperties();
        public PropertyDescriptorCollection GetProperties()
        {
            var list = new List<PropertyDescriptor> { new FamilyDescriptor(this) };
            list.AddRange(Family.Params.Where(p => p.IsVisible(Values)).Select((p, i) => (PropertyDescriptor)new ParamDescriptor(this, p, i)));
            return new PropertyDescriptorCollection(list.ToArray());
        }

        // ---- ICustomTypeDescriptor : valeurs par défaut
        public AttributeCollection GetAttributes() => AttributeCollection.Empty;
        public string GetClassName() => nameof(ParameterBag);
        public string GetComponentName() => Family.Label;
        public TypeConverter GetConverter() => new TypeConverter();
        public EventDescriptor GetDefaultEvent() => null;
        public PropertyDescriptor GetDefaultProperty() => null;
        public object GetEditor(Type editorBaseType) => null;
        public EventDescriptorCollection GetEvents(Attribute[] attributes) => EventDescriptorCollection.Empty;
        public EventDescriptorCollection GetEvents() => EventDescriptorCollection.Empty;
        public object GetPropertyOwner(PropertyDescriptor pd) => this;

        internal IEnumerable<FamilyInfo> AllFamilies => _engine.Families;

        private sealed class FamilyDescriptor : PropertyDescriptor
        {
            private readonly ParameterBag _bag;
            public FamilyDescriptor(ParameterBag bag) : base("__family", new Attribute[] { new CategoryAttribute("0. Ouvrage"), new DisplayNameAttribute("Ouvrage"), new RefreshPropertiesAttribute(RefreshProperties.All) }) { _bag = bag; }
            public override Type ComponentType => typeof(ParameterBag);
            public override bool IsReadOnly => false;
            public override Type PropertyType => typeof(string);
            public override bool CanResetValue(object c) => false;
            public override object GetValue(object c) => _bag.Family.ToString();
            public override void ResetValue(object c) { }
            public override void SetValue(object c, object v) { var f = _bag.AllFamilies.FirstOrDefault(x => x.ToString() == (string)v); if (f != null) _bag.SetFamily(f.Id); }
            public override bool ShouldSerializeValue(object c) => false;
            public override TypeConverter Converter => new Choices(_bag.AllFamilies.Select(f => f.ToString()).ToArray());
        }

        private sealed class ParamDescriptor : PropertyDescriptor
        {
            private readonly ParameterBag _bag; private readonly ParamInfo _p;
            public ParamDescriptor(ParameterBag bag, ParamInfo p, int i)
                : base(p.Key, new Attribute[] { new CategoryAttribute(p.Group), new DisplayNameAttribute(p.Label + (string.IsNullOrEmpty(p.Unit) ? "" : " (" + p.Unit + ")")),
                                                new RefreshPropertiesAttribute(p.Type == "sel" || p.Type == "bool" ? RefreshProperties.All : RefreshProperties.None) })
            { _bag = bag; _p = p; }
            public override Type ComponentType => typeof(ParameterBag);
            public override bool IsReadOnly => false;
            public override Type PropertyType => _p.Type == "num" ? typeof(double) : _p.Type == "bool" ? typeof(bool) : typeof(string);
            public override bool CanResetValue(object c) => true;
            public override void ResetValue(object c) => _bag.Values[_p.Key] = _p.Default;
            public override bool ShouldSerializeValue(object c) => !Equals(_bag.Values[_p.Key], _p.Default);
            public override object GetValue(object c)
            {
                var v = _bag.Values[_p.Key];
                if (_p.Type == "num") return Convert.ToDouble(v, CultureInfo.InvariantCulture);
                if (_p.Type == "bool") return v is bool b && b;
                if (_p.Type == "sel") return _p.Options.FirstOrDefault(o => o.Key == Convert.ToString(v, CultureInfo.InvariantCulture)).Value ?? Convert.ToString(v, CultureInfo.InvariantCulture);
                return Convert.ToString(v, CultureInfo.InvariantCulture);
            }
            public override void SetValue(object c, object v)
            {
                if (_p.Type == "sel") { var o = _p.Options.FirstOrDefault(x => x.Value == (string)v); _bag.Values[_p.Key] = o.Key ?? v; }
                else _bag.Values[_p.Key] = v;
            }
            public override TypeConverter Converter => _p.Type == "sel" ? new Choices(_p.Options.Select(o => o.Value).ToArray(), true)
                                                     : _p.Type == "prof" ? new Choices(Profiles.ProfileCatalog.All.Select(x => x.Id).OrderBy(x => x).ToArray(), false)
                                                     : base.Converter;
        }

        /// <summary>Liste déroulante (exclusive pour les choix, libre pour les profilés).</summary>
        private sealed class Choices : StringConverter
        {
            private readonly string[] _values; private readonly bool _exclusive;
            public Choices(string[] values, bool exclusive = true) { _values = values; _exclusive = exclusive; }
            public override bool GetStandardValuesSupported(ITypeDescriptorContext c) => true;
            public override bool GetStandardValuesExclusive(ITypeDescriptorContext c) => _exclusive;
            public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext c) => new StandardValuesCollection(_values);
        }
    }
}
