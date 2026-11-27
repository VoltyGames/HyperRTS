namespace HyperRTS.Presentation.Animation.Figures;

/// <summary>What a living figure is doing beside its movement, which picks its pose.</summary>
internal readonly struct FigureActivity(bool firing, bool digging)
{
    public readonly bool Firing = firing;
    public readonly bool Digging = digging;
}
