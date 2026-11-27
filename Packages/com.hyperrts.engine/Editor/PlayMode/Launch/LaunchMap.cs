namespace HyperRTS.Editor.PlayMode.Launch;

/// <summary>A map the launcher can start. <see cref="Slots"/> is how many players it seats.</summary>
public readonly struct LaunchMap
{
    public readonly string Id;
    public readonly string Name;
    public readonly int Slots;

    public LaunchMap(string id, string name, int slots)
    {
        Id = id;
        Name = name;
        Slots = slots;
    }
}
