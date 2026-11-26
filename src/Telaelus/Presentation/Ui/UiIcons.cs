namespace Telaelus.Presentation.Ui;

/// <summary>The icon models (<c>icons/&lt;name&gt;</c>) that stand for no resource or project.</summary>
public static class UiIcons
{
    public static readonly string[] Fixed = { "sun", "moon", "power" };

    /// <summary>Icons drawn from other models: the build menu's road and bridge cells.</summary>
    public static readonly System.Collections.Generic.IReadOnlyDictionary<string, string> Pictures =
        new System.Collections.Generic.Dictionary<string, string>
        {
            ["road"] = "infrastructure/road_cross",
            ["bridge"] = "infrastructure/bridge_deck"
        };
}
