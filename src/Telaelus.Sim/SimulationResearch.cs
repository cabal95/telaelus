using System;
using System.Collections.Generic;
using System.Linq;

namespace Telaelus.Sim;

/// <summary>
/// Research, global to the base: one project runs at a time and gains work each tick at the research
/// speed, which comes from the working Research Labs, each one less than the one before (half, down
/// to a floor of a quarter). Every project keeps its progress and whether its fee is paid, so pausing
/// or switching loses nothing and a fee is charged once. A complete project's effect lasts: the
/// transfer projects let online collectors drain their resources into storage.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>The least a lab past the first few adds to research speed.</summary>
    public const float LabFloor = 0.25f;

    // Kept in double: a project runs for tens of thousands of ticks, and float sums drift by seconds.
    private readonly Dictionary<ResearchProject, double> _researchProgress = new();
    private readonly HashSet<ResearchProject> _researchPaid = new();
    private readonly HashSet<ResearchProject> _researched = new();

    /// <summary>The project running now, or null.</summary>
    public ResearchProject? ActiveResearch { get; private set; }

    /// <summary>Game seconds of work done on a project (at one lab's speed).</summary>
    public float ResearchProgress(ResearchProject project)
    {
        return (float)_researchProgress.GetValueOrDefault(project);
    }

    public bool ResearchFeePaid(ResearchProject project)
    {
        return _researchPaid.Contains(project);
    }

    /// <summary>
    /// The highest unlocked tier, 1 to 4: tier 1 from the start, and one more for each completed
    /// research project that unlocks a tier. It comes from research and is never stored.
    /// </summary>
    public int UnlockedTier => 1 + _researched.Count(project => ResearchDefs.For(project).Unlocks is not null);

    public bool IsResearched(ResearchProject project)
    {
        return _researched.Contains(project);
    }

    public ResearchState StateOf(ResearchProject project)
    {
        if (_researched.Contains(project))
        {
            return ResearchState.Complete;
        }
        if (ActiveResearch == project)
        {
            return ResearchState.Running;
        }
        ResearchDef def = ResearchDefs.For(project);
        if (def.Tier > UnlockedTier || !def.Prerequisites.All(_researched.Contains))
        {
            return ResearchState.Locked;
        }
        return _researchPaid.Contains(project) || ResearchProgress(project) > 0f ? ResearchState.Paused : ResearchState.Available;
    }

    /// <summary>Whether a project can start now: incomplete, its prerequisites done, and its fee paid or held in storage.</summary>
    public bool CanStartResearch(ResearchProject project)
    {
        ResearchState state = StateOf(project);
        if (state is ResearchState.Locked or ResearchState.Complete or ResearchState.Running)
        {
            return false;
        }
        return _researchPaid.Contains(project) || HasAll(ResearchDefs.For(project).Fee);
    }

    /// <summary>
    /// Makes a project the active one, paying its fee the first time; any other running project pauses
    /// with its progress kept. False when it cannot start (see <see cref="CanStartResearch"/>).
    /// </summary>
    public bool StartResearch(ResearchProject project)
    {
        if (!CanStartResearch(project))
        {
            return false;
        }
        if (_researchPaid.Add(project))
        {
            foreach (ResourceAmount cost in ResearchDefs.For(project).Fee)
            {
                AddToStockpile(cost.Type, -cost.Amount);
            }
        }
        ActiveResearch = project;
        return true;
    }

    /// <summary>Stops the running project; its progress and paid fee stay.</summary>
    public void PauseResearch()
    {
        ActiveResearch = null;
    }

    /// <summary>The working labs (switched on, online, and running at some speed), fastest first.</summary>
    public IReadOnlyList<Building> WorkingLabs => _buildings
        .Where(b => b.Def.Researches && b.Enabled && b.Online && WorkSpeed(b) > 0f)
        .OrderByDescending(WorkSpeed)
        .ToList();

    /// <summary>The research speed: the fastest lab adds its speed, each after it half the one before, never under a quarter.</summary>
    public float ResearchSpeed => SpeedOf(WorkingLabs.Select(WorkSpeed));

    /// <summary>The research speed of labs running at these speeds, fastest first.</summary>
    public static float SpeedOf(IEnumerable<float> labSpeeds)
    {
        float total = 0f;
        float weight = 1f;
        foreach (float speed in labSpeeds)
        {
            total += speed * weight;
            weight = Math.Max(LabFloor, weight * 0.5f);
        }
        return total;
    }

    /// <summary>Whether the network carries a raw resource from online collectors (its transfer project is complete).</summary>
    public bool Transfers(ResourceType type)
    {
        foreach (ResearchProject project in _researched)
        {
            if (ResearchDefs.For(project).Transfers.Contains(type))
            {
                return true;
            }
        }
        return false;
    }

    private void StepResearch(double dt)
    {
        if (ActiveResearch is not ResearchProject project)
        {
            return;
        }
        double work = ResearchDefs.For(project).Work;
        double done = Math.Min(work, _researchProgress.GetValueOrDefault(project) + dt * ResearchSpeed);
        _researchProgress[project] = done;
        if (done >= work)
        {
            _researched.Add(project);
            ActiveResearch = null;
            Raise(new SimEvent(SimEventKind.ResearchComplete)
            {
                Project = project
            });
        }
    }

    /// <summary>Moves what fits of each online collector's transferred resources from its buffer into storage.</summary>
    private void TransferFromCollectors()
    {
        if (_researched.Count == 0)
        {
            return;
        }
        foreach (Building building in _buildings)
        {
            if (!building.Enabled || !building.Online || !building.Def.IsCollector)
            {
                continue;
            }
            foreach (ResourceType type in new List<ResourceType>(building.Output.Keys))
            {
                if (!Transfers(type))
                {
                    continue;
                }
                float move = Math.Min(Held(building.Output, type), MathF.Floor(RoomFor(type) + RoomTolerance));
                if (move > 0f)
                {
                    Put(building.Output, type, -move);
                    AddToStockpile(type, move);
                }
            }
        }
    }

    private void ResetResearch()
    {
        _researchProgress.Clear();
        _researchPaid.Clear();
        _researched.Clear();
        ActiveResearch = null;
    }

    private void SetResearchProgressForDebug(ResearchProject project, float seconds)
    {
        float work = ResearchDefs.For(project).Work;
        _researchProgress[project] = Math.Clamp(seconds, 0f, work);
        if (seconds >= work)
        {
            _researched.Add(project);
            _researchPaid.Add(project);
            if (ActiveResearch == project)
            {
                ActiveResearch = null;
            }
        }
    }
}
