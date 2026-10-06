using System;
using System.Collections.Generic;
using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Simulation.AI
{
    /// <summary>An AI opening, usually one per faction: what to build, train and research, in order.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Match + "AI Build Order", fileName = "AIBuildOrder")]
    [HelpURL(HyperRTSDocs.Modules)]
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
    }
}
