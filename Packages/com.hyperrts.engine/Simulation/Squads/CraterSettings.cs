using System;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>A match's crater rules as set on the Match; baked into <see cref="CraterRules"/> when it names a prefab.</summary>
    [Serializable]
    public struct CraterSettings
    {
        [Tooltip("Cover entity a heavy blast on open ground leaves, with Cover Spots filled as a ring. Empty: no craters give cover.")]
        public GameObject prefab;

        [Tooltip("Smallest splash radius in metres of a blast that digs a crater. Small arms and light shells dig none.")]
        [Min(0f)]
        public float minRadius;

        [Tooltip("Most craters on the map at once; past it the oldest is removed.")]
        [Min(1)]
        public int maxCount;

        [Tooltip("Metres a new crater keeps from every other crater and cover spot.")]
        [Min(0f)]
        public float spacing;

        public static CraterSettings Standard => new() { minRadius = 3f, maxCount = 24, spacing = 4f };

        public readonly CraterRules Rules(Entity baked) => new()
        {
            Prefab = baked, MinRadius = minRadius, MaxCount = maxCount, Spacing = spacing,
        };
    }
}
