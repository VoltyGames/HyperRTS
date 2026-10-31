using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// A building made of <see cref="Section"/>s: hits land on the sections they reach, sections cut off from the
    /// ground fall, and the building's health is the sum of its sections'. Masks hold one bit per section. Only
    /// <see cref="Broken"/> reaches clients, as a ghost field on a ghost and through scenery changes on scenery.
    /// </summary>
    public struct Sections : IComponentData
    {
        /// <summary>Seconds between one support level falling and the next.</summary>
        public const float FallDelay = 0.3f;

        [GhostField] public ulong Broken;

        /// <summary>Standing but cut off from every anchor, waiting to fall.</summary>
        public ulong Falling;

        /// <summary>Broken by falling rather than by a hit, so their floor is lost.</summary>
        public ulong Fell;

        /// <summary>The broken mask the last settling saw, so rubble and paths follow what changed since.</summary>
        public ulong Settled;

        /// <summary>The broken mask the last collapse check saw, so supports are flooded only after a change.</summary>
        public ulong Checked;

        /// <summary>When the next level of <see cref="Falling"/> comes down; 0 while nothing falls.</summary>
        public double NextFall;

        /// <summary>Breaks so far, which numbers each <see cref="Section.BreakOrder"/>.</summary>
        public int Breaks;

        /// <summary>Heading of the last hit (<see cref="ToppleMath"/>), which picks the way a section blows.</summary>
        public byte Side;

        /// <summary>Heap left at the foot of a broken ground-floor outer wall; <c>Entity.Null</c> for none.</summary>
        public Entity Rubble;

        /// <summary>
        /// Deals <paramref name="hit"/> to the sections it reaches, each times <paramref name="scale"/> and its
        /// material's factor, and returns the damage dealt. A direct hit lands in full on the first section the line
        /// from <paramref name="origin"/> to <paramref name="position"/> enters (else the nearest), and its splash on
        /// the rest; splash from a hit elsewhere reaches every section within its radius.
        /// </summary>
        public float Route(DynamicBuffer<Section> sections, DynamicBuffer<SectionFactor> factors, in DamageEvent hit,
            float3 origin, float3 position, float scale, bool direct)
        {
            var entry = direct ? Entry(sections, origin, position) : -1;
            var dealt = 0f;
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section.IsBroken)
                {
                    continue;
                }

                var gap = section.Gap(position);
                if (i != entry && (hit.Radius <= 0f || gap > hit.Radius))
                {
                    continue;
                }

                var share = i == entry ? 1f : CombatMath.SplashFactor(gap, hit.Radius, hit.EdgeFactor);
                var amount = hit.Amount * share * scale * SectionFactor.Find(factors, section.Material, hit.Type);
                dealt += Damage(sections, i, amount);
            }

            return dealt;
        }

        /// <summary>Takes up to <paramref name="amount"/> from section <paramref name="index"/>, breaking it at 0.</summary>
        public float Damage(DynamicBuffer<Section> sections, int index, float amount)
        {
            var section = sections[index];
            var dealt = math.min(section.Health, math.max(amount, 0f));
            section.Health -= dealt;
            if (section.Health <= 0f)
            {
                section.Health = 0f;
                section.BreakOrder = ++Breaks;
                Broken |= 1ul << index;
                Falling &= ~(1ul << index);
            }

            sections[index] = section;
            return dealt;
        }

        /// <summary>
        /// Rebuilds the latest broken section first, each coming back once full, then mends the standing ones.
        /// Returns the health restored.
        /// </summary>
        public float Repair(DynamicBuffer<Section> sections, float amount)
        {
            var restored = 0f;
            while (amount > 0f)
            {
                var index = Latest(sections);
                if (index < 0)
                {
                    index = Damaged(sections);
                }

                if (index < 0)
                {
                    break;
                }

                var section = sections[index];
                var added = math.min(section.MaxHealth - section.Health, amount);
                section.Health += added;
                amount -= added;
                restored += added;
                if (section.IsBroken && section.Health >= section.MaxHealth)
                {
                    section.BreakOrder = 0;
                    Broken &= ~(1ul << index);
                    Fell &= ~(1ul << index);
                }

                sections[index] = section;
            }

            return restored;
        }

        /// <summary>Standing sections that reach an anchor through standing supports.</summary>
        public readonly ulong Supported(DynamicBuffer<Section> sections)
        {
            var supported = 0ul;
            var grew = true;
            while (grew)
            {
                grew = false;
                for (var i = 0; i < sections.Length; i++)
                {
                    var bit = 1ul << i;
                    var section = sections[i];
                    if ((supported & bit) != 0 || section.IsBroken)
                    {
                        continue;
                    }

                    if (section.Anchored || (section.Supports & supported) != 0)
                    {
                        supported |= bit;
                        grew = true;
                    }
                }
            }

            return supported;
        }

        /// <summary>Falling sections whose supports are all down, or all of them when they only hold each other up.</summary>
        public readonly ulong DueToFall(DynamicBuffer<Section> sections)
        {
            var due = 0ul;
            for (var i = 0; i < sections.Length; i++)
            {
                if ((Falling & (1ul << i)) != 0 && (sections[i].Supports & ~Broken) == 0)
                {
                    due |= 1ul << i;
                }
            }

            return due != 0 ? due : Falling;
        }

        /// <summary>Whether section <paramref name="index"/> closes its nav part: on the ground and not broken.</summary>
        public readonly bool Blocks(DynamicBuffer<Section> sections, int index) =>
            sections[index].Anchored && (Broken & (1ul << index)) == 0;

        /// <summary>Floors, a bit each, that lost a section to a fall.</summary>
        public readonly byte FallenFloors(DynamicBuffer<Section> sections)
        {
            var floors = 0;
            for (var i = 0; i < sections.Length; i++)
            {
                if ((Fell & (1ul << i)) != 0)
                {
                    floors |= 1 << sections[i].Floor;
                }
            }

            return (byte)floors;
        }

        /// <summary>The standing share of the outer walls' health, which is how much a garrison is sheltered.</summary>
        public static float Intact(DynamicBuffer<Section> sections)
        {
            var health = 0f;
            var max = 0f;
            foreach (var section in sections)
            {
                if (section.Exterior)
                {
                    health += section.Health;
                    max += section.MaxHealth;
                }
            }

            return max > 0f ? health / max : 1f;
        }

        private static int Entry(DynamicBuffer<Section> sections, float3 origin, float3 position)
        {
            var entry = -1;
            var nearest = float.MaxValue;
            for (var i = 0; i < sections.Length; i++)
            {
                if (!sections[i].IsBroken && sections[i].Enters(origin, position, out var along) && along < nearest)
                {
                    nearest = along;
                    entry = i;
                }
            }

            return entry >= 0 ? entry : Nearest(sections, position);
        }

        private static int Nearest(DynamicBuffer<Section> sections, float3 position)
        {
            var nearest = -1;
            var best = float.MaxValue;
            for (var i = 0; i < sections.Length; i++)
            {
                var gap = sections[i].Gap(position);
                if (!sections[i].IsBroken && gap < best)
                {
                    best = gap;
                    nearest = i;
                }
            }

            return nearest;
        }

        private static int Latest(DynamicBuffer<Section> sections)
        {
            var latest = -1;
            for (var i = 0; i < sections.Length; i++)
            {
                if (sections[i].IsBroken && (latest < 0 || sections[i].BreakOrder > sections[latest].BreakOrder))
                {
                    latest = i;
                }
            }

            return latest;
        }

        private static int Damaged(DynamicBuffer<Section> sections)
        {
            for (var i = 0; i < sections.Length; i++)
            {
                if (sections[i].Health < sections[i].MaxHealth)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
