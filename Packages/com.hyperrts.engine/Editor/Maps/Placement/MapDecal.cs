using HyperRTS.Editor.Maps.Climate;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Placement;

/// <summary>One ground decal laid at build time: its kind, map position, diameter, yaw and handedness.</summary>
/// <param name="Yaw">Degrees clockwise from north.</param>
/// <param name="Mirrored">Laid by a mirroring copy, so its texture is flipped to stay a true mirror image.</param>
public readonly record struct MapDecal(MapClimateDecal Kind, Vector2 Position, float Diameter, float Yaw, bool Mirrored);
