namespace HyperRTS.Editor.PlayMode.Launch;

/// <summary>A map the launcher can start. <see cref="Slots"/> is how many players it seats.</summary>
public readonly record struct LaunchMap(string Id, string Name, int Slots);
