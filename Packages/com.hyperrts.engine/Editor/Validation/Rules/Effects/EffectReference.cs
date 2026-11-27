using HyperRTS.Simulation.Cues.Effects;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules.Effects;

/// <summary>
/// One effect field of an authoring component: a rig or a surface table, whether it must be set, and the socket
/// it plays from, if any.
/// </summary>
public struct EffectReference
{
    public string Label;
    public GameObject Rig;
    public SurfaceEffects Surface;
    public bool IsSurface;
    public bool Required;
    public string SocketName;
    public Transform Socket;

    public bool IsSet => IsSurface ? Surface != null : Rig != null;

    /// <summary>Set, or required: either way it plays, so it needs its socket.</summary>
    public bool InUse => Required || IsSet;

    public static EffectReference ForRig(string label, GameObject rig, bool required) =>
        new() { Label = label, Rig = rig, Required = required };

    /// <summary>The table's own rigs are checked once by <see cref="SurfaceEffectsRule"/>; here it only has to be set.</summary>
    public static EffectReference ForSurface(string label, SurfaceEffects surface, bool required) =>
        new() { Label = label, Surface = surface, IsSurface = true, Required = required };

    /// <summary>The reference needs <paramref name="socket"/> once it is in use.</summary>
    public EffectReference WithSocket(string socketName, Transform socket)
    {
        var reference = this;
        reference.SocketName = socketName;
        reference.Socket = socket;
        return reference;
    }
}
