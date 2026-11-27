namespace HyperRTS.Editor.PlayMode.Launch;

/// <summary>A side a slot can play. Id 0 is the random pick.</summary>
public readonly struct LaunchSide
{
    public readonly byte Id;
    public readonly string Name;

    public LaunchSide(byte id, string name)
    {
        Id = id;
        Name = name;
    }
}
