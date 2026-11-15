using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Telaelus.Sim;

/// <summary>A save's header: what the load list shows without loading the game.</summary>
public sealed class SaveHeader
{
    public int FormatVersion { get; set; } = SaveFormat.Version;
    public string GameVersion { get; set; } = "";
    /// <summary>When it was saved, as an ISO 8601 local time.</summary>
    public string SavedAt { get; set; } = "";
    public double PlayTimeSeconds { get; set; }
    public int Seed { get; set; }
    public int ShipsLaunched { get; set; }
    public int Buildings { get; set; }
}

/// <summary>A whole save file: its header, the sim's state, and the presentation's own part (pins, zoom).</summary>
public sealed class SaveFile
{
    public SaveHeader Header { get; set; } = new();
    public SaveData Sim { get; set; } = new();
    public JsonObject? Presentation { get; set; }
}

/// <summary>Why a save cannot be read.</summary>
public sealed class SaveFormatException : Exception
{
    public SaveFormatException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>
/// The save file: gzip-compressed JSON of a <see cref="SaveFile"/>, enums by name. <see cref="Version"/>
/// is the format this build writes and reads; a newer one is refused ("Saved by a newer version").
/// </summary>
public static class SaveFormat
{
    public const int Version = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public static byte[] Write(SaveFile file)
    {
        using var memory = new MemoryStream();
        using (var zip = new GZipStream(memory, CompressionLevel.Optimal, leaveOpen: true))
        {
            JsonSerializer.Serialize(zip, file, Options);
        }
        return memory.ToArray();
    }

    /// <summary>Reads a save; throws <see cref="SaveFormatException"/> for a damaged file or a newer format.</summary>
    public static SaveFile Read(byte[] bytes)
    {
        SaveFile? file;
        try
        {
            using var memory = new MemoryStream(bytes);
            using var zip = new GZipStream(memory, CompressionMode.Decompress);
            file = JsonSerializer.Deserialize<SaveFile>(zip, Options);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or NotSupportedException)
        {
            throw new SaveFormatException("The save is damaged", ex);
        }
        if (file is null || file.Sim is null || file.Header is null)
        {
            throw new SaveFormatException("The save is damaged");
        }
        if (file.Header.FormatVersion > Version || file.Sim.FormatVersion > Version)
        {
            throw new SaveFormatException("Saved by a newer version");
        }
        return file;
    }

    /// <summary>The JSON of a sim's state, uncompressed (tests compare these).</summary>
    public static string Json(SaveData data)
    {
        return JsonSerializer.Serialize(data, Options);
    }
}
