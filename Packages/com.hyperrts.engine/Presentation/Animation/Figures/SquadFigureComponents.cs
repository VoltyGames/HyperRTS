using HyperRTS.Presentation.Animation.VAT;
using Unity.Collections;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Animation.Figures
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

        /// <summary>The deployed weapon's model, a child of the unit shown while it is set up; null for none.</summary>
        public Entity DeployedModel;

        /// <summary>True while the deployed model is hidden; it starts shown, so the first update hides it.</summary>
        public bool ModelHidden;

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

        /// <summary>Seconds over which a figure's measured speed averages, since a squad moves in simulation steps.</summary>
        public const float SpeedTime = 0.25f;

        /// <summary>Seconds a figure keeps a motion before it may change to another, so a borderline speed doesn't flicker.</summary>
        public const float MoveHold = 0.3f;

        /// <summary>Share of <see cref="WalkSpeed"/> below which a walking figure stops.</summary>
        public const float StopShare = 0.5f;

        /// <summary>Share of <see cref="RunSpeed"/> below which a running figure slows to a walk.</summary>
        public const float SlowShare = 0.8f;

        /// <summary>
        /// How fast a figure moves, from its averaged horizontal <paramref name="speed"/>. A walk or run ends only below
        /// a lower speed than it began at, and a motion held under <see cref="MoveHold"/> seconds stays.
        /// </summary>
        public readonly FigureState Moving(float speed, FigureState current, float held)
        {
            var wanted = Wanted(speed, current);
            var living = current is FigureState.Idle or FigureState.Walk or FigureState.Run;
            return living && held < MoveHold ? current : wanted;
        }

        private readonly FigureState Wanted(float speed, FigureState current)
        {
            var runFrom = current == FigureState.Run ? RunSpeed * SlowShare : RunSpeed;
            if (speed >= runFrom)
            {
                return FigureState.Run;
            }

            var walkFrom = current is FigureState.Walk or FigureState.Run ? WalkSpeed * StopShare : WalkSpeed;
            return speed >= walkFrom ? FigureState.Walk : FigureState.Idle;
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

        /// <summary>Both halves of the body while digging at a field work; the idle clip when unset in authoring.</summary>
        public VATClipRange Dig;

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
}
