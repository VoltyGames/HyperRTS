using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// A squad drawn as several bone-animated figures under one unit: figure i shows squad member i, standing at its
    /// offset. Its legs play idle, walk or run for its own motion and posture, its upper body fires over them, and it
    /// dies when the member does. Client only.
    /// </summary>
    public struct SquadFigures : IComponentData
    {
        /// <summary>Below this horizontal speed in m/s figures stand idle, so ghost jitter doesn't walk them.</summary>
        public const float WalkSpeed = 0.2f;

        /// <summary>Seconds a figure keeps firing after its weapon's last shot.</summary>
        public const float FireHold = 0.6f;

        /// <summary>Seconds after a blast that a dropped figure still dies from it.</summary>
        public const float BlastAfter = 0.5f;

        /// <summary>Seconds before a blast that a figure's fall still counts as its victim, as health and cues land a frame apart.</summary>
        public const float BlastBefore = 0.2f;

        /// <summary>Metres past a blast's radius that still reach the squad's figures.</summary>
        public const float BlastReach = 2f;

        /// <summary>Horizontal speed in m/s from which figures run instead of walk.</summary>
        public float RunSpeed;

        /// <summary>Seconds a dropped figure shows its die clip before it hides.</summary>
        public float DieHold;

        /// <summary>True once a blast has hit near the squad; <see cref="BlastPosition"/> and <see cref="BlastTime"/> are the latest.</summary>
        public bool Blasted;

        public float3 BlastPosition;
        public float BlastTime;

        /// <summary>Keeps the latest impact in this frame's <paramref name="cues"/> whose splash reaches the squad at <paramref name="position"/>.</summary>
        public void Hear(DynamicBuffer<CueEvent> cues, float3 position, float now)
        {
            foreach (var cue in cues)
            {
                if (cue.IsBlast && math.distance(cue.Position, position) <= cue.Radius + BlastReach)
                {
                    Blasted = true;
                    BlastPosition = cue.Position;
                    BlastTime = now;
                }
            }
        }

        /// <summary>True when a figure that began to fall at <paramref name="fallTime"/> died of the latest blast.</summary>
        public readonly bool Killed(float fallTime) =>
            Blasted && fallTime >= BlastTime - BlastBefore && fallTime <= BlastTime + BlastAfter;

        /// <summary>How fast a figure moves, from its horizontal <paramref name="speed"/>.</summary>
        public readonly FigureState Moving(float speed)
        {
            if (speed >= RunSpeed)
            {
                return FigureState.Run;
            }

            return speed >= WalkSpeed ? FigureState.Walk : FigureState.Idle;
        }
    }

    /// <summary>
    /// One figure's clips from its own bone table: standing, crouched and prone movement and fire, and the fall.
    /// Crouch and prone clips are optional in authoring, which then fills them with the standing ones.
    /// </summary>
    public struct FigureClips
    {
        public VATClipRange Idle;
        public VATClipRange Walk;
        public VATClipRange Run;
        public VATClipRange Fire;
        public VATClipRange CrouchIdle;
        public VATClipRange CrouchWalk;
        public VATClipRange CrouchFire;
        public VATClipRange ProneIdle;
        public VATClipRange ProneCrawl;
        public VATClipRange ProneFire;
        public VATClipRange Die;

        /// <summary>Optional falls named by where the blast came from relative to the figure's facing; frame count 0 is unset.</summary>
        public VATClipRange DieBlastFront;
        public VATClipRange DieBlastBack;
        public VATClipRange DieBlastLeft;
        public VATClipRange DieBlastRight;

        /// <summary>What the legs play for a posture and a living state; running and walking share the low postures' clip.</summary>
        public readonly VATClipRange Lower(Posture posture, FigureState moving)
        {
            var still = moving == FigureState.Idle;
            return posture switch
            {
                Posture.Crouch => still ? CrouchIdle : CrouchWalk,
                Posture.Prone => still ? ProneIdle : ProneCrawl,
                _ => moving switch
                {
                    FigureState.Walk => Walk,
                    FigureState.Run => Run,
                    _ => Idle,
                },
            };
        }

        /// <summary>What the upper body plays: the fire clip of the posture while firing, else what the legs play.</summary>
        public readonly VATClipRange Upper(Posture posture, FigureState moving, bool firing)
        {
            if (!firing)
            {
                return Lower(posture, moving);
            }

            return posture switch
            {
                Posture.Crouch => CrouchFire,
                Posture.Prone => ProneFire,
                _ => Fire,
            };
        }

        /// <summary>The fall for a blast at <paramref name="blast"/> seen from a figure at <paramref name="figureToWorld"/>; Die when that side has none.</summary>
        public readonly VATClipRange Blasted(float3 blast, in float4x4 figureToWorld)
        {
            var local = math.transform(math.inverse(figureToWorld), blast);
            var clip = BlastSide(local);
            return clip.FrameCount > 0 ? clip : Die;
        }

        private readonly VATClipRange BlastSide(float3 local)
        {
            if (math.abs(local.z) >= math.abs(local.x))
            {
                return local.z >= 0f ? DieBlastFront : DieBlastBack;
            }

            return local.x >= 0f ? DieBlastRight : DieBlastLeft;
        }
    }

    /// <summary>What a squad figure shows. Unset is a baked figure the system has not placed yet; the living states are its motion.</summary>
    public enum FigureState : byte
    {
        Unset,
        Idle,
        Walk,
        Run,
        Die,
        Hidden,
    }

    /// <summary>One figure of a <see cref="SquadFigures"/> squad, showing the member at the same index.</summary>
    [InternalBufferCapacity(4)]
    public struct SquadFigure : IBufferElementData
    {
        /// <summary>Per second rate at which a figure closes on its member's offset.</summary>
        private const float FollowRate = 12f;

        public Entity Figure;

        /// <summary>Figures baked separately keep their own tables, so each carries its own clip ranges.</summary>
        public FigureClips Clips;

        /// <summary>0 to 1: how far into each looping clip this figure starts, so loops don't play in step.</summary>
        public float Phase;

        public FigureState State;

        public Posture Posture;

        /// <summary>True while the upper body plays its fire clip.</summary>
        public bool Firing;

        /// <summary>The clip the legs play now, and when it started on the clock, which the upper body follows when it plays the same.</summary>
        public VATClipRange Lower;

        public float LowerStart;

        /// <summary>The clip the upper body plays now.</summary>
        public VATClipRange Upper;

        /// <summary>Clock time a dying figure hides at.</summary>
        public float HideAt;

        /// <summary>Where the figure stands in metres from the squad on world axes, eased toward its member's offset.</summary>
        public float2 Offset;

        /// <summary>Baked height of the figure above its parent.</summary>
        public float Height;

        public float3 LastPosition;
        public byte LastShots;
        public float LastShotTime;

        /// <summary>False until the first update records position and shots, so a spawn neither runs nor fires.</summary>
        public bool Primed;

        /// <summary>Eases <see cref="Offset"/> toward <paramref name="target"/>, or jumps there on the first update.</summary>
        public void Follow(float2 target, float deltaTime)
        {
            Offset = Primed ? math.lerp(Offset, target, 1f - math.exp(-FollowRate * deltaTime)) : target;
        }

        /// <summary>Records this frame's world position and weapon shot count; returns how a living figure moves and whether it fires.</summary>
        public FigureState Observe(in SquadFigures squad, float3 position, byte shots, float now, float deltaTime, out bool firing)
        {
            if (!Primed)
            {
                Primed = true;
                LastPosition = position;
                LastShots = shots;
                LastShotTime = now - SquadFigures.FireHold;
            }

            var speed = deltaTime > 0f ? math.distance(position.xz, LastPosition.xz) / deltaTime : 0f;
            if (shots != LastShots)
            {
                LastShotTime = now;
            }

            LastPosition = position;
            LastShots = shots;
            firing = now - LastShotTime < SquadFigures.FireHold;
            return squad.Moving(speed);
        }

        /// <summary>The state this figure moves to: a dead member's figure dies then hides, a living one's shows <paramref name="living"/>.</summary>
        public readonly FigureState Next(bool alive, FigureState living, float now)
        {
            if (alive)
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
