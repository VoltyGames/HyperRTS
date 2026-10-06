using System.Collections.Generic;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Fields;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Stats;
using HyperRTS.Simulation.Upgrades;
using HyperRTS.Simulation.Veterancy;
using HyperRTS.Simulation.Vision;
using Unity.Entities;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>Adds the components every unit and building carries.</summary>
    public static class GameEntitySetup
    {
        // Each buffer is filled before the next is added: adding one invalidates earlier buffer handles.
        public static void Add<TWriter>(ref TWriter writer, in GameEntitySpec spec,
            IReadOnlyList<ResourceCost> cost = null, IReadOnlyList<Prerequisite> prerequisites = null)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new EntityInfo { TypeId = spec.TypeId, Name = spec.Name, Icon = spec.Icon });
            writer.Add(new Faction { Value = spec.Owner });
            writer.Add(new Health { Current = spec.MaxHealth, Max = spec.MaxHealth });
            writer.Add<Dead>();
            writer.SetEnabled<Dead>(false);
            writer.Add<LastAttacker>();
            writer.AddBuffer<BaseStat>();
            writer.AddBuffer<StatModifier>();
            writer.AddBuffer<FieldPresence>();
            writer.Add(new ExperienceValue { Value = spec.ExperienceValue });
            writer.Add(new AppliedUpgrades { Faction = spec.Owner });
            writer.Add(new VisionRange { Value = spec.VisionRange });
            writer.Add(new Producible { BuildTime = spec.BuildTime, Population = spec.Population });
            writer.AddBuffer(cost);
            writer.AddBuffer(prerequisites);
            writer.Add<Selectable>();
            writer.Add<Selected>();
            writer.SetEnabled<Selected>(false);

            if (spec.CountsForVictory)
            {
                writer.Add<VictoryCritical>();
            }

            if (spec.DeathPrefab != Entity.Null)
            {
                writer.Add(new SpawnOnDeath { Prefab = spec.DeathPrefab });
            }
        }
    }
}
