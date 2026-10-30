using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Leaves rubble, ash, fuel or wet mud on the ground where this unit or building dies, for the rest of the match.</summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Death Remains")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class DeathRemainsAuthoring : AuthoringBehaviour
    {
        [Tooltip("Metres from where it dies the remains reach, about half the hull's length.")]
        [Min(0.1f)]
        public float radius = 3f;

        [Tooltip("Broken stone and brick: 0 none, 1 fully covered.")]
        [Range(0f, 1f)]
        public float rubble;

        [Tooltip("Grey ash: 0 none, 1 fully covered.")]
        [Range(0f, 1f)]
        public float ash;

        [Tooltip("Spilt fuel and oil: 0 none, 1 a dark, glossy slick.")]
        [Range(0f, 1f)]
        public float fuel = 0.8f;

        [Tooltip("Wet, churned mud: 0 none, 1 fully covered.")]
        [Range(0f, 1f)]
        public float mud = 0.3f;

        public class Baker : Baker<DeathRemainsAuthoring>
        {
            public override void Bake(DeathRemainsAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.Add(new DeathRemains
                {
                    Radius = authoring.radius,
                    Amounts = new float4(authoring.rubble, authoring.ash, authoring.fuel, authoring.mud),
                });
            }
        }
    }
}
