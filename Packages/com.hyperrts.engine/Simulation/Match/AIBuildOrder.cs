using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Match
{
    /// <summary>An AI opening, usually one per side: what to build, train and research, in order.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Match + "AI Build Order", fileName = "AIBuildOrder")]
    [HelpURL(HyperRTSDocs.AI)]
    public class AIBuildOrder : ScriptableObject
    {
        [Serializable]
        public class Step
        {
            [Tooltip("Building, unit or upgrade prefab.")]
            public GameObject prefab;

            [Tooltip("How many the AI should own; queued units and unfinished buildings count. Upgrades count once.")]
            [Min(1)]
            public int count = 1;
        }

        [Tooltip("Each think the AI starts the first unmet step it can afford. Once all are met it trains and " +
                 "researches freely.")]
        public List<Step> steps = new();

        public List<(Entity Prefab, int Count)> BakeSteps(IBaker baker)
        {
            baker.DependsOn(this);
            var baked = new List<(Entity, int)>();
            foreach (var step in steps)
            {
                if (step.prefab != null)
                {
                    // None: the prefab's own bakers pick its transform usage (upgrades have none).
                    baked.Add((baker.GetEntity(step.prefab, TransformUsageFlags.None), step.count));
                }
            }

            return baked;
        }
    }
}
