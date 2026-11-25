using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Ground
{
    /// <summary>
    /// The fixed ground mesh that stands in for the terrain in light transport, which leaves the terrain out because
    /// crater dents rebuild it. Never drawn.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    [Icon(HyperRTSIcons.GroundColor)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GroundBounce : PresentationBehaviour
    {
        // Unity doesn't save forceRenderingOff with the scene. A mesh that is never drawn is never uploaded either,
        // and Surface Cache then traces an empty one.
        private void OnEnable()
        {
            GetComponent<MeshRenderer>().forceRenderingOff = true;
            GetComponent<MeshFilter>().sharedMesh.UploadMeshData(false);
        }
    }
}
