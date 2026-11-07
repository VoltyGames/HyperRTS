using HyperRTS.Simulation.Common.Infantry;
using System;
using UnityEngine;

namespace HyperRTS.Simulation.Squads.Cover
{
    /// <summary>A match's cover rules as set on the Match; baked into <see cref="CoverRules"/>.</summary>
    [Serializable]
    public struct CoverSettings
    {
        [Tooltip("Light cover (fences, hay, woodpiles, craters, and woods or tall crops with no spot).")]
        public CoverProtection light;

        [Tooltip("Heavy cover (sandbags, concrete, wrecks, rocks).")]
        public CoverProtection heavy;

        [Tooltip("Half angle in degrees either side of a spot's facing that its cover protects.")]
        [Range(0f, 180f)]
        public float frontArc;

        [Tooltip("Metres round a halted squad to look for spots, at most the squad's spread.")]
        [Min(0f)]
        public float searchRadius;

        public static CoverSettings Standard => new()
        {
            light = new CoverProtection { damageTaken = 0.75f, suppressionTaken = 0.6f, preference = 10f },
            heavy = new CoverProtection { damageTaken = 0.5f, suppressionTaken = 0.3f, preference = 20f },
            frontArc = 75f, searchRadius = 3.5f,
        };

        public readonly CoverRules Rules
        {
            get
            {
                var rules = new CoverRules { ArcCos = Mathf.Cos(frontArc * Mathf.Deg2Rad), SearchRadius = searchRadius };
                rules.Entries.Length = CoverRules.Kinds;
                rules.Entries[(int)CoverKind.None] = CoverEntry.Open;
                rules.Entries[(int)CoverKind.Light] = light.Entry;
                rules.Entries[(int)CoverKind.Heavy] = heavy.Entry;
                return rules;
            }
        }
    }
}
