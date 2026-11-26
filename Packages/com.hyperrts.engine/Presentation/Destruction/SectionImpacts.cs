using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Destruction;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// The impacts the local player saw this frame. Each holes the standing section its shot struck and is
    /// remembered there, which picks how the section breaks.
    /// </summary>
    internal struct SectionImpacts
    {
        /// <summary>Metres around a section's box that still count as an impact on it.</summary>
        private const float Margin = 0.5f;

        /// <summary>Metres back along a shot that its line is followed to the section it came in through.</summary>
        private const float Reach = 64f;

        public NativeList<CueEvent> Seen;

        /// <summary>
        /// Marks the first standing section on each shot's line and those its landing point touches; one the hit
        /// broke only remembers it. Returns the sections whose hole changed.
        /// </summary>
        public readonly ulong Record(in SectionViewContext at, ulong broken, ulong shown)
        {
            var changed = 0ul;
            if (Seen.Length == 0)
            {
                return changed;
            }

            var toBuilding = math.inverse(at.Place.Value);
            foreach (var impact in Seen)
            {
                var landed = math.transform(toBuilding, impact.Position);
                var along = math.normalizesafe(math.rotate(toBuilding, impact.Direction));
                var entry = Entry(at, shown, landed - along * Reach, landed, out var struck);
                for (var i = 0; i < at.Models.Length; i++)
                {
                    var bit = 1ul << i;
                    var touched = i == entry || at.Sections[i].Gap(landed) <= Margin;
                    if ((shown & bit) == 0ul && touched)
                    {
                        changed |= Mark(at, i, i == entry ? struck : landed, (broken & bit) != 0ul) ? bit : 0ul;
                    }
                }
            }

            return changed;
        }

        // The simulation deals a direct hit to the first standing section on the shot's line, wherever inside the
        // building it lands, so the view follows the same line for a shot that landed within the building.
        private static int Entry(in SectionViewContext at, ulong shown, float3 from, float3 landed, out float3 struck)
        {
            struck = landed;
            var min = new float3(float.MaxValue);
            var max = new float3(float.MinValue);
            var entry = -1;
            var nearest = float.MaxValue;
            for (var i = 0; i < at.Models.Length; i++)
            {
                var section = at.Sections[i];
                min = math.min(min, section.Center - section.HalfSize - Margin);
                max = math.max(max, section.Center + section.HalfSize + Margin);
                var stands = (shown & (1ul << i)) == 0ul;
                if (stands && section.Enters(from, landed, out var along) && along < nearest)
                {
                    nearest = along;
                    entry = i;
                }
            }

            if (entry < 0 || math.any(landed < min) || math.any(landed > max))
            {
                return -1;
            }

            struck = math.lerp(from, landed, nearest);
            return entry;
        }

        private static bool Mark(in SectionViewContext at, int index, float3 point, bool broken)
        {
            var look = at.Looks[index];
            var hole = broken ? look.Hole : (byte)(at.Holes.Nearest(index, point) + 1);
            var changed = hole != look.Hole;
            look.Hole = hole;
            look.Impact = point;
            look.ImpactTime = at.Now;
            var looks = at.Looks;
            looks[index] = look;
            return changed;
        }
    }
}
