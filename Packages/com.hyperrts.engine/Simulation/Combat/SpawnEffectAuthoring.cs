using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Plays a particle effect once where this appears: put it on ruins, rubble and other remains.</summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Spawn Effect")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class SpawnEffectAuthoring : AuthoringBehaviour
    {
        [Tooltip("Particle effect prefab (dust and debris of a collapse, the crash of a falling tree).")]
        public GameObject effect;

        public class Baker : Baker<SpawnEffectAuthoring>
        {
            public override void Bake(SpawnEffectAuthoring authoring)
            {
                if (authoring.effect == null)
                {
                    return;
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.Add(new SpawnEffect { Prefab = authoring.effect });
            }
        }
    }
}
