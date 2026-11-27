using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Players;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Strikes;

/// <summary>Counts the working enemy weapons that defend a strike's target, each of which weakens the strike.</summary>
public struct StrikeDefences
{
    private EntityQuery _weapons;

    public StrikeDefences(ref SystemState state) =>
        _weapons = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<WeaponMount, LocalTransform, Owner>()
            .WithNone<Offline>()
            .Build(ref state);

    /// <summary>
    /// Weapons hostile to <paramref name="owner"/> that reach <paramref name="point"/> and shoot at aircraft, or at
    /// projectiles of <paramref name="kind"/>.
    /// </summary>
    public readonly int Count(ref SystemState state, float3 point, byte owner, in OwnerRelations relations,
        ProjectileKind kind)
    {
        using var entities = _weapons.ToEntityArray(Allocator.Temp);
        var count = 0;
        foreach (var entity in entities)
        {
            if (!relations.IsHostile(owner, state.EntityManager.GetComponentData<Owner>(entity).Value))
            {
                continue;
            }

            var from = state.EntityManager.GetComponentData<LocalTransform>(entity).Position;
            foreach (var mount in state.EntityManager.GetBuffer<WeaponMount>(entity, true))
            {
                count += mount.Weapon.Defends(from, point, kind) ? 1 : 0;
            }
        }

        return count;
    }
}
