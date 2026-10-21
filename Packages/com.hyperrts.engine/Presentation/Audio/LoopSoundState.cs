using HyperRTS.Simulation.Cues;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Audio
{
    /// <summary>One entity's loop drivers on the client: smoothed speed, rotor spool and turret traverse.</summary>
    public struct LoopSoundState
    {
        /// <summary>Snapshots move turrets in steps slower than frames, so a turn holds this long between them.</summary>
        public const float TraverseHold = 0.2f;

        public const float TraverseFade = 0.1f;

        private const float SpeedSmoothing = 0.25f;

        public float3 LastPosition;
        public bool Seen;
        public float Speed;
        public float Spool;
        public float Traverse;
        public uint LastAim;
        public double LastTurn;

        /// <summary>
        /// Advances by one frame: <paramref name="flying"/> spools the rotor, a new <paramref name="aim"/> (any
        /// weapon's heading) restarts the traverse hold.
        /// </summary>
        public void Step(float3 position, bool flying, uint aim, float spoolTime, double now, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            var moved = Seen ? math.length((position - LastPosition).xz) / deltaTime : 0f;
            Speed = math.lerp(Speed, moved, 1f - math.exp(-deltaTime / SpeedSmoothing));
            Spool = math.saturate(Spool + (flying ? 1f : -1f) * deltaTime / spoolTime);
            if (Seen && aim != LastAim)
            {
                LastTurn = now;
            }

            var turning = Seen && now - LastTurn < TraverseHold;
            Traverse = math.saturate(Traverse + (turning ? 1f : -1f) * deltaTime / TraverseFade);
            LastPosition = position;
            LastAim = aim;
            Seen = true;
        }

        /// <summary>0 at rest to 1 at the tuning's full speed.</summary>
        public readonly float Throttle(in LoopSoundTuning tuning) => math.saturate(Speed / tuning.FullSpeed);

        /// <summary>How loud the slot plays, 0 to 1.</summary>
        public readonly float Level(LoopSlot slot, in LoopSoundTuning tuning, bool working) => slot switch
        {
            LoopSlot.EngineIdle => 1f - Throttle(tuning),
            LoopSlot.EngineMove => Throttle(tuning),
            LoopSlot.Rotor => Spool,
            LoopSlot.TurretTraverse => Traverse,
            LoopSlot.Working => working ? 1f : 0f,
            _ => 1f,
        };

        /// <summary>Pitch multiplier on the cue's own: engines rise with speed, a rotor with its spool.</summary>
        public readonly float PitchScale(LoopSlot slot, in LoopSoundTuning tuning) => slot switch
        {
            LoopSlot.EngineIdle or LoopSlot.EngineMove => 1f + tuning.PitchRise * Throttle(tuning),
            LoopSlot.Rotor => math.lerp(0.6f, 1f, Spool),
            _ => 1f,
        };
    }
}
