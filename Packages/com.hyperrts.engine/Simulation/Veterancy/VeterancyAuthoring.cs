using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Stats;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Veterancy
{
    /// <summary>Lets a unit earn experience from kills and rank up; each rank adds its stat bonuses.</summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Veterancy")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class VeterancyAuthoring : AuthoringBehaviour
    {
        [Serializable]
        public class Rank
        {
            [Tooltip("Total experience needed to reach this rank.")]
            [Min(0f)]
            public float experience = 100f;

            [Tooltip("Bonuses gained at this rank, on top of the lower ranks' bonuses.")]
            public List<StatBonus> bonuses = new();
        }

        [Tooltip("Ranks in ascending order of experience.")]
        public List<Rank> ranks = new();

        public class Baker : Baker<VeterancyAuthoring>
        {
            public override void Bake(VeterancyAuthoring authoring)
            {
                var thresholds = new List<float>();
                var bonuses = new List<VeterancyBonus>();
                for (var i = 0; i < authoring.ranks.Count; i++)
                {
                    var rank = authoring.ranks[i];
                    thresholds.Add(rank.experience);
                    foreach (var bonus in rank.bonuses)
                    {
                        bonuses.Add(new VeterancyBonus
                        {
                            Rank = (byte)(i + 1),
                            Modifier = bonus.ToModifier(StatSource.Veterancy),
                        });
                    }
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                VeterancySetup.Add(ref writer, thresholds, bonuses);
            }
        }
    }
}
