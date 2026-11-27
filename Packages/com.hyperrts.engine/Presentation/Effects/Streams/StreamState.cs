using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Presentation.Quality;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Effects.Streams;

/// <summary>One stream's path, picked rig and graph copies between frames.</summary>
public sealed class StreamState
{
    /// <summary>Last frame's point; this frame's on the first frame, so the first segment is empty.</summary>
    public Vector3 From;

    public Vector3 To;
    public int Frame;

    /// <summary>The variant this stream plays, picked by <see cref="Key"/>.</summary>
    public EffectRig Rig;

    public uint Key;

    /// <summary>The rig <see cref="Rig"/> was picked from.</summary>
    public EffectRig Root;

    /// <summary>This stream's copies of the rig's stream graphs; null until it first emits, or without any.</summary>
    public GraphStream Graphs;

    /// <summary>Plays the root's variant for <see cref="Key"/>, picked again only when the root changes.</summary>
    public void Use(EffectRig root)
    {
        if (Root != root)
        {
            Stop();
            Root = root;
            Rig = root.Pick(Key);
        }
    }

    /// <summary>Streams the rig's graphs along this frame's path at the effect detail's density.</summary>
    public void Emit(Quaternion rotation, float deltaTime, float strength, float poseScale = 1f)
    {
        Graphs ??= Rig.StartStream();
        Graphs?.Move(From, To, rotation, deltaTime, strength * DetailBudget.Density, poseScale);
    }

    /// <summary>Streams a loop's graphs, turned and scaled as the rig's loop poses for <see cref="Key"/>.</summary>
    public void EmitLoop(Quaternion rotation, float deltaTime, float strength) =>
        Emit(LoopRotation(rotation), deltaTime, strength, LoopScale());

    /// <summary>The rig's scale range, picked by <see cref="Key"/>.</summary>
    public float LoopScale() => Mathf.Lerp(Rig.scale.x, Rig.scale.y, Fraction(1u));

    /// <summary>The rotation, turned by a yaw picked by <see cref="Key"/> when the rig spins at random.</summary>
    public Quaternion LoopRotation(Quaternion rotation) =>
        Rig.randomSpin ? rotation * EffectRig.Yaw(Fraction(2u)) : rotation;

    private float Fraction(uint salt) => Hashes.Fraction(math.hash(new uint2(Key, salt)));

    /// <summary>Stops this stream's graphs, which fade out as their particles die.</summary>
    public void Stop()
    {
        if (Graphs != null)
        {
            Rig.StopStream(Graphs);
            Graphs = null;
        }
    }
}
