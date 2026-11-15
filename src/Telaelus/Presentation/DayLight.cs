using Telaelus.Sim;
using Godot;

namespace Telaelus.Presentation;

/// <summary>
/// The world's one light by the time of day. It never moves: the art style's angle stays fixed, so
/// the hard shadows hold still. Only its color and energy follow the clock: white at midday, warm
/// when the sun is low at dawn and dusk, and a dim blue moonlight at night, blended by the daylight
/// so nothing jumps. There is no ambient light (art-style.md).
/// </summary>
public static class DayLight
{
    private static readonly Color Noon = new(1f, 1f, 1f);
    private static readonly Color LowSun = new(1f, 0.74f, 0.52f);
    private static readonly Color Moon = new(0.56f, 0.64f, 0.88f);
    private const float MoonEnergy = 0.76f;

    public static void Apply(DirectionalLight3D light, float timeOfDay)
    {
        float daylight = Simulation.DaylightAt(timeOfDay);
        // How high the sun stands over the lit part of the day (0.15 to 0.85): 0 at the horizon, 1 at midday.
        float along = Mathf.Clamp((timeOfDay - 0.15f) / 0.7f, 0f, 1f);
        float height = Mathf.Sin(Mathf.Pi * along);
        Color sunColor = LowSun.Lerp(Noon, Mathf.Clamp(height * 1.6f, 0f, 1f));
        light.LightColor = Moon.Lerp(sunColor, daylight);
        light.LightEnergy = Mathf.Lerp(MoonEnergy, 1f, daylight);
    }
}
