using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CumulusMX.JsonConverters
{
	internal class JsonBoolConverter : JsonConverter<bool>
	{
		public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				string stringValue = reader.GetString();
				if (stringValue == "1")
				{
					return true;
				}
				else if (stringValue == "0")
				{
					return false;
				}
			}
			else if (reader.TokenType == JsonTokenType.False)
			{
				return false;
			}
			else if (reader.TokenType == JsonTokenType.True)
			{
				return true;
			}

			throw new JsonException();
		}

		public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
		{
			writer.WriteBooleanValue(value);
		}
	}

	internal class JsonNullBoolConverter : JsonConverter<bool?>
	{
		public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				string stringValue = reader.GetString();
				if (string.IsNullOrEmpty(stringValue))
				{
					return null;
				}

				if (stringValue == "1")
				{
					return true;
				}
				else if (stringValue == "0")
				{
					return false;
				}

				return null;
			}
			else if (reader.TokenType == JsonTokenType.False)
			{
				return false;
			}
			else if (reader.TokenType == JsonTokenType.True)
			{
				return true;
			}

			throw new JsonException();
		}

		public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
		{
			if (value.HasValue)
			{
				writer.WriteBooleanValue(value.Value);
			}
			else
			{
				writer.WriteNullValue();
			}
		}
	}

}
