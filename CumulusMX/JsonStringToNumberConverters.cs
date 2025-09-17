using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace CumulusMX
{
	internal class JsonIntConverter : JsonConverter<int>
	{
		public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				string stringValue = reader.GetString();
				if (int.TryParse(stringValue, out int value))
				{
					return value;
				}
			}
			else if (reader.TokenType == JsonTokenType.Number)
			{
				return reader.GetInt32();
			}

			throw new JsonException();
		}

		public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
		{
			writer.WriteNumberValue(value);
		}
	}

	internal class JsonNullIntConverter : JsonConverter<int?>
	{
		public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				string stringValue = reader.GetString();
				if (string.IsNullOrEmpty(stringValue))
				{
					return null;
				}
				else if (int.TryParse(stringValue, out int value))
				{
					return value;
				}
				else
				{
					return null;
				}
			}
			else if (reader.TokenType == JsonTokenType.Number)
			{
				return reader.GetInt32();
			}

			throw new JsonException();
		}

		public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
		{
			if (value.HasValue)
			{
				writer.WriteNumberValue(value.Value);
			}
			else
			{
				writer.WriteNullValue();
			}
		}
	}

	internal class JsonLongConverter : JsonConverter<long>
	{
		public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				string stringValue = reader.GetString();
				if (long.TryParse(stringValue, out long value))
				{
					return value;
				}
			}
			else if (reader.TokenType == JsonTokenType.Number)
			{
				return reader.GetInt64();
			}

			throw new JsonException();
		}

		public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
		{
			writer.WriteNumberValue(value);
		}
	}

	internal class JsonNullLongConverter : JsonConverter<long?>
	{
		public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				string stringValue = reader.GetString();
				if (string.IsNullOrEmpty(stringValue))
				{
					return null;
				}
				else if (long.TryParse(stringValue, out long value))
				{
					return value;
				}
				else
				{
					return null;
				}
			}
			else if (reader.TokenType == JsonTokenType.Number)
			{
				return reader.GetInt64();
			}

			throw new JsonException();
		}

		public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
		{
			if (value.HasValue)
			{
				writer.WriteNumberValue(value.Value);
			}
			else
			{
				writer.WriteNullValue();
			}
		}
	}

	internal class JsonDoubleConverter : JsonConverter<double>
	{
		public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				string stringValue = reader.GetString();
				if (double.TryParse(stringValue, CultureInfo.InvariantCulture, out double value))
				{
					return value;
				}
			}
			else if (reader.TokenType == JsonTokenType.Number)
			{
				return reader.GetDouble();
			}

			throw new JsonException();
		}

		public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
		{
			writer.WriteNumberValue(value);
		}
	}

	internal class JsonNullDoubleConverter : JsonConverter<double?>
	{
		public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				string stringValue = reader.GetString();
				if (string.IsNullOrEmpty(stringValue))
				{
					return null;
				}
				else if (double.TryParse(stringValue, CultureInfo.InvariantCulture, out double value))
				{
					return value;
				}
				else
				{
					return null;
				}
			}
			else if (reader.TokenType == JsonTokenType.Number)
			{
				return reader.GetDouble();
			}

			throw new JsonException();
		}

		public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
		{
			if (value.HasValue)
			{
				writer.WriteNumberValue(value.Value);
			}
			else
			{
				writer.WriteNullValue();
			}
		}
	}

	internal class JsonDecimalConverter : JsonConverter<decimal>
	{
		public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				string stringValue = reader.GetString();
				if (decimal.TryParse(stringValue, CultureInfo.InvariantCulture, out decimal value))
				{
					return value;
				}
			}
			else if (reader.TokenType == JsonTokenType.Number)
			{
				return reader.GetDecimal();
			}

			throw new JsonException();
		}

		public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
		{
			writer.WriteNumberValue(value);
		}
	}

	internal class JsonNullDecimalConverter : JsonConverter<decimal?>
	{
		public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				string stringValue = reader.GetString();
				if (string.IsNullOrEmpty(stringValue))
				{
					return null;
				}
				else if (decimal.TryParse(stringValue, CultureInfo.InvariantCulture, out decimal value))
				{
					return value;
				}
				else
				{
					return null;
				}
			}
			else if (reader.TokenType == JsonTokenType.Number)
			{
				return reader.GetDecimal();
			}

			throw new JsonException();
		}

		public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
		{
			if (value.HasValue)
			{
				writer.WriteNumberValue(value.Value);
			}
			else
			{
				writer.WriteNullValue();
			}
		}
	}


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

}
