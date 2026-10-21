using HyperRTS.Simulation.Cues;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Audio
{
    /// <summary>A loop that wants to play this frame, offered to the <see cref="LoopMixer"/>.</summary>
    public struct LoopCandidate
    {
        public Entity Entity;
        public LoopSlot Slot;
        public SoundCue Cue;
        public float Level;
        public float PitchScale;
        public Vector3 Position;

        /// <summary>Squared distance to the listener.</summary>
        public float DistanceSq;

        public readonly (Entity, LoopSlot) Key => (Entity, Slot);
    }
}
