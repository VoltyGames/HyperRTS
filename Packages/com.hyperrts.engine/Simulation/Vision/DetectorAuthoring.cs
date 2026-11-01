using HyperRTS.Core;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Reveals hostile stealthed entities within a radius to this entity's team.</summary>
    [AddComponentMenu(HyperRTSMenu.Vision + "Detector")]
    [Icon(HyperRTSIcons.Vision)]
    [HelpURL(HyperRTSDocs.Vision)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class DetectorAuthoring : AuthoringBehaviour
    {
        [Tooltip("Detection radius in world units, separate from the vision range.")]
        [Min(0f)]
        public float radius = 10f;

        public class Baker : Baker<DetectorAuthoring>
        {
            public override void Bake(DetectorAuthoring authoring)
            {
                var writer = this.Writer();
                StealthSetup.AddDetector(writer, authoring.radius);
            }
        }
    }
}
