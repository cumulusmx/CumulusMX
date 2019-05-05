using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Antlr4.StringTemplate;
using Antlr4.StringTemplate.Misc;
using CumulusMX.Common.ExtensionMethods;
using UnitsNet;

namespace CumulusMX.Common.StringTemplate
{
    public class ExtendedObjectModelAdaptor : IModelAdaptor
    {
        public ExtendedObjectModelAdaptor()
        {
            _standardAdapter = new ObjectModelAdaptor();
        }
        private static readonly Dictionary<Type, Type> ExtensionTypes = new Dictionary<Type, Type>()
        {
            {typeof(Angle),typeof(AngleExtensions)},
            {typeof(Speed),typeof(SpeedExtensions)}
        };

        private static readonly Dictionary<Tuple<Type,string>,MethodInfo> _cache = new Dictionary<Tuple<Type, string>, MethodInfo>();
        private readonly ObjectModelAdaptor _standardAdapter;

        public virtual object GetProperty(Interpreter interpreter, TemplateFrame frame, object o, object property, string propertyName)
        {
            if (o == null)
                throw new ArgumentNullException("o");

            var tuple = new Tuple<Type, string>(o.GetType(), property.ToString());
            if (_cache.ContainsKey(tuple))
                return _cache[tuple].Invoke(null, new[] { o });

            if (ExtensionTypes.ContainsKey(o.GetType()) && !string.IsNullOrWhiteSpace(propertyName))
            {
                var methods = ExtensionTypes[o.GetType()].GetMethods();
                var method = methods.FirstOrDefault(x => x.Name == propertyName && x.IsStatic);
                if (method != null)
                {
                    _cache.Add(tuple,method);
                    return method.Invoke(null, new[]{o});
                }
            }

            var result = _standardAdapter.GetProperty(interpreter, frame, o, property, propertyName);
            return result;
        }

    }
}
