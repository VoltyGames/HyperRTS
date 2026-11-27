using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Tests;
using HyperRTS.Simulation.Veterancy;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Network.Tests;

/// <summary>A client draws the rank the server promoted a unit to, though it never sees the points behind it.</summary>
public class VeterancyReplicationTests
{
    [Test]
    public void Rank_ReachesTheClient_WithoutPoints()
    {
        using var session = new HostTestKit(1, 2);
        var host = session.Host;
        host.SpawnBuilding(1, float3.zero, new float2(4f, 4f));
        var prefab = session.GhostUnitPrefab(2, "Sergeant");
        var unit = host.EntityManager.Spawn(prefab, new float3(20f, 0f, 20f), quaternion.identity, 2);
        host.Set(unit, new Experience { Points = 40f, Rank = 2 });

        var client = session.Client;
        Assert.IsTrue(session.RunUntil(() => ClientRank(client) == 2, 20f), "the client sees the unit's rank");
        using var query = client.Query(typeof(GhostInstance), typeof(Experience));
        var seen = client.Get<Experience>(query.GetSingletonEntity());
        Assert.AreEqual(0f, seen.Points, "only the rank replicates");
    }

    private static int ClientRank(TestWorld client)
    {
        using var query = client.Query(typeof(GhostInstance), typeof(Experience));
        using var units = query.ToEntityArray(Allocator.Temp);
        return units.Length == 1 ? client.Get<Experience>(units[0]).Rank : -1;
    }
}
