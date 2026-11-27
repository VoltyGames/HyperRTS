using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Stats;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Production;

/// <summary>Trains the units in its <see cref="ProductionQueueItem"/> queue, one at a time.</summary>
public struct Producer : IComponentData
{
    public float3 SpawnOffset;
    public int QueueLimit;

    /// <summary>Production speed multiplier (1 = build times as listed) from stats; low power slows it further.</summary>
    [StatField(Stat.ProductionSpeed)] public float Speed;

    /// <summary>Seconds of work done on the head of the queue.</summary>
    [GhostField(Quantization = 100)] public float Elapsed;

    /// <summary>Units released so far; every client sees it rise, so doors open for enemies too.</summary>
    [GhostField] public uint Released;

    [GhostField] public ProducerState State;

    /// <summary>Whether a finished producer of <paramref name="owner"/> offers <paramref name="prefab"/> and has queue room.</summary>
    public static bool CanQueue(EntityManager entityManager, Entity producer, byte owner, Entity prefab)
    {
        if (!IsReady(entityManager, producer, owner))
        {
            return false;
        }

        var hasRoom = entityManager.GetBuffer<ProductionQueueItem>(producer, true).Length
            < entityManager.GetComponentData<Producer>(producer).QueueLimit;
        return hasRoom && PrefabOptions.Has(entityManager.GetBuffer<ProductionOption>(producer, true), prefab);
    }

    /// <summary>Whether <paramref name="producer"/> is a finished producer of <paramref name="owner"/>.</summary>
    public static bool IsReady(EntityManager entityManager, Entity producer, byte owner)
    {
        if (!entityManager.HasComponent<Producer>(producer) || entityManager.GetComponentData<Owner>(producer).Value != owner)
        {
            return false;
        }

        return !entityManager.HasEnabled<ConstructionProgress>(producer);
    }

    /// <summary>Share of <paramref name="head"/>, the item at the head of the queue, built.</summary>
    public readonly float HeadProgress(in Producible head) =>
        head.BuildTime > 0f ? math.saturate(Elapsed / head.BuildTime) : 1f;
}

/// <summary>A unit prefab a producer can train.</summary>
[InternalBufferCapacity(6)]
public struct ProductionOption : IBufferElementData, IPrefabOption
{
    public Entity Prefab;

    Entity IPrefabOption.Prefab => Prefab;
}

/// <summary>Where new units walk after spawning; disabled means stay at the spawn point.</summary>
public struct RallyPoint : IComponentData, IEnableableComponent
{
    public float3 Position;
}

/// <summary>A new unit's move to the rally point, issued by <see cref="RallyOrderSystem"/>.</summary>
public struct RallyOrder : IComponentData
{
    public float3 Position;
}
