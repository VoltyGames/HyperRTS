using UnityEngine;

namespace HyperRTS.Editor.Maps.Views;

/// <summary>One standard screenshot of a map: where the camera looks, from how high, and its file name.</summary>
/// <param name="Focus">The ground point (map XZ) in the middle of the picture.</param>
public readonly record struct MapView(string Name, Vector2 Focus, float Height, float Yaw = 0f);
