using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Sim;
using Godot;

namespace Game.Presentation;

/// <summary>One save in the folder, as the load list shows it: its header, or why it cannot be loaded.</summary>
public sealed record SaveEntry(string Name, SaveHeader? Header, string? Problem, DateTime Written)
{
    public bool Loadable => Header is not null && Problem is null;
}

/// <summary>
/// The saves on disk: <c>&lt;name&gt;.save</c> (see <see cref="SaveFormat"/>) with a
/// <c>&lt;name&gt;.png</c> thumbnail beside it, in <see cref="Folder"/> (the player's data folder,
/// <c>user://saves</c>, unless a scenario or control session points it at its own). A save is written
/// to a temporary file and moved over the old one, so a failed write never ruins the last save.
/// </summary>
public static class SaveStore
{
    public const string AutosavePrefix = "autosave-";

    /// <summary>The folder saves live in.</summary>
    public static string Folder { get; set; } = ProjectSettings.GlobalizePath("user://saves");

    public static string SavePath(string name) => Path.Combine(Folder, name + ".save");

    public static string ThumbnailPath(string name) => Path.Combine(Folder, name + ".png");

    /// <summary>Every save, newest first.</summary>
    public static List<SaveEntry> List()
    {
        var entries = new List<SaveEntry>();
        if (!Directory.Exists(Folder))
        {
            return entries;
        }
        foreach (string file in Directory.GetFiles(Folder, "*.save"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            DateTime written = File.GetLastWriteTime(file);
            try
            {
                SaveFile save = SaveFormat.Read(File.ReadAllBytes(file));
                entries.Add(new SaveEntry(name, save.Header, null, written));
            }
            catch (SaveFormatException ex)
            {
                entries.Add(new SaveEntry(name, null, ex.Message, written));
            }
            catch (IOException ex)
            {
                entries.Add(new SaveEntry(name, null, ex.Message, written));
            }
        }
        return entries.OrderByDescending(e => e.Written).ToList();
    }

    public static bool Exists(string name) => File.Exists(SavePath(name));

    /// <summary>Writes a save (and its thumbnail when given), replacing any of the same name.</summary>
    public static void Write(string name, SaveFile save, Image? thumbnail)
    {
        Directory.CreateDirectory(Folder);
        string path = SavePath(name);
        string temporary = path + ".tmp";
        File.WriteAllBytes(temporary, SaveFormat.Write(save));
        File.Move(temporary, path, overwrite: true);
        if (thumbnail is not null)
        {
            thumbnail.SavePng(ThumbnailPath(name));
        }
    }

    /// <summary>Reads a save; throws <see cref="SaveFormatException"/> when it cannot be loaded.</summary>
    public static SaveFile Read(string name)
    {
        string path = SavePath(name);
        if (!File.Exists(path))
        {
            throw new SaveFormatException($"No save named {name}");
        }
        return SaveFormat.Read(File.ReadAllBytes(path));
    }

    public static void Delete(string name)
    {
        File.Delete(SavePath(name));
        if (File.Exists(ThumbnailPath(name)))
        {
            File.Delete(ThumbnailPath(name));
        }
    }

    /// <summary>A save's thumbnail, or null when it has none.</summary>
    public static Texture2D? Thumbnail(string name)
    {
        string path = ThumbnailPath(name);
        if (!File.Exists(path))
        {
            return null;
        }
        Image image = Image.LoadFromFile(path);
        return image is null || image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
    }

    /// <summary>The first free "Save n" name.</summary>
    public static string NextName()
    {
        for (int n = 1; ; n++)
        {
            string name = $"Save {n}";
            if (!Exists(name))
            {
                return name;
            }
        }
    }

    /// <summary>The autosave to write next: whichever of the two is older (or missing).</summary>
    public static string NextAutosave()
    {
        string a = AutosavePrefix + "1";
        string b = AutosavePrefix + "2";
        if (!Exists(a))
        {
            return a;
        }
        if (!Exists(b))
        {
            return b;
        }
        return File.GetLastWriteTime(SavePath(a)) <= File.GetLastWriteTime(SavePath(b)) ? a : b;
    }

    /// <summary>A play time as hours and minutes ("1 h 05 m", "12 m").</summary>
    public static string PlayTime(double seconds)
    {
        int minutes = (int)(seconds / 60);
        return minutes >= 60 ? $"{minutes / 60} h {minutes % 60:00} m" : $"{minutes} m";
    }
}
