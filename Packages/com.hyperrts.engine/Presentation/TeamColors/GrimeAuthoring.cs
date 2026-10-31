using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.TeamColors
{
    /// <summary>
    /// Makes a ground vehicle collect dust, mud, wet and soot on its HyperRTS/LitTeam meshes. Put it on the unit root,
    /// never on structures.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Units + "Grime")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public class GrimeAuthoring : AuthoringBehaviour
    {
        [Tooltip("Dust gained per 100 m driven on dry Soil, Gravel and Sand, 0 to 1.")]
        [Min(0f)]
        public float dustPer100m = 0.25f;

        [Tooltip("Mud gained per 100 m driven on Mud, and on Soil and Grass while the ground is wet, 0 to 1.")]
        [Min(0f)]
        public float mudPer100m = 0.4f;

        [Tooltip("Seconds for a soaked hull to dry.")]
        [Min(1f)]
        public float dryingSeconds = 60f;

        [Tooltip("Dust lost per second in water.")]
        [Min(0f)]
        public float dustWashPerSecond = 0.5f;

        [Tooltip("Mud lost per second in water.")]
        [Min(0f)]
        public float mudWashPerSecond = 0.1f;

        [Tooltip("Metres above the ground where mud ends: about the road wheels and lower hull.")]
        [Min(0.1f)]
        public float mudHeight = 0.7f;

        [Tooltip("Metres above the ground where dust ends: about the top of the hull.")]
        [Min(0.1f)]
        public float dustHeight = 1.6f;

        public class Baker : Baker<GrimeAuthoring>
        {
            public override void Bake(GrimeAuthoring authoring)
            {
                AddComponent(GetEntity(TransformUsageFlags.Dynamic), new Grime
                {
                    DustPer100m = authoring.dustPer100m,
                    MudPer100m = authoring.mudPer100m,
                    DryingSeconds = authoring.dryingSeconds,
                    DustWashPerSecond = authoring.dustWashPerSecond,
                    MudWashPerSecond = authoring.mudWashPerSecond,
                    MudHeight = authoring.mudHeight,
                    DustHeight = authoring.dustHeight,
                });
            }
        }
    }
}
