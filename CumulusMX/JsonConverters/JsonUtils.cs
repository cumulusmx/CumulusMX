using System;
using System.Text.Json;

namespace CumulusMX.JsonConverters
{
	internal class JsonUtils
	{
		// Add a custom date/time format converter to System.Text.Json
		// pass in the custom format as eg. "yyyy-MM-dd"

		//var options = new JsonSerializerOptions() { WriteIndented = true };
		//options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-dd"));
		//var json = JsonSerializer.Serialize(data, options)

		public class CustomDateTimeConverter(string format) : System.Text.Json.Serialization.JsonConverter<DateTime>
		{
			private readonly string Format = format;

			public override void Write(Utf8JsonWriter writer, DateTime date, JsonSerializerOptions options)
			{
				writer.WriteStringValue(date.ToString(Format));
			}
			public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			{
				return DateTime.ParseExact(reader.GetString(), Format, null);
			}
		}
	}
}
