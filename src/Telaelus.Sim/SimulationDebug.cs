using System;
using Godot;

namespace Telaelus.Sim;

public sealed partial class Simulation
{
    /// <summary>
    /// Setup operations the normal game never performs (resetting the map, editing
    /// terrain and fields, placing and removing things directly). Used by unit tests,
    /// scenarios, and the live control channel. Invalid requests throw
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    public DebugTools Debug { get; }

    /// <summary>
    /// Raised when terrain or fields change outside normal play (through
    /// <see cref="Debug"/>), so views built once at startup can rebuild.
    /// </summary>
    public event Action? MapChanged;

    public sealed class DebugTools
    {
        private readonly Simulation _sim;

        internal DebugTools(Simulation sim)
        {
            _sim = sim;
        }

        /// <summary>
        /// Resets to the bounded empty map: all-grass Grassland with no features, no
        /// fields or buildings, an empty stockpile, no launches, and the player at the
        /// map center. With fog, only the cells around the player start explored.
        /// </summary>
        public void ResetToEmptyMap(bool fog = false)
        {
            _sim.ResetWorld(WorldSettings.Empty with
            {
                Fog = fog
            });
            _sim.MapChanged?.Invoke();
        }

        /// <summary>Explores every map cell that exists (the bounded map, or every generated chunk).</summary>
        public void RevealAll()
        {
            _sim.RevealAllExisting();
            _sim.MapChanged?.Invoke();
        }

        /// <summary>
        /// Resets to a generated world from a seed on the starter planet, with nothing
        /// built, an empty stockpile, and the player at its spawn point.
        /// </summary>
        public void ResetToGeneratedWorld(int seed)
        {
            _sim.ResetWorld(WorldSettings.Generated(seed));
            _sim.MapChanged?.Invoke();
        }

        /// <summary>Sets the terrain of one map cell; throws if its feature cannot sit on the new terrain.</summary>
        public void SetTerrain(int mapX, int mapZ, TerrainType type)
        {
            RequireMapCell(mapX, mapZ);
            (Chunk chunk, int x, int z) = _sim.Cell(mapX, mapZ);
            MapFeature feature = chunk.Features[x, z];
            if (feature != MapFeature.None && !TerrainDefs.For(type).TakesFeatures)
            {
                throw new InvalidOperationException($"map cell ({mapX}, {mapZ}) has a {feature}, which cannot sit on {type}");
            }
            chunk.Terrain[x, z] = type;
            _sim.DiscoverIfExplored(mapX, mapZ);
            _sim.MapChanged?.Invoke();
        }

        /// <summary>Sets the biome of one map cell.</summary>
        public void SetBiome(int mapX, int mapZ, BiomeType biome)
        {
            RequireMapCell(mapX, mapZ);
            (Chunk chunk, int x, int z) = _sim.Cell(mapX, mapZ);
            chunk.Biomes[x, z] = biome;
            _sim.MapChanged?.Invoke();
        }

        /// <summary>
        /// Sets the feature of one map cell. A River needs a flow (the edge its
        /// water leaves by); other features take none. A feature needs terrain that
        /// takes features and a map cell with no field.
        /// </summary>
        public void SetFeature(int mapX, int mapZ, MapFeature feature, Facing? flow = null)
        {
            RequireMapCell(mapX, mapZ);
            if (feature == MapFeature.River && flow is null)
            {
                throw new InvalidOperationException("a River needs a flow");
            }
            if (feature != MapFeature.River && flow is not null)
            {
                throw new InvalidOperationException($"only a River takes a flow, not {feature}");
            }
            (Chunk chunk, int x, int z) = _sim.Cell(mapX, mapZ);
            if (feature != MapFeature.None)
            {
                TerrainType terrain = chunk.Terrain[x, z];
                if (!TerrainDefs.For(terrain).TakesFeatures)
                {
                    throw new InvalidOperationException($"a {feature} cannot sit on {terrain} at map cell ({mapX}, {mapZ})");
                }
                if (_sim._fieldsByMapCell.ContainsKey((mapX, mapZ)))
                {
                    throw new InvalidOperationException($"map cell ({mapX}, {mapZ}) has a field");
                }
            }

            chunk.Features[x, z] = feature;
            chunk.RiverFlow[x, z] = flow;
            _sim.DiscoverIfExplored(mapX, mapZ);
            _sim.MapChanged?.Invoke();
        }

        /// <summary>
        /// Adds a deposit field on an empty, featureless map cell. Without an amount it takes
        /// its biome's amount for the type (or the global default).
        /// </summary>
        public ResourceField PlaceField(ResourceType type, int mapX, int mapZ, float? amount = null)
        {
            RequireMapCell(mapX, mapZ);
            if (!ResourceDefs.For(type).HasFields)
            {
                throw new InvalidOperationException($"{type} is not a deposit and cannot be a resource field");
            }
            if (_sim._fieldsByMapCell.ContainsKey((mapX, mapZ)))
            {
                throw new InvalidOperationException($"map cell ({mapX}, {mapZ}) already has a field");
            }
            MapFeature existing = _sim.MapFeatureAt(mapX, mapZ);
            if (existing != MapFeature.None)
            {
                throw new InvalidOperationException($"map cell ({mapX}, {mapZ}) has a {existing}");
            }
            if (amount <= 0f)
            {
                throw new InvalidOperationException("a field's amount must be positive");
            }

            ResourceField field = _sim.AddField(type, mapX, mapZ, amount);
            _sim.MapChanged?.Invoke();
            return field;
        }

        /// <summary>Sets a field's remaining amount; 0 or less removes the field.</summary>
        public void SetFieldAmount(int mapX, int mapZ, float amount)
        {
            ResourceField field = RequireField(mapX, mapZ);
            if (amount <= 0f)
            {
                field.Amount = 0f;
                _sim.RemoveField(field);
            }
            else
            {
                field.Amount = amount;
            }
            _sim.MapChanged?.Invoke();
        }

        /// <summary>Removes the field on a map cell.</summary>
        public void RemoveField(int mapX, int mapZ)
        {
            ResourceField field = RequireField(mapX, mapZ);
            field.Amount = 0f;
            _sim.RemoveField(field);
            _sim.MapChanged?.Invoke();
        }

        /// <summary>
        /// Places a building by the spatial placement rule (<see cref="CanPlace"/>), free
        /// of cost and whether or not it is unlocked.
        /// </summary>
        public Building PlaceBuilding(BuildingType type, int anchorX, int anchorZ, Facing facing)
        {
            if (!_sim.CanPlace(type, anchorX, anchorZ, facing))
            {
                throw new InvalidOperationException($"cannot place {type} at ({anchorX}, {anchorZ}) facing {facing}");
            }
            return _sim.AddBuilding(type, anchorX, anchorZ, facing);
        }

        /// <summary>Removes the building anchored at a fine cell and frees its cells.</summary>
        public void RemoveBuilding(int anchorX, int anchorZ)
        {
            Building? building = _sim._buildings.Find(b => b.AnchorX == anchorX && b.AnchorZ == anchorZ);
            if (building is null)
            {
                throw new InvalidOperationException($"no building anchored at ({anchorX}, {anchorZ})");
            }

            _sim.RemoveBuilding(building);
        }

        /// <summary>Sets one resource's amount in the output buffer of the building anchored at a fine cell (0 removes it).</summary>
        public void FillBuffer(int anchorX, int anchorZ, ResourceType type, float amount)
        {
            Building? building = _sim._buildings.Find(b => b.AnchorX == anchorX && b.AnchorZ == anchorZ);
            if (building is null)
            {
                throw new InvalidOperationException($"no building anchored at ({anchorX}, {anchorZ})");
            }
            if (amount <= 0f)
            {
                building.Output.Remove(type);
            }
            else
            {
                building.Output[type] = amount;
            }
        }

        /// <summary>Lays a road line free, every layable tile (the drag's L from one cell to another); throws when none can be laid.</summary>
        public void PlaceRoad(int fromX, int fromZ, int toX, int toZ)
        {
            _sim.PlaceRoadForDebug(fromX, fromZ, toX, toZ);
        }

        /// <summary>Lays a bridge span free; throws when it cannot stand there.</summary>
        public void PlaceBridge(int fromX, int fromZ, int toX, int toZ)
        {
            _sim.PlaceBridgeForDebug(fromX, fromZ, toX, toZ);
        }

        /// <summary>Sets a project's work done, in game seconds; at its full work it completes (its fee counted as paid).</summary>
        public void SetResearchProgress(ResearchProject project, float seconds)
        {
            if (seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "research progress is 0 or more");
            }
            _sim.SetResearchProgressForDebug(project, seconds);
        }

        /// <summary>Sets the time of day (0 to 1; 0.5 is midday).</summary>
        public void SetTimeOfDay(float timeOfDay)
        {
            if (timeOfDay < 0f || timeOfDay >= 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeOfDay), timeOfDay, "the time of day is from 0 up to 1");
            }
            _sim.TimeOfDay = timeOfDay;
        }

        /// <summary>Chooses the recipe of the building anchored at a fine cell, as its picker does.</summary>
        public void SetRecipe(int anchorX, int anchorZ, int index)
        {
            Building? building = _sim._buildings.Find(b => b.AnchorX == anchorX && b.AnchorZ == anchorZ);
            if (building is null || !_sim.SetRecipe(building.Id, index))
            {
                throw new InvalidOperationException($"no building at ({anchorX}, {anchorZ}) with recipe {index}");
            }
        }

        /// <summary>Marks a map cell worked out, as if a field of the deposit had depleted there.</summary>
        public void SetWorkedOut(int mapX, int mapZ, ResourceType deposit)
        {
            _sim._workedOut[(mapX, mapZ)] = deposit;
        }

        /// <summary>Places a motherlode on the 2 x 2 map cells from a corner; throws when any is taken.</summary>
        public ResourceField PlaceMotherlode(MotherlodeType kind, int mapX, int mapZ)
        {
            for (int x = mapX; x < mapX + 2; x++)
            {
                for (int z = mapZ; z < mapZ + 2; z++)
                {
                    if (_sim._fieldsByMapCell.ContainsKey((x, z)))
                    {
                        throw new InvalidOperationException($"map cell ({x}, {z}) already has a field");
                    }
                }
            }
            return _sim.AddMotherlode(kind, mapX, mapZ);
        }

        /// <summary>Moves the player to a world position on the ground.</summary>
        public void TeleportPlayer(float x, float z)
        {
            _sim.StopWalking();
            _sim.Player.Position = new Vector3(x, 0f, z);
        }

        /// <summary>Adds an amount to the stockpile without playing for it; it unlocks any building whose whole cost the stockpile then holds.</summary>
        public void SeedStockpile(ResourceType type, float amount)
        {
            _sim.AddToStockpile(type, amount);
        }

        private void RequireMapCell(int mapX, int mapZ)
        {
            if (!_sim.MapInBounds(mapX, mapZ))
            {
                throw new InvalidOperationException($"map cell ({mapX}, {mapZ}) is out of bounds");
            }
        }

        private ResourceField RequireField(int mapX, int mapZ)
        {
            if (!_sim._fieldsByMapCell.TryGetValue((mapX, mapZ), out ResourceField? field))
            {
                throw new InvalidOperationException($"no field on map cell ({mapX}, {mapZ})");
            }
            return field;
        }
    }
}
