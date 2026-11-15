using System;
using System.Linq;

namespace Game.Sim.Tests;

public class StorageTests
{
    // The empty map's HQ stands in its north-east corner; the player starts at (32, 32).
    private static Simulation EmptyMap()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        return sim;
    }

    private static Building Hq(Simulation sim)
    {
        return Assert.Single(sim.Buildings, b => b.Type == BuildingType.Hq);
    }

    [Fact]
    public void EveryResource_HasAStorageClass_AndWhatItNeeds()
    {
        foreach (ResourceType type in Enum.GetValues<ResourceType>())
        {
            ResourceStorage storage = ResourceDefs.For(type).Storage;
            switch (storage.Class)
            {
                case StorageClass.Bulk:
                    Assert.True(storage.ClassStep > 0f && storage.SiloStep > 0f, $"{type} has no Stockyard or Silo step");
                    break;
                case StorageClass.Fluid:
                case StorageClass.Secure:
                    Assert.True(storage.ClassStep > 0f, $"{type} has no class step");
                    Assert.Equal(0f, storage.HqBase);
                    break;
                case StorageClass.Cargo:
                    Assert.True(storage.Cargo > 0f, $"{type} has no Cargo weight");
                    break;
                default:
                    Assert.Contains(type, new[] { ResourceType.Sand, ResourceType.Water });
                    break;
            }
        }
        Assert.Equal(StorageClass.Cargo, ResourceDefs.For(ResourceType.Iron).Storage.Class);
        Assert.Equal(25f, ResourceDefs.For(ResourceType.Engine).Storage.Cargo);
    }

    [Fact]
    public void TheHq_GivesTheStartingMaterialsAndASmallCargoPool_AndNothingElse()
    {
        Simulation sim = EmptyMap();

        Assert.Equal(100f, sim.StorageLimit(ResourceType.Wood));
        Assert.Equal(60f, sim.StorageLimit(ResourceType.IronOre));
        Assert.Equal(60f, sim.StorageLimit(ResourceType.Limestone));
        Assert.Equal(0f, sim.StorageLimit(ResourceType.Bauxite));
        Assert.Equal(0f, sim.StorageLimit(ResourceType.CrudeOil));
        Assert.Equal(0f, sim.StorageLimit(ResourceType.UraniumOre));
        Assert.Equal(150f, sim.CargoPool);
        Assert.Equal(float.PositiveInfinity, sim.RoomFor(ResourceType.Sand));
    }

    [Fact]
    public void Limits_SumTheHqAndEveryOnlineStorageBuilding()
    {
        Simulation sim = EmptyMap();
        sim.Debug.PlaceBuilding(BuildingType.Stockyard, 20, 20, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.Stockyard, 24, 20, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.TankFarm, 28, 20, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.Vault, 32, 20, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.Warehouse, 36, 20, Facing.North);
        Assert.Equal(100f, sim.StorageLimit(ResourceType.Wood));

        SimTestHelpers.Connect(sim, 28f, 21f);

        Assert.Equal(500f, sim.StorageLimit(ResourceType.Wood));
        Assert.Equal(160f, sim.StorageLimit(ResourceType.Bauxite));
        Assert.Equal(300f, sim.StorageLimit(ResourceType.CrudeOil));
        Assert.Equal(30f, sim.StorageLimit(ResourceType.UraniumOre));
        Assert.Equal(350f, sim.CargoPool);
    }

    [Fact]
    public void ASilo_AddsItsStepToItsChosenBulkResourceOnly()
    {
        Simulation sim = EmptyMap();
        Building silo = sim.Debug.PlaceBuilding(BuildingType.Silo, 20, 20, Facing.North);
        SimTestHelpers.Connect(sim, 21f, 21f);
        Assert.Equal(400f, sim.StorageLimit(ResourceType.Wood));

        Assert.True(sim.SetSiloResource(silo.Id, ResourceType.Quartz));

        Assert.Equal(100f, sim.StorageLimit(ResourceType.Wood));
        Assert.Equal(150f, sim.StorageLimit(ResourceType.Quartz));
        Assert.False(sim.SetSiloResource(silo.Id, ResourceType.CrudeOil));
        Assert.False(sim.SetSiloResource(Hq(sim).Id, ResourceType.Stone));
        Assert.Equal(ResourceType.Quartz, silo.SiloResource);
    }

    [Fact]
    public void CargoRoom_IsTheFreePoolOverTheWeight()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Iron, 100f);

        Assert.Equal(100f, sim.CargoUsed);
        Assert.Equal(50f, sim.RoomFor(ResourceType.Iron));
        Assert.Equal(200f, sim.RoomFor(ResourceType.Circuit));
        Assert.Equal(2.5f, sim.RoomFor(ResourceType.HullSection));
    }

    [Fact]
    public void HandHarvest_StopsAtTheLimit_AndResumesWhenThereIsRoom()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Wood, 100f);
        sim.Debug.PlaceField(ResourceType.Wood, 8, 7, 50f);
        SimTestHelpers.Teleport(sim, 33.5f, 33f);
        sim.Intent.Harvest = true;

        SimTestHelpers.Seconds(sim, 4.0);

        Assert.True(sim.HarvestBlocked);
        Assert.Equal(0f, sim.HarvestProgress);
        Assert.Equal(100f, SimTestHelpers.Stock(sim, ResourceType.Wood));

        sim.Debug.PlaceBuilding(BuildingType.Silo, 40, 40, Facing.North);
        SimTestHelpers.Connect(sim, 41f, 41f);
        SimTestHelpers.Teleport(sim, 33.5f, 33f);
        SimTestHelpers.Seconds(sim, 3.1);

        Assert.False(sim.HarvestBlocked);
        Assert.Equal(101f, SimTestHelpers.Stock(sim, ResourceType.Wood));
    }

    [Fact]
    public void AnOnlineProcessor_FillsItsBufferWhenStorageIsFull_ThenStops()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Iron, 150f);
        sim.Debug.SeedStockpile(ResourceType.IronOre, 15f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 20, 20, Facing.North);
        SimTestHelpers.Connect(sim, 21f, 21f);

        // Ten 16 s cycles fill its 10-unit output buffer; then it stops.
        SimTestHelpers.Seconds(sim, 200.0);

        Assert.Equal(10f, smelter.Output[ResourceType.Iron]);
        Assert.Equal(5f, SimTestHelpers.Stock(sim, ResourceType.IronOre));
        Assert.Equal(new BuildingStatus(BuildingState.Idle, "Storage full: Iron"), sim.BuildingStatusOf(smelter));
    }

    [Fact]
    public void AnOnlineProcessorsBuffer_DrainsIntoStorage_WhenRoomAppears()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Iron, 150f);
        sim.Debug.SeedStockpile(ResourceType.IronOre, 1f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 20, 20, Facing.North);
        SimTestHelpers.Connect(sim, 21f, 21f);
        SimTestHelpers.Seconds(sim, 20.0);
        Assert.Equal(1f, smelter.Output[ResourceType.Iron]);

        sim.Debug.PlaceBuilding(BuildingType.Warehouse, 24, 20, Facing.North);
        SimTestHelpers.Ticks(sim, 1);

        Assert.Empty(smelter.Output);
        Assert.Equal(151f, SimTestHelpers.Stock(sim, ResourceType.Iron));
    }

    [Fact]
    public void Returns_PassTheLimit()
    {
        Simulation sim = EmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Wood, 100f);
        sim.Debug.SeedStockpile(ResourceType.Stone, 100f);
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 20, 20, Facing.North);

        // The Mine costs 25 Wood and 15 Stone; half comes back even though Wood and Stone are full.
        Assert.True(sim.TryDestroyBuilding(mine.Id));

        Assert.Equal(113f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.Equal(108f, SimTestHelpers.Stock(sim, ResourceType.Stone));
    }

    [Fact]
    public void DestroyingStorage_LeavesStockOverTheLimit_AndGatheringWaits()
    {
        Simulation sim = EmptyMap();
        Building silo = sim.Debug.PlaceBuilding(BuildingType.Silo, 20, 20, Facing.North);
        sim.Debug.SeedStockpile(ResourceType.Wood, 300f);

        Assert.True(sim.TryDestroyBuilding(silo.Id));

        Assert.Equal(315f, SimTestHelpers.Stock(sim, ResourceType.Wood));
        Assert.Equal(100f, sim.StorageLimit(ResourceType.Wood));
        Assert.Equal(0f, sim.RoomFor(ResourceType.Wood));
    }

    [Fact]
    public void TheHq_CannotBeSwitchedOffOrDestroyed()
    {
        Simulation sim = EmptyMap();
        Building hq = Hq(sim);

        Assert.False(sim.SetBuildingEnabled(hq.Id, false));
        Assert.False(sim.TryDestroyBuilding(hq.Id));
        Assert.True(hq.Enabled);
        Assert.Same(hq, sim.BuildingById(hq.Id));
    }

    [Fact]
    public void TheHq_StandsAtStart_InEveryKindOfWorld()
    {
        var testMap = new Simulation();
        Assert.Equal((Simulation.TestMapHqAnchor.X, Simulation.TestMapHqAnchor.Z), (Hq(testMap).AnchorX, Hq(testMap).AnchorZ));

        Building emptyHq = Hq(EmptyMap());
        Assert.Equal((Simulation.EmptyMapHqAnchor.X, Simulation.EmptyMapHqAnchor.Z), (emptyHq.AnchorX, emptyHq.AnchorZ));
        Assert.Equal(Facing.South, emptyHq.Facing);
        Assert.Equal((6, 10), (emptyHq.Width, emptyHq.Depth));

        Assert.False(BuildingDefs.For(BuildingType.Hq).Buildable);
        Assert.False(EmptyMap().IsUnlocked(BuildingType.Hq));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(42)]
    public void AGeneratedWorldsHq_IsNorthOfTheSpawn_OnClearGround_WithNoFieldUnderIt(int seed)
    {
        var sim = new Simulation(WorldSettings.Generated(seed));
        Building hq = Hq(sim);
        (int spawnX, int spawnZ) = Simulation.CellAt(sim.SpawnPoint.X, sim.SpawnPoint.Z);

        Assert.True(hq.AnchorZ + hq.Depth <= spawnZ, "the HQ is north of the spawn");
        Assert.InRange(spawnX, hq.AnchorX, hq.AnchorX + hq.Width - 1);
        for (int x = hq.AnchorX; x < hq.AnchorX + hq.Width; x++)
        {
            for (int z = hq.AnchorZ; z < hq.AnchorZ + hq.Depth; z++)
            {
                Assert.True(sim.IsBuildable(x, z), $"({x}, {z}) is not buildable");
                Assert.Null(sim.FieldAt(x, z));
            }
        }
    }

    [Fact]
    public void EveryBuildingCost_FitsTheHqsBase()
    {
        Simulation sim = EmptyMap();
        foreach (BuildingType type in Enum.GetValues<BuildingType>().Where(t => t != BuildingType.Vault))
        {
            foreach (ResourceAmount cost in BuildingDefs.For(type).Cost)
            {
                float room = ResourceDefs.For(cost.Type).Storage.Class == StorageClass.Cargo
                    ? sim.CargoPool / ResourceDefs.For(cost.Type).Storage.Cargo
                    : sim.StorageLimit(cost.Type);
                Assert.True(cost.Amount <= room, $"{type} costs {cost.Amount} {cost.Type}, more than the HQ holds");
            }
        }
    }
}
