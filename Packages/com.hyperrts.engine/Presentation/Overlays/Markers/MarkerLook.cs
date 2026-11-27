namespace HyperRTS.Presentation.Overlays.Markers;

/// <summary>How a ground marker is drawn: its line width and fade in metres and its fill opacity at the line.</summary>
public readonly struct MarkerLook(float line, float fade, float fill)
{
    public readonly float Line = line;
    public readonly float Fade = fade;
    public readonly float Fill = fill;
}
