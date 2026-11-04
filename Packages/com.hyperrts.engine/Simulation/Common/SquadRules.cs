using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// On the match: how suppression, posture, retreat and reinforcing change a squad. Baked from
    /// <c>SquadSettings</c>.
    /// </summary>
    public struct SquadRules : IComponentData
    {
        /// <summary><see cref="Suppression.Value"/> from which a squad is suppressed.</summary>
        public float SuppressedAt;

        /// <summary><see cref="Suppression.Value"/> from which a squad is pinned.</summary>
        public float PinnedAt;

        /// <summary>Seconds without fire before suppression starts to fall.</summary>
        public float DecayDelay;

        /// <summary>Suppression lost a second while it falls.</summary>
        public float DecayRate;

        /// <summary>Speed and rate of fire multipliers of a suppressed squad.</summary>
        public float SuppressedSpeed;

        public float SuppressedFireRate;

        /// <summary>Rate of fire multiplier of a pinned squad; its speed comes from <see cref="ProneSpeed"/>.</summary>
        public float PinnedFireRate;

        /// <summary>Speed multiplier of a squad moving crouched.</summary>
        public float CrouchSpeed;

        /// <summary>Speed multiplier of a prone squad, which is every pinned one: 0 holds it still.</summary>
        public float ProneSpeed;

        public float RetreatSpeed;
        public float RetreatDamageTaken;

        /// <summary>Share of its member's slice of the squad's price that reinforcing a member costs.</summary>
        public float ReinforceCost;

        /// <summary>Seconds to bring back one member.</summary>
        public float ReinforceTime;

        public readonly SuppressionLevel Level(float suppression)
        {
            if (suppression >= PinnedAt)
            {
                return SuppressionLevel.Pinned;
            }

            return suppression >= SuppressedAt ? SuppressionLevel.Suppressed : SuppressionLevel.Steady;
        }

        /// <summary>The pace of a retreating squad: fast, tougher and silent.</summary>
        public readonly Pace RetreatPace => new() { Speed = RetreatSpeed, FireRate = 0f, DamageTaken = RetreatDamageTaken };

        /// <summary>The pace of a squad at <paramref name="level"/> moving in <paramref name="posture"/>.</summary>
        public readonly Pace PaceAt(SuppressionLevel level, Posture posture)
        {
            var pace = Pace.Normal;
            pace.Speed = PostureSpeed(posture);
            pace.FireRate = 1f;
            if (level == SuppressionLevel.Suppressed)
            {
                pace.Speed *= SuppressedSpeed;
                pace.FireRate = SuppressedFireRate;
            }
            else if (level == SuppressionLevel.Pinned)
            {
                pace.FireRate = PinnedFireRate;
            }

            return pace;
        }

        /// <summary>Speed multiplier of a squad moving in <paramref name="posture"/>.</summary>
        public readonly float PostureSpeed(Posture posture)
        {
            if (posture == Posture.Prone)
            {
                return ProneSpeed;
            }

            return posture == Posture.Crouch ? CrouchSpeed : 1f;
        }
    }
}
