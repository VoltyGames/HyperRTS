using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Cues.Effects
{
    /// <summary>Leaves a crater, scorch, rubble, ash, fuel or wet mud on the ground where this unit or building dies, for the rest of the match.</summary>
    [AddComponentMenu(HyperRTSMenu.Look + "Cues/Death Stain")]
    [Icon(HyperRTSIcons.DeathStainAuthoring)]
    [HelpURL(HyperRTSDocs.Death)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class DeathStainAuthoring : AuthoringBehaviour
    {
        [Tooltip("Metres from where it dies to the crater's rim, about half the hull's length; " +
                 "scorch and stains spread a quarter wider.")]
        [Min(0.1f)]
        public float radius = 3f;

        [Tooltip("What the death leaves in the ground for the rest of the match.")]
        public GroundScar scar = new() { fuel = 0.8f, mud = 0.3f };

        public class Baker : Baker<DeathStainAuthoring>
        {
            public override void Bake(DeathStainAuthoring authoring)
            {
                var writer = this.Writer();
                writer.Add(new DeathStain
                {
                    Radius = authoring.radius,
                    Scar = authoring.scar,
                });
            }
        }
    }
}
