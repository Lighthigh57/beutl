﻿using System.Text.Json;
using System.Text.Json.Serialization;

using BEditorNext.Media;

namespace BEditorNext.JsonConverters;

internal class PixelPointConverter : JsonConverter<PixelPoint>
{
    public override PixelPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? s = reader.GetString();
        if (s == null)
            throw new Exception("Invalid PixelPoint.");

        return PixelPoint.Parse(s);
    }

    public override void Write(Utf8JsonWriter writer, PixelPoint value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
