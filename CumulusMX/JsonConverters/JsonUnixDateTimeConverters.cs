using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CumulusMX.JsonConverters
{
	internal class UnixDateTimeConverter : UnixDateTimeConverterBase<DateTime>
	{
		public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			long timestamp = reader.GetInt64();
			return FromUnixTime(timestamp);
		}

		public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
		{
			writer.WriteNumberValue(ToUnixTime(value));
		}
	}

	internal class NullableUnixDateTimeConverter : UnixDateTimeConverterBase<DateTime?>
	{
		public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			long timestamp = reader.GetInt64(); // Assumes timestamp is in seconds
			return FromUnixTime(timestamp);
		}

		public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
		{
			if (value.HasValue)
			{
				writer.WriteNumberValue(ToUnixTime(value.Value));
			}
			else
			{
				writer.WriteNullValue();
			}
		}
	}

	internal abstract class UnixDateTimeConverterBase<T> : JsonConverter<T>
	{
		// Convert a Unix Timestamp to the local DateTime
		protected static DateTime FromUnixTime(long seconds)
		{
			return DateTime.UnixEpoch.AddSeconds(seconds).ToLocalTime();
		}

		// Convert local DateTime to Unix Timestamp
		protected static long ToUnixTime(DateTime dateTime)
		{
			return (long) (dateTime.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds;
		}
	}
}
