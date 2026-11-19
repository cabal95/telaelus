using System;
using System.Linq;

namespace Telaelus.Sim.Tests;

public class ResourceTests
{
    private static Simulation EmptyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        return sim;
    }

    [Fact]
    public void EveryType_HasADefinition_AndOnlyDepositsHaveFields()
    {
        foreach (ResourceType type in Enum.GetValues<ResourceType>())
        {
            ResourceDef def = ResourceDefs.For(type);
            Assert.False(string.IsNullOrWhiteSpace(def.Name));
            Assert.Equal(def.Kind == ResourceKind.Deposit, def.HasFields);
            if (def.HandHarvestable)
            {
                Assert.Equal(ResourceKind.Deposit, def.Kind);
            }
        }
        Assert.Equal(ResourceKind.TerrainSourced, ResourceDefs.For(ResourceType.Sand).Kind);
        Assert.Equal(ResourceKind.TerrainSourced, ResourceDefs.For(ResourceType.Water).Kind);
        Assert.False(ResourceDefs.For(ResourceType.CrudeOil).HandHarvestable);
    }

    [Fact]
    public void TheCatalog_Has63Resources_ByTier()
    {
        ILookup<int, ResourceType> byTier = Enum.GetValues<ResourceType>().ToLookup(type => ResourceDefs.For(type).Tier);

        Assert.Equal(63, Enum.GetValues<ResourceType>().Length);
        Assert.Equal(17, byTier[0].Count());
        Assert.Equal(20, byTier[1].Count());
        Assert.Equal(16, byTier[2].Count());
        Assert.Equal(8, byTier[3].Count());
        Assert.Equal(new[] { ResourceType.HullSection, ResourceType.Engine }, byTier[4]);
        Assert.Equal(Enum.GetValues<ResourceType>().OrderBy(type => ResourceDefs.For(type).Tier), Enum.GetValues<ResourceType>());
    }

    [Fact]
    public void RawResources_AreTierZero_AndMadeOnesAreNot()
    {
        foreach (ResourceType type in Enum.GetValues<ResourceType>())
        {
            ResourceDef def = ResourceDefs.For(type);
            Assert.Equal(def.Kind != ResourceKind.Refined, def.Tier == 0);
            Assert.Equal(def.Kind == ResourceKind.Deposit, def.GatherSeconds is not null);
        }
    }

    [Theory]
    [InlineData(ResourceType.Limestone, 4.5f, true)]
    [InlineData(ResourceType.Sulfur, 4.5f, true)]
    [InlineData(ResourceType.Bauxite, 6f, true)]
    [InlineData(ResourceType.Quartz, 6f, true)]
    [InlineData(ResourceType.TitaniumOre, 7.5f, true)]
    [InlineData(ResourceType.RareEarths, 9f, false)]
    [InlineData(ResourceType.UraniumOre, 9f, false)]
    [InlineData(ResourceType.Crystals, 9f, false)]
    public void NewDeposits_HaveTheirGatherTimes(ResourceType type, float seconds, bool byHand)
    {
        ResourceDef def = ResourceDefs.For(type);
        Assert.Equal(ResourceKind.Deposit, def.Kind);
        Assert.Equal(seconds, def.GatherSeconds);
        Assert.Equal(byHand, def.HandHarvestable);
    }

    [Fact]
    public void EveryResource_HasAnItemModel()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "Telaelus.slnx")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        string items = System.IO.Path.Combine(dir.FullName, "src", "Telaelus", "assets", "models", "items");
        foreach (ResourceType type in Enum.GetValues<ResourceType>())
        {
            string snake = string.Concat(type.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
            Assert.True(System.IO.File.Exists(System.IO.Path.Combine(items, $"item_{snake}.glb")), $"no item model for {type}");
        }
    }

    [Theory]
    [InlineData(ResourceType.Sand)]
    [InlineData(ResourceType.Water)]
    [InlineData(ResourceType.Iron)]
    [InlineData(ResourceType.RocketFuel)]
    public void PlaceField_RejectsNonDeposits(ResourceType type)
    {
        Simulation sim = EmptyMap();
        Assert.Throws<InvalidOperationException>(() => sim.Debug.PlaceField(type, 2, 2));
    }

    [Fact]
    public void Biomes_OfferOnlyDeposits_WithTheirSignatures()
    {
        foreach (BiomeType biome in Enum.GetValues<BiomeType>())
        {
            foreach (ResourceType type in BiomeDefs.For(biome).Fields.Keys)
            {
                Assert.True(ResourceDefs.For(type).HasFields, $"{biome} offers {type}");
            }
        }

        BiomeDef highlands = BiomeDefs.For(BiomeType.Highlands);
        Assert.Equal(1600f, highlands.FieldAmount(ResourceType.IronOre));
        Assert.Equal(1200f, highlands.FieldAmount(ResourceType.CopperOre));
        Assert.Equal(1600f, highlands.FieldAmount(ResourceType.Coal));
        Assert.Equal(1600f, highlands.FieldAmount(ResourceType.Stone));
        Assert.Equal(1600f, highlands.FieldAmount(ResourceType.Quartz));
        Assert.Equal(2000f, BiomeDefs.For(BiomeType.Desert).FieldAmount(ResourceType.CrudeOil));
        Assert.Equal(1000f, BiomeDefs.For(BiomeType.Grassland).FieldAmount(ResourceType.Stone));
        Assert.Equal(400f, BiomeDefs.For(BiomeType.Volcanic).FieldAmount(ResourceType.Crystals));
        Assert.Equal(0.013f, BiomeDefs.For(BiomeType.Grassland).FieldDensity);
        Assert.Equal(0.15f, BiomeDefs.For(BiomeType.Forest).FieldDensity);

        // Every deposit has fields in some biome now.
        foreach (ResourceType type in System.Enum.GetValues<ResourceType>())
        {
            if (ResourceDefs.For(type).HasFields)
            {
                Assert.Contains(System.Enum.GetValues<BiomeType>(), biome => BiomeDefs.For(biome).Fields.ContainsKey(type));
            }
        }
    }

    [Fact]
    public void Mine_WorksEveryMineralDeposit_NearestFirst()
    {
        // Mine anchored at (12, 17), center (13, 18), between Copper Ore at map cell (2, 4), 1 away,
        // and Coal at (4, 4), 3 away. The Wood at (4, 3) is in range too but is not a mineral.
        Simulation sim = EmptyMap();
        ResourceField copper = sim.Debug.PlaceField(ResourceType.CopperOre, 2, 4, 1f);
        ResourceField coal = sim.Debug.PlaceField(ResourceType.Coal, 4, 4, 100f);
        sim.Debug.PlaceField(ResourceType.Wood, 4, 3, 100f);
        SimTestHelpers.PlaceOnline(sim, BuildingType.Mine, 12, 17);
        Building mine = Assert.Single(SimTestHelpers.Placed(sim));

        SimTestHelpers.Ticks(sim, 15);
        Assert.Same(copper, mine.CurrentField);

        // 1 Copper Ore at 8 s per unit runs out at 8 s; then the Mine moves to the Coal, 6 s per unit.
        SimTestHelpers.Seconds(sim, 15.0);
        Assert.Equal(1f, SimTestHelpers.Gathered(sim, ResourceType.CopperOre));
        Assert.Same(coal, mine.CurrentField);
        Assert.Equal(1f, SimTestHelpers.Gathered(sim, ResourceType.Coal));
        Assert.Equal(0f, SimTestHelpers.Gathered(sim, ResourceType.Wood));
    }

    [Fact]
    public void OilWell_PumpsCrudeOil_FromBesideItsSeep()
    {
        // Oil Well anchored at (12, 10), center (13, 11): the seep at map cell (3, 3) is 1 away,
        // the one at (5, 2) is 7 away, beyond its 3-unit radius.
        Simulation sim = EmptyMap();
        ResourceField near = sim.Debug.PlaceField(ResourceType.CrudeOil, 3, 3);
        sim.Debug.PlaceField(ResourceType.CrudeOil, 5, 2);
        Building well = SimTestHelpers.PlaceOnline(sim, BuildingType.OilWell, 12, 10);
        sim.Debug.PlaceBuilding(BuildingType.TankFarm, 40, 40, Facing.North);

        // 4 s per unit at base speed.
        SimTestHelpers.Seconds(sim, 4.1);

        Assert.Same(near, well.CurrentField);
        Assert.Equal(1f, SimTestHelpers.Gathered(sim, ResourceType.CrudeOil));
        Assert.Single(sim.FieldsInRange(well.Position, well.Def.WorkRadius, well.Def.Works));
    }

    [Fact]
    public void HandHarvest_SkipsCrudeOil()
    {
        Simulation sim = EmptyMap();
        sim.Debug.PlaceField(ResourceType.CrudeOil, 2, 2);
        SimTestHelpers.Teleport(sim, 7.5f, 9.5f);
        sim.Intent = new PlayerIntent
        {
            Harvest = true
        };

        SimTestHelpers.Seconds(sim, 1.0);

        Assert.Null(sim.HarvestTarget);
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.CrudeOil));

        // The same spot beside a Stone field harvests it.
        sim.Debug.RemoveField(2, 2);
        sim.Debug.PlaceField(ResourceType.Stone, 2, 2);
        SimTestHelpers.Seconds(sim, 4.6);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Stone));
    }
}
