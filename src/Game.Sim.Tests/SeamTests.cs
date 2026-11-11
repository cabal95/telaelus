using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Game.Sim.Tests;

/// <summary>
/// Checks the sim's seam rule by reading Game.Sim.dll's metadata: the only Godot
/// types it may reference are the plain math types. Any engine object (anything
/// derived from GodotObject) needs the native engine; without this test, using one
/// would crash the whole test run instead of failing a named test.
/// </summary>
public class SeamTests
{
    private static readonly HashSet<string> AllowedGodotTypes = new()
    {
        "Godot.Vector2",
        "Godot.Vector3",
        "Godot.Mathf"
    };

    [Fact]
    public void Sim_ReferencesOnlyGodotMathTypes()
    {
        string path = typeof(Simulation).Assembly.Location;
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader reader = pe.GetMetadataReader();

        var godotTypes = new SortedSet<string>();
        foreach (TypeReferenceHandle handle in reader.TypeReferences)
        {
            TypeReference type = reader.GetTypeReference(handle);
            if (type.ResolutionScope.Kind != HandleKind.AssemblyReference)
            {
                continue;
            }

            var scope = (AssemblyReferenceHandle)type.ResolutionScope;
            string assembly = reader.GetString(reader.GetAssemblyReference(scope).Name);
            if (assembly == "GodotSharp")
            {
                godotTypes.Add($"{reader.GetString(type.Namespace)}.{reader.GetString(type.Name)}");
            }
        }

        string[] disallowed = godotTypes.Where(name => !AllowedGodotTypes.Contains(name)).ToArray();
        Assert.True(
            disallowed.Length == 0,
            $"Game.Sim references Godot types outside the math allow-list: {string.Join(", ", disallowed)}");
    }
}
