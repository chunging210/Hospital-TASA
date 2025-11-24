using System.Text.Json;
using System.Text.Json.Serialization;

namespace TASA.Program.JsonConverters
{
    public class NullableConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var underlyingType = Nullable.GetUnderlyingType(typeToConvert)!;
            var converterType = typeof(NullableConverter<>).MakeGenericType(underlyingType);
            return (JsonConverter?)Activator.CreateInstance(converterType);
        }
    }
}
