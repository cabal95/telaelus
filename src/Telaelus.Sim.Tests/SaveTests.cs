using System.Linq;
using System.Text;

namespace Telaelus.Sim.Tests;

public class SaveTests
{
    /// <summary>
    /// A busy base on the empty map: Burner Generators on Coal, a Mine on Iron Ore with a Depot whose
    /// truck hauls it, a Smelter, a Battery Bank, a Research Lab on Material Transfer, a road and a
    /// bridge over a strip of water, a worked-out cell, and the clock at mid-afternoon.
    /// </summary>
    private static Simulation Busy()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.Coal, 200f);
        sim.Debug.SeedStockpile(ResourceType.IronOre, 20f);
        sim.Debug.SeedStockpile(ResourceType.Iron, 40f);
        sim.Debug.SeedStockpile(ResourceType.Copper, 20f);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 44, 0, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.BurnerGenerator, 46, 0, Facing.North);
        sim.Debug.PlaceField(ResourceType.IronOre, 2, 10, 300f);
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 13, 40, Facing.North);
        sim.Debug.FillBuffer(13, 40, ResourceType.IronOre, 15f);
        sim.Debug.PlaceBuilding(BuildingType.Depot, 46, 14, Facing.North);
        sim.Debug.PlaceBuilding(BuildingType.Smelter, 40, 4, Facing.North);
        Building bank = sim.Debug.PlaceBuilding(BuildingType.BatteryBank, 48, 4, Facing.North);
        bank.Charge = 1234f;
        sim.Debug.PlaceBuilding(BuildingType.ResearchLab, 52, 12, Facing.North);
        Assert.True(sim.StartResearch(ResearchProject.MaterialTransfer));
        for (int z = 0; z < 16; z++)
        {
            sim.Debug.SetTerrain(5, z, TerrainType.ShallowWater);
        }
        sim.Debug.PlaceRoad(26, 30, 40, 30);
        sim.Debug.PlaceBridge(20, 40, 22, 40);
        sim.Debug.SetWorkedOut(8, 12, ResourceType.CrudeOil);
        sim.Debug.SetTimeOfDay(0.6f);
        SimTestHelpers.Connect(sim, mine.Position.X, mine.Position.Z);
        return sim;
    }

    private static string Json(Simulation sim)
    {
        return SaveFormat.Json(sim.Save());
    }

    [Fact]
    public void ASave_LoadedAndSavedAgain_IsIdentical()
    {
        Simulation sim = Busy();
        SimTestHelpers.Seconds(sim, 20.0);
        string saved = Json(sim);

        Simulation loaded = Simulation.FromSave(sim.Save());

        Assert.Equal(saved, Json(loaded));
    }

    [Fact]
    public void ALoadedGame_ContinuesExactlyAsTheSavedOne()
    {
        Simulation sim = Busy();
        SimTestHelpers.Seconds(sim, 15.0);
        Truck truck = Assert.Single(sim.Trucks);
        Assert.NotEqual(TruckState.Parked, truck.State);
        SaveData save = sim.Save();

        SimTestHelpers.Seconds(sim, 60.0);
        Simulation loaded = Simulation.FromSave(save);
        SimTestHelpers.Seconds(loaded, 60.0);

        Assert.Equal(Json(sim), Json(loaded));
    }

    [Fact]
    public void ASwitchedOffExtractor_WhoseFieldWasRemoved_SavesAndLoads()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.PlaceField(ResourceType.IronOre, 2, 10, 300f);
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 13, 40, Facing.North);
        SimTestHelpers.Connect(sim, mine.Position.X, mine.Position.Z);
        SimTestHelpers.Seconds(sim, 2.0);
        Assert.NotNull(mine.CurrentField);
        Assert.True(sim.SetBuildingEnabled(mine.Id, false));

        sim.Debug.RemoveField(2, 10);

        Assert.Null(mine.CurrentField);
        Simulation loaded = Simulation.FromSave(sim.Save());
        Assert.Null(loaded.BuildingById(mine.Id)!.CurrentField);
    }

    [Fact]
    public void AnOlderSavesInputBufferAndFuelSlot_GoToStorage()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        sim.Debug.SeedStockpile(ResourceType.IronOre, 10f);
        Building smelter = sim.Debug.PlaceBuilding(BuildingType.Smelter, 40, 4, Facing.North);
        SaveData save = sim.Save();
        BuildingData data = save.Buildings.Single(b => b.Id == smelter.Id);
        data.Input.Add(new AmountData
        {
            Type = ResourceType.IronOre,
            Amount = 3f
        });
        data.FuelSlot.Add(new AmountData
        {
            Type = ResourceType.Coal,
            Amount = 2f
        });

        Simulation loaded = Simulation.FromSave(save);

        Assert.Equal(13f, SimTestHelpers.Stock(loaded, ResourceType.IronOre));
        Assert.Equal(2f, SimTestHelpers.Stock(loaded, ResourceType.Coal));
        Assert.Empty(loaded.Save().Buildings.Single(b => b.Id == smelter.Id).Input);
    }

    [Fact]
    public void ASaveNamingAMissingCurrentField_LoadsWithNone()
    {
        var sim = new Simulation();
        sim.Debug.ResetToEmptyMap();
        Building mine = sim.Debug.PlaceBuilding(BuildingType.Mine, 13, 40, Facing.North);
        SaveData save = sim.Save();
        save.Buildings.Single(b => b.Id == mine.Id).CurrentField = 9999;

        Simulation loaded = Simulation.FromSave(save);

        Assert.Null(loaded.BuildingById(mine.Id)!.CurrentField);
    }

    [Fact]
    public void ALoadedGame_KeepsEveryKindOfState()
    {
        Simulation sim = Busy();
        SimTestHelpers.Seconds(sim, 15.0);

        Simulation loaded = Simulation.FromSave(sim.Save());

        Assert.Equal(sim.Buildings.Count, loaded.Buildings.Count);
        float charge = sim.Buildings.Single(b => b.Type == BuildingType.BatteryBank).Charge;
        Assert.True(charge > 1234f);
        Assert.Equal(charge, loaded.Buildings.Single(b => b.Type == BuildingType.BatteryBank).Charge);
        Assert.Equal(ResearchProject.MaterialTransfer, loaded.ActiveResearch);
        Assert.Equal(sim.ResearchProgress(ResearchProject.MaterialTransfer), loaded.ResearchProgress(ResearchProject.MaterialTransfer));
        Assert.True(loaded.IsRoad(27, 31));
        Assert.True(loaded.IsBridge(21, 41));
        Assert.True(loaded.IsWalkable(21, 41));
        Assert.Equal(sim.TimeOfDay, loaded.TimeOfDay);
        Assert.Equal(sim.Trucks[0].Position, loaded.Trucks[0].Position);
        Assert.Equal(sim.Trucks[0].State, loaded.Trucks[0].State);
        Assert.All(loaded.Buildings, b => Assert.Equal(sim.BuildingById(b.Id)!.Online, b.Online));
        Assert.False(loaded.CanPlace(BuildingType.SmallHub, 41, 5, Facing.North));
    }

    [Fact]
    public void AGeneratedWorldWithFog_RoundTrips_AndKeepsGrowing()
    {
        var sim = new Simulation();
        sim.Debug.ResetToGeneratedWorld(42);
        sim.WalkTo(sim.Player.Position.X + 60f, sim.Player.Position.Z);
        SimTestHelpers.Seconds(sim, 10.0);
        sim.StopWalking();
        string saved = Json(sim);

        Simulation loaded = Simulation.FromSave(sim.Save());

        Assert.Equal(saved, Json(loaded));
        Assert.Equal(sim.ExploredCount, loaded.ExploredCount);
        Assert.Equal(sim.ExploredBounds, loaded.ExploredBounds);
        // Walking on generates the same new chunks in both.
        foreach (Simulation s in new[] { sim, loaded })
        {
            s.WalkTo(s.Player.Position.X, s.Player.Position.Z + 120f);
            SimTestHelpers.Seconds(s, 20.0);
        }
        Assert.Equal(Json(sim), Json(loaded));
    }

    [Fact]
    public void TheFile_RoundTrips_AndRefusesANewerVersionOrDamage()
    {
        Simulation sim = Busy();
        var file = new SaveFile
        {
            Header = new SaveHeader
            {
                GameVersion = "test",
                SavedAt = "2026-11-15T12:00:00",
                PlayTimeSeconds = 12.5,
                Seed = 0,
                Buildings = sim.Buildings.Count
            },
            Sim = sim.Save()
        };
        byte[] bytes = SaveFormat.Write(file);

        SaveFile read = SaveFormat.Read(bytes);
        Assert.Equal(12.5, read.Header.PlayTimeSeconds);
        Assert.Equal(SaveFormat.Json(file.Sim), SaveFormat.Json(read.Sim));

        file.Header.FormatVersion = SaveFormat.Version + 1;
        var newer = Assert.Throws<SaveFormatException>(() => SaveFormat.Read(SaveFormat.Write(file)));
        Assert.Equal("Saved by a newer version", newer.Message);
        var damaged = Assert.Throws<SaveFormatException>(() => SaveFormat.Read(Encoding.UTF8.GetBytes("not a save")));
        Assert.Equal("The save is damaged", damaged.Message);
    }
}
