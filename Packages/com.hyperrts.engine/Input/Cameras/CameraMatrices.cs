using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Input.Cameras
{
    /// <summary>Camera matrices in the simulation's math types, for screen-space picking.</summary>
    public static class CameraMatrices
    {
        /// <summary>projection * worldToCamera, the matrix SelectionMath projects with.</summary>
        public static float4x4 ViewProjection(Camera camera)
        {
            var vp = camera.projectionMatrix * camera.worldToCameraMatrix;
            return new float4x4(vp.GetColumn(0), vp.GetColumn(1), vp.GetColumn(2), vp.GetColumn(3));
        }
    }
}
