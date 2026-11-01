using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>
    /// A building made of <see cref="Section"/>s: hits land on the sections they reach, sections cut off from the
    /// ground fall, and the building's health is the sum of its sections'. Masks hold one bit per section. Only
    /// <see cref="Broken"/> reaches clients, as a ghost field on a ghost and through scenery changes on scenery.
    /// </summary>
    public partial struct SectionedBuilding : IComponentData
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

        /// <summary>Heap left at the foot of a broken ground-floor outer wall; <c>Entity.Null</c> for none.</summary>
        public Entity Rubble;

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
        /// Breaks and rebuilds sections so exactly those in <paramref name="broken"/> stand broken, as another world
        /// decided; the settling that follows opens paths and drops rubble.
        /// </summary>
        public void SetBroken(DynamicBuffer<Section> sections, ulong broken)
        {
            if (Broken == broken)
            {
                return;
            }

            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                var now = (broken & (1ul << i)) != 0;
                if (now != section.IsBroken)
                {
                    section.Health = now ? 0f : section.MaxHealth;
                    section.BreakOrder = now ? ++Breaks : 0;
                    sections[i] = section;
                }
            }

            Broken = broken;
        }

        /// <summary>The mask with a bit for each of <paramref name="count"/> sections.</summary>
        public static ulong All(int count) => count >= 64 ? ~0ul : (1ul << count) - 1ul;

        /// <summary>Standing sections that reach an anchor through standing supports.</summary>
        public static ulong Supported(DynamicBuffer<Section> sections)
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

                    if (section.StandsOn(supported))
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

        /// <summary>Whether section <paramref name="index"/> closes its nav part: one that <see cref="Section.Closes"/>, not broken.</summary>
        public readonly bool Blocks(DynamicBuffer<Section> sections, int index) =>
            sections[index].Closes && (Broken & (1ul << index)) == 0;

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

        /// <summary>
        /// The first standing section the line from <paramref name="origin"/> to <paramref name="position"/> enters, else
        /// the nearest.
        /// </summary>
        public static int Entry(DynamicBuffer<Section> sections, float3 origin, float3 position)
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
    }
}
