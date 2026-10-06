using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Units;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Capture
{
    /// <summary>Lets a unit capture buildings that have a Capturable component.</summary>
    [AddComponentMenu(HyperRTSMenu.Units + "Capturer")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(UnitAuthoring), "a Unit")]
    public class CapturerAuthoring : AuthoringBehaviour
    {
        [Tooltip("Capture speed multiplier.")]
        [Min(0.01f)]
        public float rate = 1f;

        [Tooltip("The unit is used up when the capture completes (it enters the building).")]
        public bool consumedOnCapture;

        public class Baker : Baker<CapturerAuthoring>
        {
            public override void Bake(CapturerAuthoring authoring)
            {
                var sink = new BakerSink(this, GetEntity(TransformUsageFlags.Dynamic));
                CaptureSetup.AddCapturer(ref sink, authoring.rate, authoring.consumedOnCapture);
            }
        }
    }

    /// <summary>Captures <see cref="Capturable"/> buildings through Capture orders.</summary>
    public struct Capturer : IComponentData
    {
        public float Rate;
        public bool ConsumedOnCapture;
    }
}
