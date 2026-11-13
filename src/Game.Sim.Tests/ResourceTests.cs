using System;

namespace Game.Sim.Tests;

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
        Assert.Equal(400f, highlands.FieldAmount(ResourceType.IronOre));
        Assert.Equal(300f, highlands.FieldAmount(ResourceType.CopperOre));
        Assert.Equal(400f, highlands.FieldAmount(ResourceType.Coal));
        Assert.Equal(400f, highlands.FieldAmount(ResourceType.Stone));
        Assert.Equal(500f, BiomeDefs.For(BiomeType.Desert).FieldAmount(ResourceType.CrudeOil));
        Assert.Equal(250f, BiomeDefs.For(BiomeType.Grassland).FieldAmount(ResourceType.Stone));
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
        sim.Debug.PlaceBuilding(BuildingType.Mine, 12, 17, Facing.North);
        Building mine = Assert.Single(sim.Buildings);

        SimTestHelpers.Ticks(sim, 15);
        Assert.Same(copper, mine.CurrentField);

        // 1 Copper Ore at 8 s per unit runs out at 8 s; then the Mine moves to the Coal, 6 s per unit.
        SimTestHelpers.Seconds(sim, 15.0);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.CopperOre));
        Assert.Same(coal, mine.CurrentField);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.Coal));
        Assert.Equal(0f, SimTestHelpers.Stock(sim, ResourceType.Wood));
    }

    [Fact]
    public void OilWell_PumpsCrudeOil_FromBesideItsSeep()
    {
        // Oil Well anchored at (12, 10), center (13, 11): the seep at map cell (3, 3) is 1 away,
        // the one at (5, 2) is 7 away, beyond its 3-unit radius.
        Simulation sim = EmptyMap();
        ResourceField near = sim.Debug.PlaceField(ResourceType.CrudeOil, 3, 3);
        sim.Debug.PlaceField(ResourceType.CrudeOil, 5, 2);
        sim.Debug.PlaceBuilding(BuildingType.OilWell, 12, 10, Facing.North);
        Building well = Assert.Single(sim.Buildings);

        // 4 s per unit at base speed.
        SimTestHelpers.Seconds(sim, 4.1);

        Assert.Same(near, well.CurrentField);
        Assert.Equal(1f, SimTestHelpers.Stock(sim, ResourceType.CrudeOil));
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
