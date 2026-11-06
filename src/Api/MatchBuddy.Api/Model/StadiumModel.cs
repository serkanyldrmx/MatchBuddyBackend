using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MatchBuddy.Api.Model
{
    public class StadiumModel
    {
        public string? StadiumName { get; set; }
        public string? Location { get; set; }
        // İl / ilçe artık metin. Eski admin sayfası sayı (il/ilçe id) gönderebilir: string olarak kabul edilir.
        [JsonConverter(typeof(NumberOrStringConverter))]
        public string? City { get; set; }
        [JsonConverter(typeof(NumberOrStringConverter))]
        public string? District { get; set; }
        public string? Address { get; set; }
        public TimeOnly? OpeningTime { get; set; }
        public TimeOnly? ClosingTime { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>JSON sayı ya da metin -> string.</summary>
    public class NumberOrStringConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString(CultureInfo.InvariantCulture)
                                        : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
                JsonTokenType.String => reader.GetString(),
                _ => throw new JsonException("Metin ya da sayı bekleniyordu."),
            };

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value == null) writer.WriteNullValue();
            else writer.WriteStringValue(value);
        }
    }
}
