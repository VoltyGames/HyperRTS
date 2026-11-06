using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Common.Infantry
{
    /// <summary>
    /// On the match: how suppression, posture, retreat and reinforcing change a squad, and where its members stand.
    /// Baked from <c>SquadSettings</c>.
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

        /// <summary>Metres of ring a halted squad gives each living member, so a bigger squad spreads wider.</summary>
        public float MemberSpacing;

        /// <summary>Metres between a moving squad's wedge rows.</summary>
        public float WedgeDepth;

        /// <summary>Metres a wedge's members stand to the side for each row.</summary>
        public float WedgeWidth;

        /// <summary>Furthest a member stands from a squad that moves or halts, in metres; it also bounds the cover search.</summary>
        public float MaxSpread;

        /// <summary>Furthest a digging member stands from its squad, which halts short of a site by its reach.</summary>
        public float WorkSpread;

        /// <summary>Share of the squad's speed a member may run at to catch up with its slot.</summary>
        public float CatchUp;

        /// <summary>Metres from its place on the site within which a member digs.</summary>
        public float DigRange;

        /// <summary>Directions a moving squad's wedge points in.</summary>
        public const int WedgeHeadings = 16;

        /// <summary>Radius of the ring of <paramref name="count"/> living members, at most <see cref="MaxSpread"/>.</summary>
        public readonly float RingRadius(int count) => math.min(MemberSpacing * count / (math.PI * 2f), MaxSpread);

        /// <summary>The offset of living member <paramref name="rank"/> of <paramref name="count"/> in a ring round a halted squad.</summary>
        public readonly float2 SpreadSlot(int rank, int count)
        {
            if (count <= 1)
            {
                return float2.zero;
            }

            var angle = math.PI * 0.25f + rank * math.PI * 2f / count;
            return new float2(math.cos(angle), math.sin(angle)) * RingRadius(count);
        }

        /// <summary>
        /// The offset in a loose wedge along <paramref name="heading"/>, snapped to <see cref="WedgeHeadings"/> so a
        /// curving path doesn't resend offsets every frame.
        /// </summary>
        public readonly float2 WedgeSlot(int rank, int count, float2 heading)
        {
            var step = math.PI * 2f / WedgeHeadings;
            var angle = math.round(math.atan2(heading.y, heading.x) / step) * step;
            heading = new float2(math.cos(angle), math.sin(angle));
            var side = new float2(heading.y, -heading.x);
            var row = (rank + 1) / 2;
            var sign = (rank & 1) == 1 ? -1f : 1f;
            var middle = count / 2 * WedgeDepth * 0.5f;
            return heading * (middle - row * WedgeDepth) + side * (sign * row * WedgeWidth);
        }

        /// <summary>The part of the squad's price that bringing back one of <paramref name="members"/> costs.</summary>
        public readonly float ReinforceShare(int members) => ReinforceCost / members;

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
