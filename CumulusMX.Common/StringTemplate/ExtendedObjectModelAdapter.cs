using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Antlr4.StringTemplate;
using Antlr4.StringTemplate.Misc;
using CumulusMX.Common.ExtensionMethods;
using UnitsNet;

namespace CumulusMX.Common.StringTemplate
{
    public class ExtendedObjectModelAdapter : ObjectModelAdaptor
    {
        private static readonly Dictionary<Type, Type> ExtensionTypes = new Dictionary<Type, Type>()
        {
            {typeof(Angle),typeof(AngleExtensions)},
            {typeof(Speed),typeof(SpeedExtensions)}
        };

        public new object GetProperty(Interpreter interpreter, TemplateFrame frame, object o, object property,
            string propertyName)
        {
            if (o == null)
                throw new ArgumentNullException("o");

            if (ExtensionTypes.ContainsKey(o.GetType()) && property != null)
            {
                var method = o.GetType().GetMethod(property.ToString(),BindingFlags.Static);
                if (method != null)
                {
                    return method.Invoke(null, new[]{o});
                }
            }

            return base.GetProperty(interpreter, frame, o, property, propertyName);
        }

    }
}
