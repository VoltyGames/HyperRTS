using UnityEngine;

namespace HyperRTS.Presentation.Effects.Rig;

/// <summary>A shake a burst gives the camera: where it went off, how hard, how far it is felt and for how long.</summary>
public readonly struct CameraJolt(Vector3 position, float strength, float reach, float seconds)
{
    public readonly Vector3 Position = position;

    /// <summary>Metres of camera offset at the burst point at the closest zoom.</summary>
    public readonly float Strength = strength;

    /// <summary>Distance from the camera's focus at which it is no longer felt; 0 uses the camera's reach.</summary>
    public readonly float Reach = reach;

    /// <summary>Seconds it takes to die out; 0 uses the camera's duration.</summary>
    public readonly float Seconds = seconds;
}
