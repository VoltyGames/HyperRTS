using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Continuous sounds a unit or building plays while shown: engine, rotor, turret, work and hum.</summary>
    [AddComponentMenu(HyperRTSMenu.Audio + "Entity Loops")]
    [Icon(HyperRTSIcons.Audio)]
    [HelpURL(HyperRTSDocs.Audio)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class EntityLoopsAuthoring : AuthoringBehaviour
    {
        [Header("Engine")]
        [Tooltip("At rest; fades out as it speeds up.")]
        public SoundCue engineIdle;

        [Tooltip("Under way; fades in and pitches up with speed.")]
        public SoundCue engineMove;

        [Tooltip("Speed in m/s at which the move loop is full and the idle loop silent.")]
        [Min(0.1f)]
        public float fullSpeed = 8f;

        [Tooltip("Extra pitch at full speed, as a fraction (0.15 is 15% higher).")]
        [Range(0f, 1f)]
        public float pitchRise = 0.15f;

        [Header("Rotor")]
        [Tooltip("While the aircraft flies; spools down when it docks.")]
        public SoundCue rotor;

        [Tooltip("Seconds the rotor takes to spool from still to full, as on the motion rig.")]
        [Min(0.1f)]
        public float rotorSpool = 4f;

        [Header("Other")]
        [Tooltip("While any weapon turns.")]
        public SoundCue turretTraverse;

        [Tooltip("While under construction or producing.")]
        public SoundCue working;

        [Tooltip("Always while shown, such as a generator hum.")]
        public SoundCue ambient;

        public static DynamicBuffer<EntityLoopSound> Add<TWriter>(ref TWriter writer, float fullSpeed, float pitchRise,
            float spoolTime)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new LoopSoundTuning { FullSpeed = fullSpeed, PitchRise = pitchRise, SpoolTime = spoolTime });
            return writer.AddBuffer<EntityLoopSound>();
        }

        /// <summary>Unset cues are skipped, so the slot stays silent.</summary>
        public static void Set(DynamicBuffer<EntityLoopSound> loops, LoopSlot slot, SoundCue cue)
        {
            if (cue != null)
            {
                loops.Add(new EntityLoopSound { Slot = slot, Cue = cue });
            }
        }

        public class Baker : Baker<EntityLoopsAuthoring>
        {
            public override void Bake(EntityLoopsAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                var loops = EntityLoopsAuthoring.Add(ref writer, authoring.fullSpeed, authoring.pitchRise,
                    authoring.rotorSpool);
                EntityLoopsAuthoring.Set(loops, LoopSlot.EngineIdle, authoring.engineIdle);
                EntityLoopsAuthoring.Set(loops, LoopSlot.EngineMove, authoring.engineMove);
                EntityLoopsAuthoring.Set(loops, LoopSlot.Rotor, authoring.rotor);
                EntityLoopsAuthoring.Set(loops, LoopSlot.TurretTraverse, authoring.turretTraverse);
                EntityLoopsAuthoring.Set(loops, LoopSlot.Working, authoring.working);
                EntityLoopsAuthoring.Set(loops, LoopSlot.Ambient, authoring.ambient);
            }
        }
    }
}
