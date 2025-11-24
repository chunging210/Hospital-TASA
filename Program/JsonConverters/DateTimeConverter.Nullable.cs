using System.Text.Json;
using System.Text.Json.Serialization;

namespace TASA.Program.JsonConverters
{
    public class NullableDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            DateTime? result = null;

            try
            {
                result = new DateTimeConverter().Read(ref reader, typeToConvert, options);
            }
            catch
            {
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
