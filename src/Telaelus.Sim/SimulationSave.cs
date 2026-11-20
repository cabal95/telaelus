using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Telaelus.Sim;

/// <summary>
/// Saving and loading the sim's whole state (<see cref="SaveData"/>). A loaded sim continues exactly
/// as the saved one would have: every chunk, field, building, truck (with its path), and counter is
/// restored as it was, and what is derived (who is online, the occupied cells) is rebuilt. The
/// player's held actions (harvesting, collecting by hand) are not saved.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>A sim loaded from a save.</summary>
    public static Simulation FromSave(SaveData data)
    {
        var sim = new Simulation(WorldSettings.Empty);
        sim.Load(data);
        return sim;
    }

    public SaveData Save()
    {
        var data = new SaveData
        {
            World = new WorldData
            {
                Kind = World.Kind,
                Seed = World.Seed,
                Planet = World.Planet?.Name,
                Fog = World.Fog
            },
            ShipsLaunched = ShipsLaunched,
            TimeOfDay = TimeOfDay,
            Player = Floats(Player.Position),
            RevealedFrom = _revealedFrom is (int rx, int rz) ? new[] { rx, rz } : null,
            NextBuildingId = _nextBuildingId,
            NextFieldId = _nextFieldId,
            NextTruckId = _nextTruckId,
            NextBridgeId = _nextBridgeId,
            Unlocked = _unlocked.OrderBy(t => t).ToList(),
            Discovered = _discovered.OrderBy(t => t).ToList(),
            Stockpile = Amounts(_stockpile)
        };
        foreach (((int cx, int cz), Chunk chunk) in _chunks.OrderBy(p => p.Key.ChunkX).ThenBy(p => p.Key.ChunkZ))
        {
            data.Chunks.Add(SaveChunk(cx, cz, chunk));
        }
        foreach (ResourceField f in _fields)
        {
            data.Fields.Add(new FieldData
            {
                Id = f.Id,
                Type = f.Type,
                Motherlode = f.Motherlode,
                MapX = f.MapX,
                MapZ = f.MapZ,
                InitialAmount = f.InitialAmount,
                Amount = f.Amount,
                Owner = f.Owner?.Id,
                Growing = f.Growing,
                GrowLeft = f.GrowLeft
            });
        }
        foreach (((int x, int z), ResourceType deposit) in _workedOut)
        {
            data.WorkedOut.Add(new WorkedOutData
            {
                MapX = x,
                MapZ = z,
                Deposit = deposit
            });
        }
        foreach (Building b in _buildings)
        {
            data.Buildings.Add(SaveBuilding(b));
        }
        foreach (Truck t in _trucks)
        {
            data.Trucks.Add(new TruckData
            {
                Id = t.Id,
                Depot = t.Depot.Id,
                Bay = t.Bay,
                CameWithDepot = t.CameWithDepot,
                Position = Floats(t.Position),
                Heading = Floats(t.Heading),
                State = t.State,
                Cargo = t.Cargo,
                Amount = t.Amount,
                Target = t.Target?.Id,
                Timer = t.Timer,
                Path = t.Path.SelectMany(p => new[] { p.X, p.Z }).ToArray(),
                PathIndex = t.PathIndex
            });
        }
        data.Unreachable = _unreachable.OrderBy(p => p.Collector).ThenBy(p => p.Truck).Select(p => new[] { p.Collector, p.Truck }).ToList();
        data.Research = new ResearchData
        {
            Active = ActiveResearch,
            Projects = ResearchDefs.All.Select(p => new ResearchProgressData
            {
                Project = p,
                Progress = _researchProgress.GetValueOrDefault(p),
                Paid = _researchPaid.Contains(p),
                Complete = _researched.Contains(p)
            }).ToList()
        };
        data.RoadTiles = _roadTiles.OrderBy(t => t.Z).ThenBy(t => t.X).Select(t => new[] { t.X, t.Z }).ToList();
        data.Bridges = _bridges.Select(b => new BridgeData
        {
            Id = b.Id,
            AlongX = b.AlongX,
            Tiles = b.Tiles.Select(t => new[] { t.X, t.Z }).ToList()
        }).ToList();
        foreach (CapacityType type in CapacityTypes)
        {
            CapacityReading r = _capacity[(int)type];
            data.Capacity.Add(new CapacityData
            {
                Type = type,
                Supply = r.Supply,
                Demand = r.Demand,
                Speed = r.Speed,
                State = r.State,
                Stored = r.Stored,
                StoreCapacity = r.StoreCapacity,
                StoreFlow = r.StoreFlow
            });
        }
        return data;
    }

    /// <summary>Replaces the whole state with a save's (see <see cref="Save"/>).</summary>
    public void Load(SaveData data)
    {
        Quietly(() => LoadNow(data));
    }

    private void LoadNow(SaveData data)
    {
        _blackoutRaised.Clear();
        PlanetDef? planet = data.World.Planet is null ? null : PlanetDefs.Starter;
        World = new WorldSettings(data.World.Kind, data.World.Seed, planet)
        {
            Fog = data.World.Fog
        };
        _generator = World.Kind == WorldKind.Generated ? new WorldGenerator(World.Seed, World.Planet!) : null;
        _chunks.Clear();
        _fields.Clear();
        _fieldsByMapCell.Clear();
        _buildings.Clear();
        _workedOut.Clear();
        _trucks.Clear();
        _unreachable.Clear();
        _occupied.Clear();
        _stockpile.Clear();
        _unlocked.Clear();
        _discovered.Clear();
        ResetResearch();
        ResetRoads();
        HarvestTarget = null;
        HarvestProgress = 0f;
        InteractTarget = null;
        InteractProgress = 0f;

        foreach (ChunkData c in data.Chunks)
        {
            _chunks[(c.X, c.Z)] = LoadChunk(c);
        }

        var buildings = new Dictionary<int, Building>();
        foreach (BuildingData d in data.Buildings)
        {
            var b = new Building(d.Id, d.Type, d.AnchorX, d.AnchorZ, d.Facing, Telaelus.Sim.Recipes.For(d.Type))
            {
                Enabled = d.Enabled,
                RecipeIndex = d.RecipeIndex,
                PendingRecipe = d.PendingRecipe,
                Burning = d.Burning,
                BurnLeft = d.BurnLeft,
                Charge = d.Charge,
                FuelChoice = d.FuelChoice,
                SiloResource = d.SiloResource,
                Crafting = d.Crafting,
                Progress = d.Progress,
                OutputChoice = d.OutputChoice,
                SiteDeposit = d.SiteDeposit,
                PlotWait = d.PlotWait,
                FeedLeft = d.FeedLeft,
                Starved = d.Starved
            };
            foreach (int[] choice in d.InputChoices)
            {
                if (choice.Length == 2)
                {
                    b.InputChoices[(d.RecipeIndex, choice[0])] = (ResourceType)choice[1];
                }
                else
                {
                    b.InputChoices[(choice[0], choice[1])] = (ResourceType)choice[2];
                }
            }
            foreach (int[] order in d.Queue)
            {
                b.Queue.Add(new CraftOrder(order[0], order[1]));
            }
            if (b.Queues)
            {
                // An older save's queueing processor chose its recipe directly; its orders choose now.
                b.PendingRecipe = null;
            }
            b.CycleInputs.AddRange(d.CycleInputs.Select(a => new ResourceAmount(a.Type, a.Amount)));
            Fill(b.Output, d.Output);
            Fill(b.Input, d.Input);
            Fill(b.FuelSlot, d.FuelSlot);
            _buildings.Add(b);
            buildings[b.Id] = b;
            for (int x = b.AnchorX; x < b.AnchorX + b.Width; x++)
            {
                for (int z = b.AnchorZ; z < b.AnchorZ + b.Depth; z++)
                {
                    _occupied.Add((x, z));
                }
            }
        }

        var fields = new Dictionary<int, ResourceField>();
        foreach (FieldData d in data.Fields)
        {
            Building? owner = d.Owner is int id ? buildings[id] : null;
            ResourceField field = d.Motherlode is MotherlodeType kind
                ? new ResourceField(d.Id, kind, d.MapX, d.MapZ)
                {
                    Owner = owner
                }
                : new ResourceField(d.Id, d.Type, d.MapX, d.MapZ, d.InitialAmount)
                {
                    Owner = owner
                };
            field.Amount = d.Amount;
            field.Growing = d.Growing;
            field.GrowLeft = d.GrowLeft;
            RegisterField(field);
            fields[field.Id] = field;
        }
        foreach (BuildingData d in data.Buildings)
        {
            Building b = buildings[d.Id];
            b.CurrentField = d.CurrentField is int cf ? fields[cf] : null;
            b.SiteMotherlode = d.SiteMotherlode is int sm ? fields[sm] : null;
            b.Plot = d.Plot is int pl ? fields[pl] : null;
        }

        foreach (WorkedOutData w in data.WorkedOut)
        {
            _workedOut[(w.MapX, w.MapZ)] = w.Deposit;
        }
        foreach (TruckData d in data.Trucks)
        {
            var truck = new Truck(d.Id, buildings[d.Depot], d.Bay, d.CameWithDepot, Vector(d.Position))
            {
                Heading = Vector(d.Heading),
                State = d.State,
                Cargo = d.Cargo,
                Amount = d.Amount,
                Target = d.Target is int target ? buildings[target] : null,
                Timer = d.Timer
            };
            var path = new List<Vector3>();
            for (int i = 0; i + 1 < d.Path.Length; i += 2)
            {
                path.Add(new Vector3(d.Path[i], 0f, d.Path[i + 1]));
            }
            truck.Path = path;
            truck.PathIndex = d.PathIndex;
            _trucks.Add(truck);
        }
        foreach (int[] pair in data.Unreachable)
        {
            _unreachable.Add((pair[0], pair[1]));
        }
        Fill(_stockpile, data.Stockpile);
        _unlocked.UnionWith(data.Unlocked);
        _discovered.Clear();
        _discovered.UnionWith(data.Discovered);
        ShipsLaunched = data.ShipsLaunched;
        TimeOfDay = data.TimeOfDay;
        ActiveResearch = data.Research.Active;
        foreach (ResearchProgressData p in data.Research.Projects)
        {
            if (p.Progress > 0)
            {
                _researchProgress[p.Project] = p.Progress;
            }
            if (p.Paid)
            {
                _researchPaid.Add(p.Project);
            }
            if (p.Complete)
            {
                _researched.Add(p.Project);
            }
        }
        foreach (int[] t in data.RoadTiles)
        {
            _roadTiles.Add((t[0], t[1]));
            foreach ((int, int) cell in CellsOf((t[0], t[1])))
            {
                _roadCells.Add(cell);
            }
        }
        foreach (BridgeData d in data.Bridges)
        {
            var bridge = new Bridge(d.Id, d.Tiles.Select(t => (t[0], t[1])).ToList(), d.AlongX);
            _bridges.Add(bridge);
            foreach ((int X, int Z) tile in bridge.Tiles)
            {
                foreach ((int, int) cell in CellsOf(tile))
                {
                    _bridgeCells[cell] = bridge;
                }
            }
        }
        StopWalking();
        Player.Position = Vector(data.Player);
        _revealedFrom = data.RevealedFrom is { Length: 2 } from ? (from[0], from[1]) : null;
        _nextBuildingId = data.NextBuildingId;
        _nextFieldId = data.NextFieldId;
        _nextTruckId = data.NextTruckId;
        _nextBridgeId = data.NextBridgeId;
        foreach (CapacityData c in data.Capacity)
        {
            _capacity[(int)c.Type] = new CapacityReading(c.Supply, c.Demand, c.Speed, c.State, c.Stored, c.StoreCapacity, c.StoreFlow);
        }

        RecountExplored();
        RecomputeNetwork();
        RoadVersion++;
        MapChanged?.Invoke();
    }

    /// <summary>Recounts the explored cells and their bounds from the chunks (fog worlds).</summary>
    private void RecountExplored()
    {
        ExploredCount = 0;
        _exploredBounds = null;
        if (!World.Fog)
        {
            return;
        }
        foreach (Chunk chunk in _chunks.Values)
        {
            for (int x = 0; x < Chunk.Size; x++)
            {
                for (int z = 0; z < Chunk.Size; z++)
                {
                    if (!chunk.Explored[x, z])
                    {
                        continue;
                    }
                    int mapX = Chunk.Origin(chunk.ChunkX) + x;
                    int mapZ = Chunk.Origin(chunk.ChunkZ) + z;
                    ExploredCount++;
                    _exploredBounds = _exploredBounds is (int minX, int minZ, int maxX, int maxZ)
                        ? (Math.Min(minX, mapX), Math.Min(minZ, mapZ), Math.Max(maxX, mapX), Math.Max(maxZ, mapZ))
                        : (mapX, mapZ, mapX, mapZ);
                }
            }
        }
    }

    private static ChunkData SaveChunk(int cx, int cz, Chunk chunk)
    {
        int n = Chunk.Size * Chunk.Size;
        var data = new ChunkData
        {
            X = cx,
            Z = cz,
            Terrain = new byte[n],
            Biomes = new byte[n],
            Features = new byte[n],
            Flows = new byte[n],
            Explored = new byte[n]
        };
        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int z = 0; z < Chunk.Size; z++)
            {
                int i = x * Chunk.Size + z;
                data.Terrain[i] = (byte)chunk.Terrain[x, z];
                data.Biomes[i] = (byte)chunk.Biomes[x, z];
                data.Features[i] = (byte)chunk.Features[x, z];
                data.Flows[i] = chunk.RiverFlow[x, z] is Facing flow ? (byte)flow : (byte)255;
                data.Explored[i] = chunk.Explored[x, z] ? (byte)1 : (byte)0;
            }
        }
        return data;
    }

    private static Chunk LoadChunk(ChunkData data)
    {
        var chunk = new Chunk(data.X, data.Z);
        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int z = 0; z < Chunk.Size; z++)
            {
                int i = x * Chunk.Size + z;
                chunk.Terrain[x, z] = (TerrainType)data.Terrain[i];
                chunk.Biomes[x, z] = (BiomeType)data.Biomes[i];
                chunk.Features[x, z] = (MapFeature)data.Features[i];
                chunk.RiverFlow[x, z] = data.Flows[i] == 255 ? null : (Facing)data.Flows[i];
                chunk.Explored[x, z] = data.Explored[i] != 0;
            }
        }
        return chunk;
    }

    private static BuildingData SaveBuilding(Building b)
    {
        return new BuildingData
        {
            Id = b.Id,
            Type = b.Type,
            AnchorX = b.AnchorX,
            AnchorZ = b.AnchorZ,
            Facing = b.Facing,
            Enabled = b.Enabled,
            RecipeIndex = b.RecipeIndex,
            PendingRecipe = b.PendingRecipe,
            InputChoices = b.InputChoices.Select(p => new[] { p.Key.Recipe, p.Key.Input, (int)p.Value }).ToList(),
            Queue = b.Queue.Select(o => new[] { o.RecipeIndex, o.Remaining }).ToList(),
            CycleInputs = b.CycleInputs.Select(a => new AmountData(a.Type, a.Amount)).ToList(),
            Output = Amounts(b.Output),
            Input = Amounts(b.Input),
            FuelSlot = Amounts(b.FuelSlot),
            Burning = b.Burning,
            BurnLeft = b.BurnLeft,
            Charge = b.Charge,
            FuelChoice = b.FuelChoice,
            SiloResource = b.SiloResource,
            CurrentField = b.CurrentField?.Id,
            Crafting = b.Crafting,
            Progress = b.Progress,
            OutputChoice = b.OutputChoice,
            SiteMotherlode = b.SiteMotherlode?.Id,
            SiteDeposit = b.SiteDeposit,
            Plot = b.Plot?.Id,
            PlotWait = b.PlotWait,
            FeedLeft = b.FeedLeft,
            Starved = b.Starved
        };
    }

    private static List<AmountData> Amounts(Dictionary<ResourceType, float> amounts)
    {
        // In the dictionary's own order: the sim drains and spends in it, so a loaded game keeps it.
        return amounts.Select(p => new AmountData(p.Key, p.Value)).ToList();
    }

    private static void Fill(Dictionary<ResourceType, float> into, List<AmountData> amounts)
    {
        into.Clear();
        foreach (AmountData a in amounts)
        {
            into[a.Type] = a.Amount;
        }
    }

    private static float[] Floats(Vector3 v)
    {
        return new[] { v.X, v.Y, v.Z };
    }

    private static Vector3 Vector(float[] v)
    {
        return new Vector3(v[0], v[1], v[2]);
    }
}
