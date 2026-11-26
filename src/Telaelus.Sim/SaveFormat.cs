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
    /// <summary>The save's version; 0 when the file has none (every save from before versions). Writers set <see cref="SaveFormat.Version"/>.</summary>
    public int SaveVersion { get; set; }
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
public enum SaveProblem
{
    /// <summary>The file does not read as a save.</summary>
    Damaged,

    /// <summary>Its version is above this build's.</summary>
    Newer,

    /// <summary>Its version is below this build's: an unsupported old save.</summary>
    Older
}

/// <summary>Why a save cannot be read, as a <see cref="SaveProblem"/> and a message for the player.</summary>
public sealed class SaveFormatException : Exception
{
    public SaveFormatException(SaveProblem kind, string message, Exception? inner = null) : base(message, inner)
    {
        Kind = kind;
    }

    public SaveProblem Kind { get; }
}

/// <summary>
/// The save file: gzip-compressed JSON of a <see cref="SaveFile"/>, enums by name. <see cref="Version"/>
/// is the save version this build writes and reads; a newer one is refused ("Saved by a newer
/// version"), and so is an older one ("Saved by an older version"): old saves are not migrated.
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

    /// <summary>Reads a save; throws <see cref="SaveFormatException"/> for a damaged file or another version.</summary>
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
            throw new SaveFormatException(SaveProblem.Damaged, "The save is damaged", ex);
        }
        if (file is null || file.Sim is null || file.Header is null)
        {
            throw new SaveFormatException(SaveProblem.Damaged, "The save is damaged");
        }
        if (file.Header.SaveVersion > Version)
        {
            throw new SaveFormatException(SaveProblem.Newer, "Saved by a newer version");
        }
        if (file.Header.SaveVersion < Version)
        {
            throw new SaveFormatException(SaveProblem.Older, "Saved by an older version");
        }
        return file;
    }

    /// <summary>The JSON of a sim's state, uncompressed (tests compare these).</summary>
    public static string Json(SaveData data)
    {
        return JsonSerializer.Serialize(data, Options);
    }
}
