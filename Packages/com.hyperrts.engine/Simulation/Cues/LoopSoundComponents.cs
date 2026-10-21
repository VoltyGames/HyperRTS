using Unity.Entities;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>What drives a continuous entity sound; the client fades each in and out from replicated state.</summary>
    public enum LoopSlot : byte
    {
        /// <summary>Engine at rest, fading out as the entity speeds up.</summary>
        EngineIdle = 0,

        /// <summary>Engine under way, fading in and pitching up with speed.</summary>
        EngineMove = 1,

        /// <summary>Spins up while an aircraft flies and down when it docks.</summary>
        Rotor = 2,

        /// <summary>While any weapon turns.</summary>
        TurretTraverse = 3,

        /// <summary>While under construction or producing.</summary>
        Working = 4,

        /// <summary>Always while shown, such as a generator hum.</summary>
        Ambient = 5,
    }

    /// <summary>One loop per slot; a slot without an entry is silent.</summary>
    [InternalBufferCapacity(0)]
    public struct EntityLoopSound : IBufferElementData
    {
        public LoopSlot Slot;
        public UnityObjectRef<SoundCue> Cue;
    }

    /// <summary>How an entity's loops follow its motion.</summary>
    public struct LoopSoundTuning : IComponentData
    {
        /// <summary>Speed in m/s at which the move loop is full and the idle loop silent.</summary>
        public float FullSpeed;

        /// <summary>Extra pitch at full speed, as a fraction.</summary>
        public float PitchRise;
    }
}
