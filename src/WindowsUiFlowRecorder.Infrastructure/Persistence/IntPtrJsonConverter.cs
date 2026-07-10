namespace WindowsUiFlowRecorder.Infrastructure.Persistence;

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class IntPtrJsonConverter : JsonConverter<IntPtr>
{
    public override IntPtr Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var value))
            return new IntPtr(value);

        if (reader.TokenType == JsonTokenType.String && long.TryParse(reader.GetString(), out var parsed))
            return new IntPtr(parsed);

        return IntPtr.Zero;
    }

    public override void Write(Utf8JsonWriter writer, IntPtr value, JsonSerializerOptions options)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            writer.WriteNumberValue(value.ToInt64());
        }
        else
        {
            writer.WriteNumberValue(value.ToInt64());
        }
    }
}
