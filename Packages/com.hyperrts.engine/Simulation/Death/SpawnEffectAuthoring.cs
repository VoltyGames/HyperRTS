using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Death
{
    /// <summary>Plays a particle effect once where this appears: put it on ruins, rubble and other remains.</summary>
    [AddComponentMenu(HyperRTSMenu.Look + "Effects/Spawn Effect")]
    [Icon(HyperRTSIcons.SpawnEffectAuthoring)]
    [HelpURL(HyperRTSDocs.Combat)]
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

                var writer = this.Writer();
                writer.Add(new SpawnEffect { Prefab = authoring.effect });
            }
        }
    }
}
