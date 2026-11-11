namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>What a living figure is doing beside its movement, which picks its pose.</summary>
    internal readonly struct FigureActivity
    {
        public readonly bool Firing;
        public readonly bool Digging;

        public FigureActivity(bool firing, bool digging)
        {
            Firing = firing;
            Digging = digging;
        }
    }
}
