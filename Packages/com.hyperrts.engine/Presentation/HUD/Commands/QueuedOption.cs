
namespace HyperRTS.Presentation.HUD.Commands;

/// <summary>How many of one option the selected producers have queued, and how far the one in work is.</summary>
public readonly struct QueuedOption(int count, float progress)
{
    /// <summary>The <see cref="Progress"/> of an option no producer is working on.</summary>
    public const float Idle = -1f;

    public int Count { get; } = count;

    /// <summary>0 to 1 for the furthest one at the head of a queue, <see cref="Idle"/> when none is.</summary>
    public float Progress { get; } = progress;

    public bool InWork => Progress >= 0f;
}
