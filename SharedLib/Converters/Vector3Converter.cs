using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Numerics;

namespace SharedLib.Converters
{
    public class Vector3Converter : JsonConverter<Vector3>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(Vector3);
        }

        public override Vector3 Read(ref Utf8JsonReader reader,
            Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            Vector3 result = new();

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
                }
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer,
            Vector3 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", value.X);
            writer.WriteNumber("y", value.Y);
            writer.WriteNumber("z", value.Z);
            writer.WriteEndObject();
        }
    }
}
