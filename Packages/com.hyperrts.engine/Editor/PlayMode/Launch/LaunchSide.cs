namespace HyperRTS.Editor.PlayMode.Launch;

/// <summary>A side a slot can play. Id 0 is the random pick.</summary>
public readonly record struct LaunchSide(byte Id, string Name);
