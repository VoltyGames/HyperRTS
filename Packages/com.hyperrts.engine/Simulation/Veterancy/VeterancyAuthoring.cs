using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Stats;
using System;
using System.Collections.Generic;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Veterancy
{
    /// <summary>Lets a unit earn experience from kills and rank up; each rank adds its stat bonuses.</summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Veterancy")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Veterancy)]
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

        // Each buffer is filled before the next is added: adding one invalidates earlier buffer handles.
        public static void Add(EntityWriter writer, IReadOnlyList<float> thresholds,
            IReadOnlyList<VeterancyBonus> bonuses)
        {
            writer.Add<Experience>();
            var ranks = writer.AddBuffer<VeterancyRank>();
            foreach (var threshold in thresholds)
            {
                ranks.Add(new VeterancyRank { Experience = threshold });
            }

            writer.AddBuffer(bonuses);
        }

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
                    foreach (var modifier in StatBonus.ToModifiers(rank.bonuses, StatSource.Veterancy))
                    {
                        bonuses.Add(new VeterancyBonus { Rank = (byte)(i + 1), Modifier = modifier });
                    }
                }

                var writer = this.Writer();
                VeterancyAuthoring.Add(writer, thresholds, bonuses);
            }
        }
    }
}
