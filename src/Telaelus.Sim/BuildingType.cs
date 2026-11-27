namespace Telaelus.Sim;

/// <summary>
/// Kinds of building: every one the player can place, and the HQ, which the world places. A leveled
/// family (the Mine, Lumber Camp, Water Pump, Sand Collector, Oil Well, Smelter, and Kiln) is one
/// type with several levels (see <see cref="BuildingDefs.For(BuildingType, int)"/>).
/// </summary>
public enum BuildingType
{
    Mine,
    LumberCamp,
    Smelter,
    Refinery,
    OilWell,
    Hq,
    Silo,
    Stockyard,
    TankFarm,
    Warehouse,
    Vault,
    SmallHub,
    Relay,
    WaterWheel,
    BurnerGenerator,
    WaterPump,
    SandCollector,
    Glassworks,
    Depot,
    Forester,
    Quarry,
    Kiln,
    RockCrusher,
    Electrolyzer,
    FuelPlant,
    ChemicalPlant,
    Fabricator,
    ElectronicsWorks,
    AssemblyPlant,
    Shipyard,
    Sawmill,
    ConcreteMixer,
    BrineWell,
    SemiconductorFab,
    EnrichmentPlant,
    SolarArray,
    BatteryBank,
    FissionReactor,
    ResearchLab
}
