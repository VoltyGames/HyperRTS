using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Simulation.Common.Infantry;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>Where a blast came from relative to a figure's facing.</summary>
    public enum BlastSide : byte
    {
        Front,
        Back,
        Left,
        Right,
    }

    /// <summary>
    /// The clip ranges a <see cref="FigureClipSet"/> resolves to in one bone clip table, and the table's ragdoll, shared
    /// by every figure that plays that set on that table. Its methods aren't <c>readonly</c>: a copy of a blob array points nowhere.
    /// </summary>
    public struct FigureClips
    {
        /// <summary>Entries per posture in <see cref="Legs"/>: idle, walk and run.</summary>
        public const int Motions = 3;

        /// <summary>Entries per posture in <see cref="Falls"/>: the shot fall, then one per <see cref="BlastSide"/>.</summary>
        public const int FallKinds = 5;

        /// <summary>By posture then idle, walk, run.</summary>
        public BlobArray<ClipRange> Legs;

        /// <summary>By posture; each plays once per shot.</summary>
        public BlobArray<ClipRange> Fire;

        /// <summary>By <see cref="MemberAction"/>; the entry of None has no frames.</summary>
        public BlobArray<ClipRange> Actions;

        /// <summary>By posture then the shot fall and the four blast falls.</summary>
        public BlobArray<ClipRange> Falls;

        /// <summary>The table's bones and ragdoll, which a dying figure becomes after its handover.</summary>
        public Ragdoll Ragdoll;

        /// <summary>What the legs play for a posture and a living state.</summary>
        public ClipRange Lower(Posture posture, FigureState moving)
        {
            var motion = moving switch
            {
                FigureState.Walk => 1,
                FigureState.Run => 2,
                _ => 0,
            };
            return Legs[(int)posture * Motions + motion];
        }

        /// <summary>What the upper body plays: the fire clip of the posture while firing, else what the legs play.</summary>
        public ClipRange Upper(Posture posture, FigureState moving, bool firing) =>
            firing ? Fire[(int)posture] : Lower(posture, moving);

        /// <summary>Seconds a figure of <paramref name="posture"/> keeps firing after a shot: its fire clip's length.</summary>
        public float FireHold(Posture posture)
        {
            var clip = Fire[(int)posture];
            return clip.FrameCount / clip.Fps;
        }

        /// <summary>The whole-body clip for what a member does where it stands.</summary>
        public ClipRange Action(MemberAction action) => Actions[(int)action];

        /// <summary>
        /// The fall of a member of <paramref name="posture"/> dropped by <paramref name="cause"/>; for a blast, by its
        /// world <paramref name="direction"/> to a figure at <paramref name="figureToWorld"/>.
        /// </summary>
        public ClipRange Fall(Posture posture, DeathCause cause, float3 direction, in float4x4 figureToWorld)
        {
            var kind = 0;
            if (cause == DeathCause.Blast)
            {
                kind = 1 + (int)Side(math.rotate(math.inverse(figureToWorld), direction));
            }

            return Falls[(int)posture * FallKinds + kind];
        }

        private static BlastSide Side(float3 local)
        {
            if (math.abs(local.z) >= math.abs(local.x))
            {
                return local.z >= 0f ? BlastSide.Front : BlastSide.Back;
            }

            return local.x >= 0f ? BlastSide.Right : BlastSide.Left;
        }
    }
}
