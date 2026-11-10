namespace Game.Sim;

/// <summary>A quantity of a resource, used to describe recipe inputs and outputs.</summary>
public readonly record struct ResourceAmount(ResourceType Type, float Amount);
