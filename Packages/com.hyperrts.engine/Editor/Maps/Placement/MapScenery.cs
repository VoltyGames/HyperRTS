using UnityEngine;

namespace HyperRTS.Editor.Maps.Placement;

/// <summary>A prop in the backdrop outside the play area, drawn on the terrain from its visual-only model.</summary>
/// <param name="Yaw">Degrees.</param>
/// <param name="Radius">Scaled footprint radius (metres), so scenery keeps apart.</param>
public readonly record struct MapScenery(GameObject Model, Vector2 Position, float Yaw, float Scale, float Radius);
