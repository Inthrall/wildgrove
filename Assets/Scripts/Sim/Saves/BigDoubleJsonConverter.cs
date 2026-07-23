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
            // A NaN/Infinity mantissa (a sim bug poisoned the value) would
            // serialise fine but fail ReadJson's TryParse on next launch,
            // condemning the whole save as corrupt. Write zero instead: one
            // poisoned stat resets rather than the run being set aside.
            var mantissa = value.Mantissa;
            if (double.IsNaN(mantissa) || double.IsInfinity(mantissa))
            {
                writer.WriteValue("0e0");
                return;
            }

            // G17 guarantees an exact double round-trip; "R" does not on all
            // .NET/Mono runtimes, so a mantissa could drift by a ULP per save.
            writer.WriteValue(
                mantissa.ToString("G17", CultureInfo.InvariantCulture)
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

            // TryParse, not Parse: FormatException/OverflowException are not
            // JsonExceptions, so they would sail past FromJson's catch and
            // turn a corrupt save into a launch crash loop instead of a
            // set-aside .corrupt file.
            if (!double.TryParse(text.Substring(0, split), NumberStyles.Float, CultureInfo.InvariantCulture, out var mantissa)
                || !long.TryParse(text.Substring(split + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var exponent))
            {
                throw new JsonSerializationException(
                    "Malformed BigDouble '" + text + "' — expected '<mantissa>e<exponent>'.");
            }

            return new BigDouble(mantissa, exponent);
        }
    }
}
