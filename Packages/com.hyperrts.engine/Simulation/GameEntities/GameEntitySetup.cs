using System.Collections.Generic;
using HyperRTS.Simulation.Auras;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Stats;
using HyperRTS.Simulation.Status;
using HyperRTS.Simulation.Upgrades;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.GameEntities;

/// <summary>Adds the components every unit and building carries.</summary>
public static class GameEntitySetup
{
    public static void Add(EntityWriter writer, in GameEntitySpec spec,
        IReadOnlyList<ResourceCost> cost = null, IReadOnlyList<Prerequisite> prerequisites = null)
    {
        writer.Add(SharedTypes());
        writer.Add(new ComponentTypeSet(ComponentType.ReadWrite<Selectable>(), ComponentType.ReadWrite<Selected>()));
        writer.Set(spec.Info);
        writer.Set(new Owner { Value = spec.Owner });
        writer.Set(new Health { Current = spec.MaxHealth, Max = spec.MaxHealth });
        writer.SetEnabled<Dead>(false);
        writer.Set(new AppliedUpgrades { Owner = spec.Owner });
        writer.Set(new Producible { BuildTime = spec.BuildTime, Population = spec.Population });
        writer.SetBuffer(cost);
        writer.SetBuffer(prerequisites);
        writer.SetEnabled<Selected>(false);
        VisionSetup.AddVision(writer, spec.VisionRange, spec.EyeHeight);

        if (spec.Tall)
        {
            writer.Add<TallTarget>();
        }

        if (spec.CountsForVictory)
        {
            writer.Add<VictoryCritical>();
        }

        if (spec.Remains != Entity.Null)
        {
            writer.Add(new SpawnsRemains { Prefab = spec.Remains });
        }
    }

    // A set holds 15 types at most.
    private static ComponentTypeSet SharedTypes() => new(new FixedList128Bytes<ComponentType>
    {
        ComponentType.ReadWrite<EntityInfo>(),
        ComponentType.ReadWrite<Owner>(),
        ComponentType.ReadWrite<SpawnNumber>(),
        ComponentType.ReadWrite<Health>(),
        ComponentType.ReadWrite<Dead>(),
        ComponentType.ReadWrite<LastAttacker>(),
        ComponentType.ReadWrite<BaseStat>(),
        ComponentType.ReadWrite<StatBasesPending>(),
        ComponentType.ReadWrite<StatModifier>(),
        ComponentType.ReadWrite<AuraPresence>(),
        ComponentType.ReadWrite<ActiveStatus>(),
        ComponentType.ReadWrite<AppliedUpgrades>(),
        ComponentType.ReadWrite<Producible>(),
        ComponentType.ReadWrite<ResourceCost>(),
        ComponentType.ReadWrite<Prerequisite>(),
    });
}
