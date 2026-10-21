using HyperRTS.Simulation.Combat;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// A felled tree's shown fall: a rigid rod tipping about its stump under gravity, then a damped bounce off its
    /// canopy. Disabled once it rests in its final pose.
    /// </summary>
    public struct FallClock : IComponentData, IEnableableComponent
    {
        /// <summary>Lean in radians a snapped trunk already has when it starts to go.</summary>
        public const float StartLean = 0.1f;

        /// <summary>Angular speed every fall starts with, plus tip speed in m/s spread over the height.</summary>
        public const float BasePush = 0.1f;
        public const float TipPush = 1.2f;

        /// <summary>Share of a crushing vehicle's speed the tip picks up.</summary>
        public const float CrushShare = 0.25f;

        /// <summary>The canopy as a spring under the trunk once it is down: per-second² stiffness and damping.</summary>
        private const float CanopyStiffness = 900f;
        private const float CanopyDamping = 22f;

        // Fixed steps so every client shows the same fall whatever its frame rate.
        private const float Step = 1f / 240f;
        private const float RestAngle = 0.01f;
        private const float RestRate = 0.05f;
        private const float MaxTime = 8f;

        private const float Ground = math.PI * 0.5f;

        /// <summary>Radians from upright toward the ground.</summary>
        public float Angle;
        public float Rate;
        public float Time;
        public float Lag;
        public bool Started;
        public bool Landed;

        /// <summary>0 upright, 1 lying in the final pose, a little over while the canopy gives.</summary>
        public readonly float Progress => Angle / Ground;

        public readonly bool Resting
        {
            get
            {
                if (Time >= MaxTime)
                {
                    return true;
                }

                var still = math.abs(Rate) < RestRate && math.abs(Angle - Ground) < RestAngle;
                return Landed && still;
            }
        }

        /// <summary>A fall of a <paramref name="height"/> metre tree pushed at <paramref name="crushSpeed"/> m/s.</summary>
        public static FallClock Pushed(float height, float crushSpeed) => new()
        {
            Angle = StartLean,
            Rate = BasePush + (TipPush + crushSpeed * CrushShare) / height,
            Started = true,
        };

        /// <summary>Runs the fall on by <paramref name="deltaTime"/>; returns true on the frame the crown first hits.</summary>
        public bool Advance(float height, float deltaTime)
        {
            var wasLanded = Landed;
            var tipping = 1.5f * FallingRemains.Gravity / height;
            Lag += deltaTime;
            while (Lag >= Step)
            {
                Lag -= Step;
                Time += Step;
                var below = Angle - Ground;
                var canopy = below > 0f ? -CanopyStiffness * below - CanopyDamping * Rate : 0f;
                Rate += (tipping * math.sin(Angle) + canopy) * Step;
                Angle += Rate * Step;
                Landed |= Angle >= Ground;
            }

            return Landed && !wasLanded;
        }
    }
}
