using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Common
{
    /// <summary>How a squad member holds its body; clients pick the pose from it.</summary>
    public enum Posture : byte
    {
        Stand,
        Crouch,
        Prone,
    }

    /// <summary>
    /// One soldier of a squad, replicated on the squad's ghost like a <see cref="Combat.Weapons.WeaponPose"/>. The squad
    /// stays one entity and its <see cref="Health"/> is the sum of its living members. A member at zero health is
    /// dead and stays in the buffer, so indices never shift.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct SquadMember : IBufferElementData
    {
        /// <summary>Furthest a member stands from its squad, in metres.</summary>
        public const float MaxSpread = 3.5f;

        /// <summary>Splash treats a member as a disc of this radius.</summary>
        public const float Radius = 0.3f;

        private const float SpreadRadius = 2.2f;
        private const float WedgeDepth = 1.2f;
        private const float WedgeWidth = 1f;

        /// <summary>Directions a moving squad's wedge points in.</summary>
        public const int WedgeHeadings = 16;

        [GhostField(Quantization = 10)] public float Health;
        [GhostField(Quantization = 10)] public float MaxHealth;

        /// <summary>
        /// Metres on the XZ plane from the squad's position, on world axes so a turning squad doesn't swing it. Sent in
        /// 10 cm steps, which figures smooth.
        /// </summary>
        [GhostField(Quantization = 10)] public float2 Offset;

        [GhostField] public Posture Posture;

        /// <summary>Index of the squad's <see cref="Combat.Weapons.WeaponMount"/> this member fires.</summary>
        [GhostField] public byte Weapon;

        /// <summary>Server only: the offset it walks toward.</summary>
        [GhostField(SendData = false)] public float2 Slot;

        /// <summary>
        /// Protection it has now: from its spot once it stands there, or light from every side on a concealing cell
        /// (woods, tall crops) with no spot. None while it walks.
        /// </summary>
        [GhostField] public CoverKind Cover;

        /// <summary>World direction it faces in cover, in 256 steps round from the X axis toward Z.</summary>
        [GhostField] public byte CoverHeading;

        /// <summary>Server only: the prop of the spot it holds, or null; see <c>CoverSpot</c>.</summary>
        [GhostField(SendData = false)] public Entity SpotEntity;

        /// <summary>Server only: its index in the prop's <c>CoverSpot</c> buffer.</summary>
        [GhostField(SendData = false)] public byte SpotIndex;

        /// <summary>A squad moving faster than this, in metres a second, walks in a wedge and holds no spot.</summary>
        public const float MovingSpeed = 0.5f;

        public readonly bool Alive => Health > 0f;

        public readonly bool HasSpot => SpotEntity != Entity.Null;

        /// <summary>Unit direction on the XZ plane the member faces in cover.</summary>
        public readonly float2 CoverFacing
        {
            get
            {
                var angle = CoverHeading * (math.PI * 2f / 256f);
                return new float2(math.cos(angle), math.sin(angle));
            }
        }

        public static byte HeadingOf(float2 direction) =>
            (byte)((int)math.round(math.atan2(direction.y, direction.x) * (256f / (math.PI * 2f))) & 255);

        /// <summary>Stops holding a spot, so it walks to its slot again.</summary>
        public void LeaveSpot()
        {
            SpotEntity = Entity.Null;
            Cover = CoverKind.None;
        }

        /// <summary>
        /// Share of a hit it takes: the cover's share, or 1 when none or when the hit comes from outside the front arc
        /// of its spot (light cover on a concealing cell has no arc). <paramref name="toThreat"/> is the XZ direction
        /// to where the hit came from; <paramref name="piercing"/> is the share of the protection the hit ignores.
        /// </summary>
        public readonly float DamageShare(in CoverRules rules, float2 toThreat, float piercing) =>
            Covers(rules, toThreat) ? math.lerp(rules.DamageTaken(Cover), 1f, math.saturate(piercing)) : 1f;

        /// <summary>Share of a hit's suppression it takes, by the same rules as <see cref="DamageShare"/>.</summary>
        public readonly float SuppressionShare(in CoverRules rules, float2 toThreat, float piercing) =>
            Covers(rules, toThreat) ? math.lerp(rules.SuppressionTaken(Cover), 1f, math.saturate(piercing)) : 1f;

        private readonly bool Covers(in CoverRules rules, float2 toThreat) =>
            Cover != CoverKind.None
            && !(HasSpot && math.dot(math.normalizesafe(toThreat), CoverFacing) < rules.ArcCos);

        /// <summary>World position of a member standing at <see cref="Offset"/> from a squad at <paramref name="squad"/>.</summary>
        public readonly float3 Position(float3 squad) => squad + new float3(Offset.x, 0f, Offset.y);

        /// <summary>Health of the living members, which the squad's <see cref="Health.Current"/> equals.</summary>
        public static float Total(DynamicBuffer<SquadMember> members)
        {
            var total = 0f;
            foreach (var member in members)
            {
                total += math.max(0f, member.Health);
            }

            return total;
        }

        public static float TotalMax(DynamicBuffer<SquadMember> members)
        {
            var total = 0f;
            foreach (var member in members)
            {
                total += member.MaxHealth;
            }

            return total;
        }

        public static int CountLiving(DynamicBuffer<SquadMember> members)
        {
            var count = 0;
            foreach (var member in members)
            {
                count += member.Alive ? 1 : 0;
            }

            return count;
        }

        /// <summary>
        /// One living member chosen by <paramref name="seed"/>, or -1 when all are dead. Members weigh by the share of a
        /// hit from <paramref name="from"/> they take, so exposed ones are hit more often than ones in cover.
        /// </summary>
        public static int PickExposed(DynamicBuffer<SquadMember> members, float3 squad, uint seed,
            in CoverRules rules, float3 from, float piercing)
        {
            var total = 0f;
            for (var i = 0; i < members.Length; i++)
            {
                total += members[i].Alive ? members[i].ExposureTo(rules, squad, from, piercing) : 0f;
            }

            var roll = Random.CreateFromIndex(seed).NextFloat() * total;
            var last = -1;
            for (var i = 0; i < members.Length; i++)
            {
                if (!members[i].Alive)
                {
                    continue;
                }

                last = i;
                roll -= members[i].ExposureTo(rules, squad, from, piercing);
                if (roll < 0f)
                {
                    return i;
                }
            }

            return last;
        }

        /// <summary>The share of a hit from <paramref name="from"/> this member takes, standing in a squad at <paramref name="squad"/>.</summary>
        public readonly float ExposureTo(in CoverRules rules, float3 squad, float3 from, float piercing) =>
            DamageShare(rules, from.xz - Position(squad).xz, piercing);

        /// <summary>The share of a hit's suppression from <paramref name="from"/> this member takes.</summary>
        public readonly float SuppressionFrom(in CoverRules rules, float3 squad, float3 from, float piercing) =>
            SuppressionShare(rules, from.xz - Position(squad).xz, piercing);

        /// <summary>The living member standing nearest <paramref name="point"/>, or -1 when all are dead.</summary>
        public static int FindNearest(DynamicBuffer<SquadMember> members, float3 squad, float3 point)
        {
            var nearest = -1;
            var best = float.MaxValue;
            for (var i = 0; i < members.Length; i++)
            {
                var distance = math.distancesq(members[i].Position(squad).xz, point.xz);
                if (members[i].Alive && distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            return nearest;
        }

        /// <summary>Takes <paramref name="amount"/> off one member only, none carrying over; returns the squad's new total.</summary>
        public static float Hurt(DynamicBuffer<SquadMember> members, int index, float amount)
        {
            var member = members[index];
            member.Health = math.max(0f, member.Health - amount);
            members[index] = member;
            return Total(members);
        }

        /// <summary>
        /// Shares <paramref name="total"/> out from the first member on, so the last ones are the dead: a replay
        /// records only the squad's health.
        /// </summary>
        public static void Fill(DynamicBuffer<SquadMember> members, float total)
        {
            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                member.Health = math.clamp(total, 0f, member.MaxHealth);
                members[i] = member;
                total -= member.Health;
            }
        }

        /// <summary>Restores the most hurt living member first, then the next, until <paramref name="amount"/> is spent.</summary>
        public static float Heal(DynamicBuffer<SquadMember> members, float amount)
        {
            while (amount > 0f && TryFindMostHurt(members, out var index))
            {
                var member = members[index];
                var given = math.min(amount, member.MaxHealth - member.Health);
                member.Health += given;
                members[index] = member;
                amount -= given;
            }

            return Total(members);
        }

        private static bool TryFindMostHurt(DynamicBuffer<SquadMember> members, out int index)
        {
            index = -1;
            var worst = 1e-3f;
            for (var i = 0; i < members.Length; i++)
            {
                var missing = members[i].MaxHealth - members[i].Health;
                if (members[i].Alive && missing > worst)
                {
                    worst = missing;
                    index = i;
                }
            }

            return index >= 0;
        }

        /// <summary>The offset of living member <paramref name="rank"/> of <paramref name="count"/> in a ring round a halted squad.</summary>
        public static float2 SpreadSlot(int rank, int count)
        {
            if (count <= 1)
            {
                return float2.zero;
            }

            var angle = math.PI * 0.25f + rank * math.PI * 2f / count;
            return new float2(math.cos(angle), math.sin(angle)) * SpreadRadius;
        }

        /// <summary>
        /// The offset in a loose wedge pointing along <paramref name="heading"/>, snapped to one of
        /// <see cref="WedgeHeadings"/> so a squad on a curving path doesn't resend its offsets every frame: a point man,
        /// then pairs falling back to the sides.
        /// </summary>
        public static float2 WedgeSlot(int rank, int count, float2 heading)
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
    }
}
