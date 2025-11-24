using System.Text.Json;
using System.Text.Json.Serialization;

namespace TASA.Program.JsonConverters
{
    public class DateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            DateTime result;
            if (reader.TokenType == JsonTokenType.Number)
            {
                // 處理 JSON 為數字的情況
                result = DateTime.Parse(reader.GetDouble().ToString());
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                // 處理 JSON 為字串的情況
                result = DateTime.Parse(reader.GetString()!);
            }
            else
            {
                throw new FormatException("無法轉換為有效的DateTime。");
            }
            return result.ToUniversalTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
