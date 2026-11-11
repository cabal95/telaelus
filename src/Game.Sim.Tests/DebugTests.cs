using System;
using Godot;

namespace Game.Sim.Tests;

public class DebugTests
{
    [Fact]
    public void ResetToEmptyMap_ClearsEverything()
    {
        var sim = new Simulation();
        sim.Debug.SeedStockpile(ResourceType.Ore, 5f);
        sim.Debug.PlaceBuilding(BuildingType.Smelter, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North);
        sim.Debug.TeleportPlayer(5f, 5f);

        sim.Debug.ResetToEmptyMap();

        Assert.Empty(sim.Fields);
        Assert.Empty(sim.Buildings);
        Assert.Empty(sim.Stockpile);
        Assert.Equal(new Vector3(32f, 0f, 32f), sim.Player.Position);
        for (int mapX = 0; mapX < sim.MapWidth; mapX++)
        {
            for (int mapZ = 0; mapZ < sim.MapHeight; mapZ++)
            {
                Assert.Equal(TerrainType.Grass, sim.MapTerrainAt(mapX, mapZ));
            }
        }
        Assert.True(sim.IsWalkable(SimTestHelpers.ClearX, SimTestHelpers.ClearZ));
    }

    [Fact]
    public void SetTerrain_ChangesOneMapCell()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();

        sim.Debug.SetTerrain(2, 3, TerrainType.Water);

        Assert.Equal(TerrainType.Water, sim.TerrainAt(8, 12));
        Assert.Equal(TerrainType.Water, sim.TerrainAt(11, 15));
        Assert.Equal(TerrainType.Grass, sim.TerrainAt(12, 15));
    }

    [Fact]
    public void PlaceField_AddsABlockingField()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();

        ResourceField field = sim.Debug.PlaceField(ResourceType.Wood, 5, 5, 100f);

        Assert.Same(field, sim.FieldAt(21, 22));
        Assert.Equal(100f, field.Amount);
        Assert.False(sim.IsWalkable(21, 22));
        Assert.Throws<InvalidOperationException>(() => sim.Debug.PlaceField(ResourceType.Ore, 5, 5));
    }

    [Fact]
    public void SetFieldAmount_ChangesTheStageAndZeroRemovesIt()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        ResourceField field = sim.Debug.PlaceField(ResourceType.Ore, 10, 10);

        sim.Debug.SetFieldAmount(10, 10, 120f);
        Assert.Equal(3, field.Stage);

        sim.Debug.SetFieldAmount(10, 10, 0f);
        Assert.Empty(sim.Fields);
        Assert.Null(sim.FieldAt(40, 40));
    }

    [Fact]
    public void RemoveField_RemovesIt()
    {
        var sim = new Simulation();
        sim.Debug.RemoveField(10, 9);

        Assert.Null(sim.FieldAt(40, 36));
        Assert.Equal(7, sim.Fields.Count);
        Assert.Throws<InvalidOperationException>(() => sim.Debug.RemoveField(10, 9));
    }

    [Fact]
    public void RemoveBuilding_FreesItsCells()
    {
        var sim = new Simulation();
        sim.Debug.PlaceBuilding(BuildingType.Refinery, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.East);

        sim.Debug.RemoveBuilding(SimTestHelpers.ClearX, SimTestHelpers.ClearZ);

        Assert.Empty(sim.Buildings);
        Assert.True(sim.IsWalkable(SimTestHelpers.ClearX + 2, SimTestHelpers.ClearZ + 1));
        Assert.True(sim.CanPlace(BuildingType.Smelter, SimTestHelpers.ClearX, SimTestHelpers.ClearZ, Facing.North));
    }

    [Fact]
    public void PlaceBuilding_ThrowsWhenPlacementIsInvalid()
    {
        var sim = new Simulation();
        Assert.Throws<InvalidOperationException>(() => sim.Debug.PlaceBuilding(BuildingType.Smelter, 36, 20, Facing.North));
    }

    [Fact]
    public void MapChanged_IsRaisedByMapEdits()
    {
        var sim = new Simulation();
        int raised = 0;
        sim.MapChanged += () => raised++;

        sim.Debug.ResetToEmptyMap();
        sim.Debug.SetTerrain(1, 1, TerrainType.Mountain);
        sim.Debug.PlaceField(ResourceType.Ore, 3, 3);
        sim.Debug.SetFieldAmount(3, 3, 50f);
        sim.Debug.RemoveField(3, 3);

        Assert.Equal(5, raised);
    }

    [Fact]
    public void OutOfBoundsMapCells_Throw()
    {
        var sim = new Simulation();
        Assert.Throws<InvalidOperationException>(() => sim.Debug.SetTerrain(-1, 0, TerrainType.Water));
        Assert.Throws<InvalidOperationException>(() => sim.Debug.PlaceField(ResourceType.Ore, 16, 0));
    }
}
