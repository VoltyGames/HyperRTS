using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Common.Infantry
{
    public partial struct SquadMember
    {
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

        /// <summary>Takes <paramref name="amount"/> off one member only, none carrying over; returns the squad's new total.</summary>
        public static float Hurt(DynamicBuffer<SquadMember> members, int index, float amount)
        {
            var member = members[index];
            member.Health = math.max(0f, member.Health - amount);
            members[index] = member;
            return Total(members);
        }

        /// <summary>The most members whose life <see cref="LivingMask"/> can hold.</summary>
        public const int MaskedMembers = 16;

        /// <summary>Bit i is set when member i lives, for the first <see cref="MaskedMembers"/> members.</summary>
        public static ushort LivingMask(DynamicBuffer<SquadMember> members)
        {
            var mask = 0;
            for (var i = 0; i < math.min(members.Length, MaskedMembers); i++)
            {
                mask |= members[i].Alive ? 1 << i : 0;
            }

            return (ushort)mask;
        }

        /// <summary>
        /// Gives the members in <paramref name="living"/> (see <see cref="LivingMask"/>) <paramref name="total"/> between
        /// them in proportion to their maximum, and the rest none: a replay records only the squad's health and who lives.
        /// </summary>
        public static void Fill(DynamicBuffer<SquadMember> members, float total, ushort living)
        {
            var capacity = 0f;
            for (var i = 0; i < members.Length; i++)
            {
                capacity += IsMasked(living, i) ? members[i].MaxHealth : 0f;
            }

            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                var share = IsMasked(living, i) && capacity > 0f ? total * member.MaxHealth / capacity : 0f;
                member.Health = math.clamp(share, 0f, member.MaxHealth);
                members[i] = member;
            }
        }

        private static bool IsMasked(ushort mask, int index) => index < MaskedMembers && (mask & (1 << index)) != 0;

        /// <summary>Counts the living members that don't stand at a crew place, whose slots share the ring when <paramref name="emplaced"/>.</summary>
        public static int CountFreeLiving(DynamicBuffer<SquadMember> members, bool emplaced)
        {
            var count = 0;
            foreach (var member in members)
            {
                count += member.Alive && !member.Crews(emplaced) ? 1 : 0;
            }

            return count;
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

        public static bool AnyDead(DynamicBuffer<SquadMember> members) => CountLiving(members) < members.Length;

        /// <summary>Brings the first dead member back at full health in the squad's centre; it walks to its slot.</summary>
        public static void Revive(DynamicBuffer<SquadMember> members)
        {
            for (var i = 0; i < members.Length; i++)
            {
                if (!members[i].Alive)
                {
                    var member = members[i];
                    member.Health = member.MaxHealth;
                    member.Offset = float2.zero;
                    member.Slot = float2.zero;
                    member.Posture = Posture.Stand;
                    member.Death = DeathCause.None;
                    member.LeaveSpot();
                    members[i] = member;
                    return;
                }
            }
        }
    }
}
