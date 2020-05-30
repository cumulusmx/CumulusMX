using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using UnitsNet.Serialization.JsonNet;
using UnitsNet;

namespace CumulusMX
{
    public class ExtendedJsonConverter : UnitsNetBaseJsonConverter<IQuantity>
    {
        private UnitsNetIQuantityJsonConverter _innerConverter;

        public ExtendedJsonConverter()
        {
            _innerConverter = new UnitsNetIQuantityJsonConverter();
        }

        public override IQuantity ReadJson(JsonReader reader, Type objectType, [AllowNull] IQuantity existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (objectType == typeof(Number))
            {
                double theValue = 0.0;
                reader.Read();
                while (reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType == JsonToken.PropertyName && (string)reader.Value == "Value")
                    {
                        reader.Read();
                        theValue = (double)reader.Value;
                    }
                    reader.Read();
                }
                return new Number(theValue,UnitsNet.Units.NumberUnit.Value);
            }
            else
                return _innerConverter.ReadJson(reader,objectType, existingValue, hasExistingValue, serializer);
        }

        public override void WriteJson(JsonWriter writer, [AllowNull] IQuantity value, JsonSerializer serializer)
        {
            if (value.GetType() == typeof(Number))
            //{"Unit":"NumberUnit.Value","Value":0.0}
            {    
                writer.WriteStartObject();
                writer.WritePropertyName("Unit");
                writer.WriteValue("NumberUnit.Value");
                writer.WritePropertyName("Value");
                writer.WriteValue(value.Value);
                writer.WriteEndObject();
            }
            else
                _innerConverter.WriteJson(writer,value,serializer);
        }
    }
}
