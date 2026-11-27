using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Production;

/// <summary>A producer that is sold or destroyed gives its whole queue back to its owner, once.</summary>
public class ProducerLossTests : SimulationFixture
{
    private ResourceType _supplies;
    private Entity _prefab;
    private Entity _producer;

    [SetUp]
    public void Prepare()
    {
        _supplies = World.CreateSupplies();
        _prefab = World.UnitPrefab(_supplies, 30);
        _producer = World.SpawnProducer(_prefab);
        World.SpawnProvider(1, new float3(20f, 0f, 20f), 10);
        World.AddResources(1, _supplies, 100);
        World.Produce(1, _producer, _prefab);
        World.Produce(1, _producer, _prefab);
        World.Tick();
    }

    [Test]
    public void Sell_OnTheFrameTheHeadFinishes_SpawnsNothing_AndRefundsOnce()
    {
        var producer = World.Get<Producer>(_producer);
        producer.Elapsed = World.Get<Producible>(_prefab).BuildTime;
        World.Set(_producer, producer);

        World.Command(1, new PlayerCommand { Type = CommandType.Sell, Unit = _producer });
        World.Tick();

        Assert.AreEqual(0, World.All<UnitTag>().Length, "the sold queue is gone before production runs");
        Assert.AreEqual(100, World.GetResources(1, _supplies));
    }

    [Test]
    public void Destroyed_RefundsItsQueueToItsOwner_Once()
    {
        Assert.AreEqual(40, World.GetResources(1, _supplies));

        World.Kill(_producer);
        World.Tick(frames: 3);

        Assert.AreEqual(100, World.GetResources(1, _supplies));
    }
}
