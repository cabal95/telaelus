namespace Telaelus.Sim;

/// <summary>
/// Accumulates real frame time and releases it in fixed-size steps, so the
/// simulation advances at a constant rate no matter the render framerate.
/// This is the backbone of a deterministic-feeling, network-ready sim.
/// </summary>
public sealed class SimClock
{
    /// <summary>Seconds per simulation step.</summary>
    public double FixedDelta { get; }

    private double _accumulator;

    public SimClock(double ticksPerSecond)
    {
        FixedDelta = 1.0 / ticksPerSecond;
    }

    /// <summary>
    /// Feed elapsed real time; returns how many fixed steps to run this frame.
    /// </summary>
    public int Advance(double realDelta)
    {
        _accumulator += realDelta;

        // Clamp so a long stall (breakpoint, hitch) cannot trigger a runaway
        // catch-up loop ("spiral of death").
        double max = FixedDelta * 8;
        if (_accumulator > max)
        {
            _accumulator = max;
        }

        int steps = 0;
        while (_accumulator >= FixedDelta)
        {
            _accumulator -= FixedDelta;
            steps++;
        }
        return steps;
    }

    /// <summary>
    /// Fraction into the next pending step [0, 1). Reserved for presentation
    /// interpolation later so motion stays smooth above the tick rate.
    /// </summary>
    public float Alpha => (float)(_accumulator / FixedDelta);
}
