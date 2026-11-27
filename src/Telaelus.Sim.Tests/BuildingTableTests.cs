using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Telaelus.Sim.Tests;

/// <summary>
/// The buildings blueprint's "At a glance" table gathers every building's numbers from the code.
/// This test rebuilds it from <see cref="BuildingDefs"/> and <see cref="Recipes"/> and fails when
/// the blueprint differs, printing the table as it should read, so a balance change cannot leave
/// the blueprint behind.
/// </summary>
public class BuildingTableTests
{
    private const string Header = "| Building | Tier | Size | Build cost | Draws | Supplies | Burns / eats | Makes |";

    /// <summary>Collectors whose output the definitions do not name in a way a table can say plainly.</summary>
    private static readonly Dictionary<string, string> MakesInWords = new()
    {
        ["Quarry"] = "1 Stone every 6 s (or Limestone beside a Highlands mountain), beside a mountain",
        ["Deep Well"] = "with no live field in range, 1 Crude Oil every 26.67 s from a worked-out oil cell or Salt Dome beside it",
        ["Deep Mine"] = "with no live field in range, 1 unit every 20 s from a worked-out Iron Ore, Copper Ore, Bauxite, Titanium Ore, Quartz, or Limestone cell or a motherlode beside it"
    };

    [Fact]
    public void TheBlueprintsBuildingTable_MatchesTheDefinitions()
    {
        string expected = Table();
        string actual = BlueprintTable();

        Assert.True(expected == actual,
            "docs/blueprints/buildings.md's \"At a glance\" table differs from the code. It should read:\n\n" + expected);
    }

    /// <summary>The table as the code defines it, one row per building and level in enum order.</summary>
    public static string Table()
    {
        var lines = new List<string> { Header, "|---|---|---|---|---|---|---|---|" };
        foreach ((BuildingType type, int level) in BuildingDefs.AllLevels())
        {
            BuildingDef d = BuildingDefs.For(type, level);
            string cost = d.Buildable ? Amounts(d.Cost) : "placed by the world";
            string draws = string.Join(", ", d.Draws.Select(kv => $"{Num(kv.Value)} {kv.Key}"));
            string supplies = string.Join(", ", d.Supplies.Select(kv => $"{Num(kv.Value)} {kv.Key}"));
            if (d.Solar)
            {
                supplies += " (daylight)";
            }
            if (d.StoreCapacity > 0f)
            {
                supplies = (supplies.Length > 0 ? supplies + "; " : "") + $"stores {Num(d.StoreCapacity)} Power";
            }
            var burns = new List<string>();
            if (d.BurnsFuel)
            {
                burns.Add("fuel: " + string.Join(", ", d.Fuels.Select(Name)));
            }
            if (d.Eats is (ResourceType eats, float every))
            {
                burns.Add($"1 {Name(eats)} every {Num(every)} s");
            }
            lines.Add($"| {d.Name} | {d.Tier} | {d.Width} x {d.Depth} | {cost} | {draws} | {supplies} | {string.Join("; ", burns)} | {string.Join("<br>", Makes(type, level, d))} |");
        }
        return string.Join("\n", lines);
    }

    private static List<string> Makes(BuildingType type, int level, BuildingDef d)
    {
        var makes = new List<string>();
        if (d.IsExtractor)
        {
            makes.Add($"works {string.Join(", ", d.Works.Select(Name))} (radius {Num(d.WorkRadius)}, speed {Num(d.Speed)})");
        }
        if (MakesInWords.TryGetValue(d.Name, out string? words))
        {
            makes.Add(words);
        }
        else if (d.ProduceSeconds > 0f)
        {
            makes.Add($"1 unit every {Num(d.ProduceSeconds)} s");
        }
        if (d.Plot is (float amount, float grow, float regrow))
        {
            makes.Add($"grows {Num(amount)} Wood in {Num(grow)} s, bare {Num(regrow)} s after cutting");
        }
        foreach (Recipe recipe in Recipes.For(type, level))
        {
            makes.Add(RecipeText(recipe));
        }
        if (d.IsHub)
        {
            makes.Add($"network: covers {Num(d.CoverageRadius)}, links {Num(d.LinkRadius)} units");
        }
        if (d.TruckSlots > 0)
        {
            makes.Add($"{d.TruckSlots} truck bays");
        }
        if (d.Researches)
        {
            makes.Add("research");
        }
        if (d.Storage != StorageRole.None)
        {
            makes.Add("raises storage limits");
        }
        return makes;
    }

    private static string RecipeText(Recipe recipe)
    {
        var inputs = new List<string>();
        for (int i = 0; i < recipe.Inputs.Count; i++)
        {
            inputs.Add($"{Num(recipe.Inputs[i].Amount)} {string.Join(" or ", recipe.OptionsFor(i).Select(Name))}");
        }
        string from = inputs.Count == 0 ? "(capacity only)" : string.Join(" + ", inputs);
        return $"{from} → {Amounts(recipe.Outputs)} ({Num(recipe.CycleTime)} s)";
    }

    private static string Amounts(IEnumerable<ResourceAmount> list)
    {
        return string.Join(", ", list.Select(a => $"{Num(a.Amount)} {Name(a.Type)}"));
    }

    private static string Name(ResourceType type) => ResourceDefs.For(type).Name;

    private static string Num(float value)
    {
        return value == MathF.Floor(value) ? ((int)value).ToString() : value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The table's lines as the blueprint has them, from its header to the first non-table line.</summary>
    private static string BlueprintTable()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Telaelus.slnx")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        string[] lines = File.ReadAllLines(Path.Combine(dir.FullName, "docs", "blueprints", "buildings.md"));
        int start = Array.IndexOf(lines, Header);
        Assert.True(start >= 0, "docs/blueprints/buildings.md has no \"At a glance\" table header");
        return string.Join("\n", lines.Skip(start).TakeWhile(l => l.StartsWith('|')));
    }
}
