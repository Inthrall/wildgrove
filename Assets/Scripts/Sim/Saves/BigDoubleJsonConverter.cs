using System;
using System.Globalization;
using BreakInfinity;
using Newtonsoft.Json;

namespace Wildgrove.Sim.Saves
{
    /// <summary>
    /// Writes a <see cref="BigDouble"/> as "&lt;mantissa&gt;e&lt;exponent&gt;"
    /// (e.g. "1.5e42") using the round-trip double format, so magnitudes past
    /// double range survive the JSON save exactly. The normalised mantissa
    /// lives in [1, 10) and so never carries its own exponent marker — the
    /// last 'e' always splits the pair.
    /// </summary>
    public sealed class BigDoubleJsonConverter : JsonConverter<BigDouble>
    {
        public override void WriteJson(JsonWriter writer, BigDouble value, JsonSerializer serializer)
        {
            writer.WriteValue(
                value.Mantissa.ToString("R", CultureInfo.InvariantCulture)
                + "e" + value.Exponent.ToString(CultureInfo.InvariantCulture));
        }

        public override BigDouble ReadJson(JsonReader reader, Type objectType, BigDouble existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return BigDouble.Zero;
            }

            var text = Convert.ToString(reader.Value, CultureInfo.InvariantCulture);
            var split = text?.LastIndexOf('e') ?? -1;
            if (split <= 0)
            {
                throw new JsonSerializationException(
                    "Malformed BigDouble '" + text + "' — expected '<mantissa>e<exponent>'.");
            }

            var mantissa = double.Parse(text.Substring(0, split), NumberStyles.Float, CultureInfo.InvariantCulture);
            var exponent = long.Parse(text.Substring(split + 1), NumberStyles.Integer, CultureInfo.InvariantCulture);
            return new BigDouble(mantissa, exponent);
        }
    }
}
