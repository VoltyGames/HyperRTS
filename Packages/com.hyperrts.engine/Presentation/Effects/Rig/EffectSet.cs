using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Cues.Effects;
using HyperRTS.Simulation.Cues.Surfaces;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Effects.Rig;

/// <summary>One entity type's effects: a rig per slot, and what its shots throw up where they land.</summary>
public sealed class EffectSet(EntityEffect[] effects, SurfaceEffects impacts)
{
    private readonly EntityEffect[] _effects = effects;

    /// <summary>The set of an entity with <see cref="EntityEffect"/>s or <see cref="EntityImpactEffects"/>.</summary>
    public static EffectSet Read(EntityManager entityManager, Entity source)
    {
        var effects = entityManager.HasBuffer<EntityEffect>(source)
            ? entityManager.GetBuffer<EntityEffect>(source, true).AsNativeArray().ToArray()
            : [];
        var impacts = entityManager.HasComponent<EntityImpactEffects>(source)
            ? entityManager.GetComponentData<EntityImpactEffects>(source).Effects.Value
            : null;
        return new EffectSet(effects, impacts);
    }

    /// <summary>Null when the type's shots throw nothing up.</summary>
    public SurfaceEffects Impacts { get; } = impacts;

    /// <summary>The rig the type's shots throw up on <paramref name="material"/>; null when they throw nothing up.</summary>
    public GameObject ImpactRig(SurfaceMaterial material) => Impacts != null ? Impacts.RigFor(material) : null;

    /// <summary>The slot and variant's rig entry; false when the type has none.</summary>
    public bool TryGet(CueSlot slot, byte variant, out EntityEffect effect)
    {
        foreach (var entry in _effects)
        {
            if (entry.Plays(slot, variant))
            {
                effect = entry;
                return true;
            }
        }

        effect = default;
        return false;
    }
}
