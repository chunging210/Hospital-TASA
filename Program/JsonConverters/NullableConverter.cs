using System.Text.Json;
using System.Text.Json.Serialization;

namespace TASA.Program.JsonConverters
{
    class NullableConverter<T> : JsonConverter<T?> where T : struct
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            T? result = null;

            try
            {
                // 處理 JSON 為數字的情況
                if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
                {
                    result = (T?)Convert.ChangeType(reader.GetBoolean(), typeof(T));
                }
                // 處理 JSON 為數字的情況
                else if (reader.TokenType == JsonTokenType.Number)
                {
                    result = (T?)Convert.ChangeType(reader.GetDouble(), typeof(T));
                }
                // 處理 JSON 為字串的情況
                else if (reader.TokenType == JsonTokenType.String)
                {
                    string? stringValue = reader.GetString();
                    if (!string.IsNullOrEmpty(stringValue))
                    {
                        if (typeof(T) == typeof(DateTime))
                        {
                            if (DateTime.TryParse(stringValue, out var dateTimeResult))
                            {
                                return (T?)Convert.ChangeType(dateTimeResult, typeof(T));
                            }
                        }
                        else if (typeof(T) == typeof(Guid))
                        {
                            if (Guid.TryParse(stringValue, out var guidResult))
                            {
                                return (T?)Convert.ChangeType(guidResult, typeof(T));
                            }
                        }
                        else
                        {
                            // 其他類型，使用 Convert.ChangeType
                            return (T?)Convert.ChangeType(stringValue, typeof(T));
                        }
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            if (typeof(T) == typeof(bool))
            {
                writer.WriteBooleanValue(Convert.ToBoolean(value.Value));
            }
            else if (typeof(T) == typeof(string))
            {
                writer.WriteStringValue(value.Value.ToString());
            }
            else if (typeof(T) == typeof(int) || typeof(T) == typeof(double))
            {
                writer.WriteNumberValue(Convert.ToDouble(value.Value));
            }
            else
            {
                JsonSerializer.Serialize(writer, value.Value, options);
            }
        }
    }
}
