using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Numerics;

namespace SharedLib.Converters
{
    public class Vector4Converter : JsonConverter<Vector4>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(Vector4);
        }

        public override Vector4 Read(ref Utf8JsonReader reader,
            Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            Vector4 result = new();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return result;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                switch (reader.GetString())
                {
                    case "x":
                    case "X":
                        reader.Read();
                        result.X = reader.GetSingle();
                        break;
                    case "y":
                    case "Y":
                        reader.Read();
                        result.Y = reader.GetSingle();
                        break;
                    case "z":
                    case "Z":
                        reader.Read();
                        result.Z = reader.GetSingle();
                        break;
                    case "w":
                    case "W":
                        reader.Read();
                        result.Z = reader.GetSingle();
                        break;
                }
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer,
            Vector4 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", value.X);
            writer.WriteNumber("y", value.Y);
            writer.WriteNumber("z", value.Z);
            writer.WriteNumber("w", value.W);
            writer.WriteEndObject();
        }
    }
}
