using HyperRTS.Simulation.Common.Infantry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Damage
{
    /// <summary>What one hit did to a squad: the member it struck and the health that member held and lost.</summary>
    public struct MemberWound
    {
        public int Member;
        public float Held;
        public float Dealt;
        public float SquadHealth;
    }

    /// <summary>Hits and heals on a squad, which land on its <see cref="SquadMember"/>s.</summary>
    public struct SquadDamage
    {
        private BufferLookup<SquadMember> _members;
        private ComponentLookup<Suppression> _suppressions;
        [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
        private CoverRules _cover;

        public SquadDamage(ref SystemState state)
        {
            _members = state.GetBufferLookup<SquadMember>();
            _suppressions = state.GetComponentLookup<Suppression>();
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _cover = default;
        }

        public void Update(ref SystemState state, in CoverRules cover)
        {
            _members.Update(ref state);
            _suppressions.Update(ref state);
            _transforms.Update(ref state);
            _cover = cover;
        }

        public bool IsSquad(Entity target) => _members.HasBuffer(target);

        public DynamicBuffer<SquadMember> Members(Entity squad) => _members[squad];

        /// <summary>The member a hit is aimed at, else the member nearest a blast aimed at a squad; -1 lets a direct hit roll.</summary>
        public int Nearest(in DamageEvent hit)
        {
            if (hit.AimedMember >= 0)
            {
                return hit.AimedMember;
            }

            if (hit.Profile.Radius <= 0f || !_members.TryGetBuffer(hit.Target, out var members)
                || !_transforms.TryGetComponent(hit.Target, out var squad))
            {
                return -1;
            }

            return SquadMember.FindNearest(members, squad.Position, hit.Position);
        }

        /// <summary>Restores health to the most hurt members; returns the squad's health.</summary>
        public float Heal(Entity squad, float amount) => SquadMember.Heal(_members[squad], amount);

        /// <summary>
        /// Deals <paramref name="amount"/>, scaled by the struck member's cover, to the member the blow names or one
        /// rolled by its seed, and adds the blow's suppression. False when no member can be struck.
        /// </summary>
        public bool Hurt(Entity squad, float amount, in DamageEvent hit, in Blow blow, out MemberWound wound)
        {
            var members = _members[squad];
            var position = _transforms[squad].Position;
            var piercing = hit.Profile.CoverPiercing;
            var index = blow.Member >= 0
                ? blow.Member
                : SquadMember.PickExposed(members, position, blow.Seed, _cover, blow.From, piercing);
            if (index < 0)
            {
                wound = default;
                return false;
            }

            var exposure = members[index].ExposureTo(_cover, position, blow.From, piercing);
            var dealt = amount * exposure.Damage;
            var held = members[index].Health;
            var squadHealth = SquadMember.Hurt(members, index, dealt);
            RecordDeath(members, index, held, hit, position);
            Suppress(squad, blow.Suppression * exposure.Suppression);
            wound = new MemberWound { Member = index, Held = held, Dealt = dealt, SquadHealth = squadHealth };
            return true;
        }

        /// <summary>A member a hit just dropped remembers whether a blast or a shot did it, and the way the blast lay.</summary>
        private static void RecordDeath(DynamicBuffer<SquadMember> members, int index, float heldBefore,
            in DamageEvent hit, float3 squad)
        {
            var member = members[index];
            if (heldBefore <= 0f || member.Alive)
            {
                return;
            }

            var blast = hit.Profile.Radius > 0f;
            member.Death = blast ? DeathCause.Blast : DeathCause.Shot;
            member.BlastHeading = blast ? SquadMember.PackHeading(hit.Position.xz - member.Position(squad).xz) : (byte)0;
            members[index] = member;
        }

        private void Suppress(Entity squad, float amount)
        {
            if (amount > 0f && _suppressions.TryGetComponent(squad, out var suppression))
            {
                suppression.Add(amount);
                _suppressions[squad] = suppression;
            }
        }
    }
}
