using System;

namespace Telaelus.Sim;

/// <summary>
/// The simulation's own seeded noise: integer hashing, smooth value noise, fractal
/// octaves, and ridged noise. Engine-free and deterministic, so world generation
/// gives the same result for the same seed everywhere it runs (including unit
/// tests). Every function takes a seed and a salt; different salts give unrelated
/// fields from one seed.
/// </summary>
public static class Noise
{
    /// <summary>A well-mixed 32-bit hash of a seed, salt, and integer position.</summary>
    public static uint Hash(int seed, int salt, int x, int z)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u;
            h ^= (uint)salt * 0x27D4EB2Fu;
            h ^= (uint)x * 0x85EBCA77u;
            h = (h << 13) | (h >> 19);
            h ^= (uint)z * 0xC2B2AE3Du;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }
    }

    /// <summary>A hash mapped to [0, 1).</summary>
    public static float Hash01(int seed, int salt, int x, int z)
    {
        return (Hash(seed, salt, x, z) & 0xFFFFFF) / 16777216f;
    }

    /// <summary>Smooth value noise in [-1, 1] at a point in noise space (lattice spacing 1).</summary>
    public static float Value(int seed, int salt, float x, float z)
    {
        int x0 = (int)MathF.Floor(x);
        int z0 = (int)MathF.Floor(z);
        float tx = Smooth(x - x0);
        float tz = Smooth(z - z0);
        float a = Lattice(seed, salt, x0, z0);
        float b = Lattice(seed, salt, x0 + 1, z0);
        float c = Lattice(seed, salt, x0, z0 + 1);
        float d = Lattice(seed, salt, x0 + 1, z0 + 1);
        float top = a + (b - a) * tx;
        float bottom = c + (d - c) * tx;
        return top + (bottom - top) * tz;
    }

    /// <summary>
    /// Fractal value noise in about [-1, 1]: <paramref name="octaves"/> layers, each
    /// at twice the frequency and half the amplitude of the last. <paramref name="scale"/>
    /// is the size of the largest features in world map cells.
    /// </summary>
    public static float Fbm(int seed, int salt, float x, float z, float scale, int octaves)
    {
        float sum = 0f;
        float amplitude = 1f;
        float total = 0f;
        float frequency = 1f / scale;
        for (int i = 0; i < octaves; i++)
        {
            sum += Value(seed, salt + i * 101, x * frequency, z * frequency) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }
        return sum / total;
    }

    /// <summary>Ridged noise in [0, 1]: high along thin lines where the fractal noise crosses zero.</summary>
    public static float Ridged(int seed, int salt, float x, float z, float scale, int octaves)
    {
        float ridge = 1f - MathF.Abs(Fbm(seed, salt, x, z, scale, octaves));
        return ridge * ridge;
    }

    private static float Lattice(int seed, int salt, int x, int z)
    {
        return Hash01(seed, salt, x, z) * 2f - 1f;
    }

    private static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
