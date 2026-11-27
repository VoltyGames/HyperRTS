using HyperRTS.Editor.Maps.Report;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build;

/// <summary>A preview's report and its land image; the image is null when the map has errors. The caller owns it.</summary>
public sealed class MapPreviewResult(MapReport report, Texture2D image)
{
    public MapReport Report { get; } = report;

    public Texture2D Image { get; } = image;
}
