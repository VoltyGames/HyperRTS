using HyperRTS.Simulation.Common.Infantry;
using System;
using UnityEngine;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>A match's squad rules as set on the Match; baked into <see cref="SquadRules"/>.</summary>
    [Serializable]
    public struct SquadSettings
    {
        [Header("Suppression")]
        [Tooltip("Suppression (0 to 1) from which a squad is suppressed: slower and it fires less often.")]
        [Range(0f, 1f)]
        public float suppressedAt;

        [Tooltip("Suppression from which a squad is pinned: prone, held still, and it fires much less often.")]
        [Range(0f, 1f)]
        public float pinnedAt;

        [Tooltip("Seconds without incoming fire before suppression starts to fall.")]
        [Min(0f)]
        public float decayDelay;

        [Tooltip("Suppression lost a second once it falls.")]
        [Min(0f)]
        public float decayRate;

        [Tooltip("Speed multiplier of a suppressed squad, on top of crouching.")]
        [Range(0f, 1f)]
        public float suppressedSpeed;

        [Tooltip("Rate of fire multiplier of a suppressed squad.")]
        [Range(0f, 1f)]
        public float suppressedFireRate;

        [Tooltip("Rate of fire multiplier of a pinned squad.")]
        [Range(0f, 1f)]
        public float pinnedFireRate;

        [Header("Posture")]
        [Tooltip("Speed multiplier of a squad moving crouched.")]
        [Range(0f, 1f)]
        public float crouchSpeed;

        [Tooltip("Speed multiplier of a prone squad. Pinned squads are prone, so 0 holds them still.")]
        [Range(0f, 1f)]
        public float proneSpeed;

        [Header("Retreat")]
        [Tooltip("Speed multiplier of a retreating squad.")]
        [Min(0f)]
        public float retreatSpeed;

        [Tooltip("Share of damage a retreating squad takes.")]
        [Range(0f, 1f)]
        public float retreatDamageTaken;

        [Header("Reinforce")]
        [Tooltip("Share of a member's part of the squad's price that bringing it back costs.")]
        [Min(0f)]
        public float reinforceCost;

        [Tooltip("Seconds to bring back one member.")]
        [Min(0.1f)]
        public float reinforceTime;

        [Header("Formation")]
        [Tooltip("Metres of ring a halted squad gives each living member, so the ring grows with the squad. 3.5 puts four members 2.2 m out.")]
        [Min(0.1f)]
        public float memberSpacing;

        [Tooltip("Metres between the rows of a moving squad's wedge.")]
        [Min(0f)]
        public float wedgeDepth;

        [Tooltip("Metres a wedge's members stand to the side for each row.")]
        [Min(0f)]
        public float wedgeWidth;

        [Tooltip("Furthest a member stands from a squad that moves or halts, in metres. It also caps the cover search and the ring.")]
        [Min(0f)]
        public float maxSpread;

        [Tooltip("Furthest a digging member stands from its squad, in metres.")]
        [Min(0f)]
        public float workSpread;

        [Tooltip("Share of the squad's speed a member runs at to catch up with its slot.")]
        [Min(0f)]
        public float catchUp;

        [Tooltip("Metres from its place on a building site within which a member digs.")]
        [Min(0f)]
        public float digRange;

        public static SquadSettings Standard => new()
        {
            suppressedAt = 0.4f, pinnedAt = 0.8f, decayDelay = 3f, decayRate = 0.2f,
            suppressedSpeed = 0.85f, suppressedFireRate = 0.75f, pinnedFireRate = 0.3f,
            crouchSpeed = 0.8f, proneSpeed = 0f,
            retreatSpeed = 1.25f, retreatDamageTaken = 0.5f,
            reinforceCost = 0.75f, reinforceTime = 4f,
            memberSpacing = 3.5f, wedgeDepth = 1.2f, wedgeWidth = 1f, maxSpread = 3.5f, workSpread = 6f,
            catchUp = 0.75f, digRange = 0.25f,
        };

        public readonly SquadRules Rules => new()
        {
            SuppressedAt = suppressedAt,
            PinnedAt = pinnedAt,
            DecayDelay = decayDelay,
            DecayRate = decayRate,
            SuppressedSpeed = suppressedSpeed,
            SuppressedFireRate = suppressedFireRate,
            PinnedFireRate = pinnedFireRate,
            CrouchSpeed = crouchSpeed,
            ProneSpeed = proneSpeed,
            RetreatSpeed = retreatSpeed,
            RetreatDamageTaken = retreatDamageTaken,
            ReinforceCost = reinforceCost,
            ReinforceTime = reinforceTime,
            MemberSpacing = memberSpacing,
            WedgeDepth = wedgeDepth,
            WedgeWidth = wedgeWidth,
            MaxSpread = maxSpread,
            WorkSpread = workSpread,
            CatchUp = catchUp,
            DigRange = digRange,
        };
    }
}
