using HyperRTS.Simulation.Common.Combat;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Common.Infantry
{
    /// <summary>A squad's members buffer with the rules over their health, who lives and who a hit lands on.</summary>
    public struct SquadRoster
    {
        private DynamicBuffer<SquadMember> _members;

        public SquadRoster(DynamicBuffer<SquadMember> members) => _members = members;

        /// <summary>Health of the living _members, which the squad's <see cref="Health.Current"/> equals.</summary>
        public float Total()
        {
            var total = 0f;
            foreach (var member in _members)
            {
                total += math.max(0f, member.Health);
            }

            return total;
        }

        public float TotalMax()
        {
            var total = 0f;
            foreach (var member in _members)
            {
                total += member.MaxHealth;
            }

            return total;
        }

        public int CountLiving()
        {
            var count = 0;
            foreach (var member in _members)
            {
                count += member.Alive ? 1 : 0;
            }

            return count;
        }

        /// <summary>Takes <paramref name="amount"/> off one member only, none carrying over; returns the squad's new total.</summary>
        public float Hurt(int index, float amount)
        {
            var member = _members[index];
            member.Health = math.max(0f, member.Health - amount);
            _members[index] = member;
            return Total();
        }

        /// <summary>The most _members whose life <see cref="LivingMask"/> can hold.</summary>
        public const int MaskedMembers = 16;

        /// <summary>Bit i is set when member i lives, for the first <see cref="MaskedMembers"/> _members.</summary>
        public ushort LivingMask()
        {
            var mask = 0;
            for (var i = 0; i < math.min(_members.Length, MaskedMembers); i++)
            {
                mask |= _members[i].Alive ? 1 << i : 0;
            }

            return (ushort)mask;
        }

        /// <summary>
        /// Gives the _members in <paramref name="living"/> (see <see cref="LivingMask"/>) <paramref name="total"/> between
        /// them in proportion to their maximum, and the rest none: a replay records only the squad's health and who lives.
        /// </summary>
        public void Fill(float total, ushort living)
        {
            var capacity = 0f;
            for (var i = 0; i < _members.Length; i++)
            {
                capacity += IsMasked(living, i) ? _members[i].MaxHealth : 0f;
            }

            for (var i = 0; i < _members.Length; i++)
            {
                var member = _members[i];
                var share = IsMasked(living, i) && capacity > 0f ? total * member.MaxHealth / capacity : 0f;
                member.Health = math.clamp(share, 0f, member.MaxHealth);
                _members[i] = member;
            }
        }

        private static bool IsMasked(ushort mask, int index) => index < MaskedMembers && (mask & (1 << index)) != 0;

        /// <summary>Counts the living _members that don't stand at a crew place, whose slots share the ring when <paramref name="emplaced"/>.</summary>
        public int CountFreeLiving(bool emplaced)
        {
            var count = 0;
            foreach (var member in _members)
            {
                count += member.Alive && !member.Crews(emplaced) ? 1 : 0;
            }

            return count;
        }

        /// <summary>Restores the most hurt living member first, then the next, until <paramref name="amount"/> is spent.</summary>
        public float Heal(float amount)
        {
            while (amount > 0f && TryFindMostHurt(out var index))
            {
                var member = _members[index];
                var given = math.min(amount, member.MaxHealth - member.Health);
                member.Health += given;
                _members[index] = member;
                amount -= given;
            }

            return Total();
        }

        private bool TryFindMostHurt(out int index)
        {
            index = -1;
            var worst = 1e-3f;
            for (var i = 0; i < _members.Length; i++)
            {
                var missing = _members[i].MaxHealth - _members[i].Health;
                if (_members[i].Alive && missing > worst)
                {
                    worst = missing;
                    index = i;
                }
            }

            return index >= 0;
        }

        public bool AnyDead() => CountLiving() < _members.Length;

        /// <summary>Brings the first dead member back at full health in the squad's centre; it walks to its slot.</summary>
        public void Revive()
        {
            for (var i = 0; i < _members.Length; i++)
            {
                if (!_members[i].Alive)
                {
                    var member = _members[i];
                    member.Health = member.MaxHealth;
                    member.Offset = float2.zero;
                    member.Slot = float2.zero;
                    member.Posture = Posture.Stand;
                    member.Death = DeathCause.None;
                    member.LeaveSpot();
                    _members[i] = member;
                    return;
                }
            }
        }

        /// <summary>
        /// One living member chosen by <paramref name="seed"/>, or -1 when all are dead. Members weigh by the share of a
        /// hit from <paramref name="from"/> they take, so exposed ones are hit more often than ones in cover.
        /// </summary>
        public int PickExposed(float3 squad, uint seed,
            in CoverRules rules, float3 from, float piercing)
        {
            var total = 0f;
            for (var i = 0; i < _members.Length; i++)
            {
                total += _members[i].Alive ? _members[i].ExposureTo(rules, squad, from, piercing).Damage : 0f;
            }

            var roll = Random.CreateFromIndex(seed).NextFloat() * total;
            var last = -1;
            for (var i = 0; i < _members.Length; i++)
            {
                if (!_members[i].Alive)
                {
                    continue;
                }

                last = i;
                roll -= _members[i].ExposureTo(rules, squad, from, piercing).Damage;
                if (roll < 0f)
                {
                    return i;
                }
            }

            return last;
        }

        /// <summary>The living member standing nearest <paramref name="point"/>, or -1 when all are dead.</summary>
        public int FindNearest(float3 squad, float3 point)
        {
            var nearest = -1;
            var best = float.MaxValue;
            for (var i = 0; i < _members.Length; i++)
            {
                var distance = math.distancesq(_members[i].Position(squad).xz, point.xz);
                if (_members[i].Alive && distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            return nearest;
        }
    }
}
