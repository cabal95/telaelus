using System.Reflection;
using System.Text.RegularExpressions;

namespace Telaelus.Sim.Tests;

public class VersionTests
{
    [Fact]
    public void TheSimAssembly_CarriesTheVersionFromTheGitTags()
    {
        string? informational = typeof(Simulation).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // MinVer's version ("0.2.0", "0.2.1-dev.5"), then the commit; never the SDK's default 1.0.0.
        Assert.NotNull(informational);
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?\+[0-9a-f]{40}$"), informational);
        Assert.DoesNotMatch(new Regex(@"^1\.0\.0\+"), informational);
    }
}
