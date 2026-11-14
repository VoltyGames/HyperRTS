namespace HyperRTS.Presentation.Overlays.Markers
{
    /// <summary>How a ground marker is drawn: its line width and fade in metres and its fill opacity at the line.</summary>
    public readonly struct MarkerLook
    {
        public readonly float Line;
        public readonly float Fade;
        public readonly float Fill;

        public MarkerLook(float line, float fade, float fill)
        {
            Line = line;
            Fade = fade;
            Fill = fill;
        }
    }
}
