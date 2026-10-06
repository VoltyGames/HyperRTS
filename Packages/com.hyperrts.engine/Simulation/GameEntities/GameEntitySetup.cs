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
        public static void Add<TSink>(ref TSink sink, in GameEntitySpec spec, IReadOnlyList<ResourceCost> cost = null,
            IReadOnlyList<Prerequisite> prerequisites = null) where TSink : struct, IComponentSink
        {
            sink.Add(new EntityInfo { TypeId = spec.TypeId, Name = spec.Name, Icon = spec.Icon });
            sink.Add(new Faction { Value = spec.Owner });
            sink.Add(new Health { Current = spec.MaxHealth, Max = spec.MaxHealth });
            sink.Add<Dead>();
            sink.SetEnabled<Dead>(false);
            sink.Add<LastAttacker>();
            sink.AddBuffer<BaseStat>();
            sink.AddBuffer<StatModifier>();
            sink.AddBuffer<FieldPresence>();
            sink.Add(new ExperienceValue { Value = spec.ExperienceValue });
            sink.Add(new AppliedUpgrades { Faction = spec.Owner });
            sink.Add(new VisionRange { Value = spec.VisionRange });
            sink.Add(new Producible { BuildTime = spec.BuildTime, Population = spec.Population });
            Fill(sink.AddBuffer<ResourceCost>(), cost);
            Fill(sink.AddBuffer<Prerequisite>(), prerequisites);
            sink.Add<Selectable>();
            sink.Add<Selected>();
            sink.SetEnabled<Selected>(false);

            if (spec.CountsForVictory)
            {
                sink.Add<VictoryCritical>();
            }

            if (spec.DeathPrefab != Entity.Null)
            {
                sink.Add(new SpawnOnDeath { Prefab = spec.DeathPrefab });
            }
        }

        private static void Fill<T>(DynamicBuffer<T> buffer, IReadOnlyList<T> items)
            where T : unmanaged, IBufferElementData
        {
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                buffer.Add(item);
            }
        }
    }
}
