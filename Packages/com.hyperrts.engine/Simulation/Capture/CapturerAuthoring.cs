using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Capture
{
    /// <summary>Lets a unit capture buildings that have a Capturable component.</summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Units/Capturer")]
    [Icon(HyperRTSIcons.CapturerAuthoring)]
    [HelpURL(HyperRTSDocs.Capture)]
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
                var writer = this.Writer();
                CaptureSetup.AddCapturer(writer, authoring.rate, authoring.consumedOnCapture);
            }
        }
    }
}
