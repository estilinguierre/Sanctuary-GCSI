using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace MetalForge.Core.Model
{
    /// <summary>Affiche les libellés [Description] des énumérations dans les grilles de propriétés (en français).</summary>
    public sealed class EnumDescriptionConverter : EnumConverter
    {
        private readonly Type _type;
        public EnumDescriptionConverter(Type type) : base(type) { _type = type; }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) =>
            destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value != null && value.GetType() == _type)
            {
                var f = _type.GetField(value.ToString());
                var d = f?.GetCustomAttribute<DescriptionAttribute>();
                return d?.Description ?? value.ToString();
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string s)
            {
                var f = _type.GetFields(BindingFlags.Public | BindingFlags.Static)
                             .FirstOrDefault(x => x.GetCustomAttribute<DescriptionAttribute>()?.Description == s || x.Name == s);
                if (f != null) return f.GetValue(null);
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
