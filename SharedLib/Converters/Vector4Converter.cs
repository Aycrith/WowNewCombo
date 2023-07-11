using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Numerics;

namespace SharedLib.Converters;

public sealed class Vector4Converter : JsonConverter<Vector4>
{
    private const string X = "x";
    private const string Y = "y";
    private const string Z = "z";
    private const string W = "w";

    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(Vector4);
    }

    public override Vector4 Read(ref Utf8JsonReader reader,
        Type typeToConvert, JsonSerializerOptions options)
    {
        JsonDocument doc = JsonDocument.ParseValue(ref reader);
        JsonElement root = doc.RootElement;

        float x = root.GetProperty(X).GetSingle();
        float y = root.GetProperty(Y).GetSingle();
        float z = root.GetProperty(Z).GetSingle();
        float w = root.GetProperty(W).GetSingle();

        return new Vector4(x, y, z, w);
    }

    public override void Write(Utf8JsonWriter writer,
        Vector4 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(X, value.X);
        writer.WriteNumber(Y, value.Y);
        writer.WriteNumber(Z, value.Z);
        writer.WriteNumber(W, value.W);
        writer.WriteEndObject();
    }
}
