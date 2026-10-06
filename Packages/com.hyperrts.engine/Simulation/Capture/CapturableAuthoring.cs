using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Capture
{
    /// <summary>Lets capturer units take this building over (neutral oil derricks, enemy outposts).</summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Capturable")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
    public class CapturableAuthoring : AuthoringBehaviour
    {
        [Tooltip("Seconds one capturer needs; several capturers of one player add up.")]
        [Min(0.1f)]
        public float captureTime = 10f;

        public class Baker : Baker<CapturableAuthoring>
        {
            public override void Bake(CapturableAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                CaptureSetup.AddCapturable(ref writer, authoring.captureTime);
            }
        }
    }
}
