using System;
using Godot;

namespace Game.Sim;

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
        /// Resets to an empty, all-grass Grassland map with no features: no fields or
        /// buildings, an empty stockpile, no launches, and the player at the map center.
        /// </summary>
        public void ResetToEmptyMap()
        {
            for (int mapX = 0; mapX < _sim.MapWidth; mapX++)
            {
                for (int mapZ = 0; mapZ < _sim.MapHeight; mapZ++)
                {
                    _sim._terrain[mapX, mapZ] = TerrainType.Grass;
                    _sim._biomes[mapX, mapZ] = BiomeType.Grassland;
                    _sim._features[mapX, mapZ] = MapFeature.None;
                    _sim._riverFlow[mapX, mapZ] = null;
                }
            }

            _sim._fields.Clear();
            _sim._fieldsByMapCell.Clear();
            _sim._buildings.Clear();
            _sim._occupied.Clear();
            _sim._stockpile.Clear();
            _sim.HarvestTarget = null;
            _sim.ShipsLaunched = 0;
            _sim.Player.Position = new Vector3(_sim.GridWidth * 0.5f, 0f, _sim.GridHeight * 0.5f);
            _sim.MapChanged?.Invoke();
        }

        /// <summary>Sets the terrain of one map cell; throws if its feature cannot sit on the new terrain.</summary>
        public void SetTerrain(int mapX, int mapZ, TerrainType type)
        {
            RequireMapCell(mapX, mapZ);
            MapFeature feature = _sim._features[mapX, mapZ];
            if (feature != MapFeature.None && !TerrainDefs.For(type).TakesFeatures)
            {
                throw new InvalidOperationException($"map cell ({mapX}, {mapZ}) has a {feature}, which cannot sit on {type}");
            }
            _sim._terrain[mapX, mapZ] = type;
            _sim.MapChanged?.Invoke();
        }

        /// <summary>Sets the biome of one map cell.</summary>
        public void SetBiome(int mapX, int mapZ, BiomeType biome)
        {
            RequireMapCell(mapX, mapZ);
            _sim._biomes[mapX, mapZ] = biome;
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
            if (feature != MapFeature.None)
            {
                TerrainType terrain = _sim._terrain[mapX, mapZ];
                if (!TerrainDefs.For(terrain).TakesFeatures)
                {
                    throw new InvalidOperationException($"a {feature} cannot sit on {terrain} at map cell ({mapX}, {mapZ})");
                }
                if (_sim._fieldsByMapCell.ContainsKey((mapX, mapZ)))
                {
                    throw new InvalidOperationException($"map cell ({mapX}, {mapZ}) has a field");
                }
            }

            _sim._features[mapX, mapZ] = feature;
            _sim._riverFlow[mapX, mapZ] = flow;
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
            if (_sim._features[mapX, mapZ] != MapFeature.None)
            {
                throw new InvalidOperationException($"map cell ({mapX}, {mapZ}) has a {_sim._features[mapX, mapZ]}");
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

        /// <summary>Places a building through the normal placement rules.</summary>
        public Building PlaceBuilding(BuildingType type, int anchorX, int anchorZ, Facing facing)
        {
            if (!_sim.TryPlaceBuilding(type, anchorX, anchorZ, facing))
            {
                throw new InvalidOperationException($"cannot place {type} at ({anchorX}, {anchorZ}) facing {facing}");
            }
            return _sim._buildings[^1];
        }

        /// <summary>Removes the building anchored at a fine cell and frees its cells.</summary>
        public void RemoveBuilding(int anchorX, int anchorZ)
        {
            Building? building = _sim._buildings.Find(b => b.AnchorX == anchorX && b.AnchorZ == anchorZ);
            if (building is null)
            {
                throw new InvalidOperationException($"no building anchored at ({anchorX}, {anchorZ})");
            }

            _sim._buildings.Remove(building);
            for (int x = anchorX; x < anchorX + building.Width; x++)
            {
                for (int z = anchorZ; z < anchorZ + building.Depth; z++)
                {
                    _sim._occupied.Remove((x, z));
                }
            }
        }

        /// <summary>Moves the player to a world position on the ground.</summary>
        public void TeleportPlayer(float x, float z)
        {
            _sim.Player.Position = new Vector3(x, 0f, z);
        }

        /// <summary>Adds an amount to the stockpile without playing for it.</summary>
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
