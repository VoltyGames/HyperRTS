using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// A squad drawn as several VAT figures under one unit: one figure per share of health, each playing idle, walk,
    /// run, fire or die from the unit's motion and primary weapon. Client only.
    /// </summary>
    public struct SquadFigures : IComponentData
    {
        /// <summary>Below this horizontal speed in m/s figures stand idle, so ghost jitter doesn't walk them.</summary>
        public const float WalkSpeed = 0.2f;

        /// <summary>Seconds figures keep firing after the primary weapon's last shot.</summary>
        public const float FireHold = 0.6f;

        /// <summary>Horizontal speed in m/s from which figures run instead of walk.</summary>
        public float RunSpeed;

        /// <summary>Seconds a dropped figure shows its die clip before it hides.</summary>
        public float DieHold;

        public float3 LastPosition;
        public byte LastShots;
        public float LastShotTime;

        /// <summary>False until the first update records position and shots, so a spawn neither runs nor fires.</summary>
        public bool Primed;

        /// <summary>Figures shown: one per share of health, at least one while alive.</summary>
        public static int VisibleCount(in Health health, int figures)
        {
            if (health.Current <= 0f)
            {
                return 0;
            }

            return math.clamp((int)math.ceil(figures * health.Fraction), 1, figures);
        }

        /// <summary>Records this frame's position and primary weapon shot count; returns what living figures show.</summary>
        public FigureState Observe(float3 position, byte shots, float now, float deltaTime)
        {
            if (!Primed)
            {
                Primed = true;
                LastPosition = position;
                LastShots = shots;
                LastShotTime = now - FireHold;
            }

            var speed = deltaTime > 0f ? math.distance(position.xz, LastPosition.xz) / deltaTime : 0f;
            if (shots != LastShots)
            {
                LastShotTime = now;
            }

            LastPosition = position;
            LastShots = shots;
            return Living(speed, now - LastShotTime < FireHold);
        }

        /// <summary>The motion state for a horizontal <paramref name="speed"/> and whether the squad fired lately.</summary>
        public readonly FigureState Living(float speed, bool firing)
        {
            if (firing)
            {
                return FigureState.Fire;
            }

            if (speed >= RunSpeed)
            {
                return FigureState.Run;
            }

            return speed >= WalkSpeed ? FigureState.Walk : FigureState.Idle;
        }
    }

    /// <summary>One figure's idle, walk, run, fire and die clips, from its own VAT table.</summary>
    public struct FigureClips
    {
        public VATClipRange Idle;
        public VATClipRange Walk;
        public VATClipRange Run;
        public VATClipRange Fire;
        public VATClipRange Die;

        public readonly VATClipRange Clip(FigureState state) => state switch
        {
            FigureState.Walk => Walk,
            FigureState.Run => Run,
            FigureState.Fire => Fire,
            FigureState.Die => Die,
            _ => Idle,
        };
    }

    /// <summary>What a squad figure shows. Unset is a baked figure the system has not placed yet.</summary>
    public enum FigureState : byte
    {
        Unset,
        Idle,
        Walk,
        Run,
        Fire,
        Die,
        Hidden,
    }

    /// <summary>One figure of a <see cref="SquadFigures"/> squad; the last in the buffer drops first.</summary>
    [InternalBufferCapacity(4)]
    public struct SquadFigure : IBufferElementData
    {
        public Entity Figure;

        /// <summary>Figures baked separately keep their own tables, so each carries its own clip ranges.</summary>
        public FigureClips Clips;

        /// <summary>The figure's own uniform scale, restored when it shows again.</summary>
        public float Scale;

        /// <summary>0 to 1: how far into each looping clip this figure starts, so loops don't play in step.</summary>
        public float Phase;

        public FigureState State;

        /// <summary>Clock time a dying figure hides at.</summary>
        public float HideAt;

        /// <summary>The state this figure moves to: dropped figures die then hide, and kept ones show the squad's.</summary>
        public readonly FigureState Next(bool kept, FigureState living, float now)
        {
            if (kept)
            {
                return living;
            }

            // A figure already gone when first placed, as on a damaged spawn, skips its fall.
            if (State is FigureState.Unset or FigureState.Hidden)
            {
                return FigureState.Hidden;
            }

            if (State != FigureState.Die)
            {
                return FigureState.Die;
            }

            return now >= HideAt ? FigureState.Hidden : FigureState.Die;
        }

        /// <summary>When <paramref name="clip"/> started for this figure if it began at <paramref name="now"/>.</summary>
        public readonly float StartTime(in VATClipRange clip, float now)
        {
            var length = clip.FrameCount / clip.Fps;
            return clip.Loop ? now - Phase * length : now;
        }
    }
}
