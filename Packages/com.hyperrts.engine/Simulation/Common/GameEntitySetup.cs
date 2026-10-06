using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Fields;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Stats;
using HyperRTS.Simulation.Upgrades;
using HyperRTS.Simulation.Veterancy;
using HyperRTS.Simulation.Vision;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Adds the components every unit and building carries.</summary>
    public static class GameEntitySetup
    {
        public static void Add<TSink>(ref TSink sink, in GameEntitySpec spec) where TSink : struct, IComponentSink
        {
            sink.Add(new EntityInfo { TypeId = spec.TypeId, Name = spec.Name, Icon = spec.Icon });
            sink.Add(new Faction { Value = spec.Owner });
            sink.Add(new Health { Current = spec.MaxHealth, Max = spec.MaxHealth });
            sink.Add<Dead>();
            sink.SetEnabled<Dead>(false);
            sink.Add<LastAttacker>();
            sink.Add<BaseStats>();
            sink.AddBuffer<StatModifier>();
            sink.AddBuffer<FieldPresence>();
            sink.Add(new ExperienceValue { Value = spec.ExperienceValue });
            sink.Add(new AppliedUpgrades { Faction = spec.Owner });
            sink.Add(new VisionRange { Value = spec.VisionRange });
            sink.Add(new Producible { BuildTime = spec.BuildTime, Population = spec.Population });
            sink.AddBuffer<ResourceCost>();
            sink.AddBuffer<Prerequisite>();
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
    }
}
