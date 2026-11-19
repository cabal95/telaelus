using System;
using System.Linq;

namespace Telaelus.Sim.Tests;

public class BuildingDescriptionTests
{
    [Fact]
    public void EveryBuildableBuilding_HasAShortDescription()
    {
        foreach (BuildingType type in Enum.GetValues<BuildingType>().Where(t => BuildingDefs.For(t).Buildable))
        {
            string description = BuildingDefs.DescriptionOf(type);
            Assert.False(string.IsNullOrWhiteSpace(description), $"{type} has no description");
            Assert.True(description.Length < 80, $"{type}'s description is {description.Length} long");
            Assert.DoesNotContain("—", description);
            Assert.DoesNotContain("--", description);
        }
        Assert.Equal("", BuildingDefs.DescriptionOf(BuildingType.Hq));
    }
}
