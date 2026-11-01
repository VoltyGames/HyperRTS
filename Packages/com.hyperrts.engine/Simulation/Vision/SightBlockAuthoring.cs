using HyperRTS.Core;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Blocks sight below its top over a box centred on this transform: a house, a wall or a wreck.</summary>
    [AddComponentMenu(HyperRTSMenu.Vision + "Sight Block")]
    [Icon(HyperRTSIcons.Vision)]
    [HelpURL(HyperRTSDocs.Vision)]
    [DisallowMultipleComponent]
    public class SightBlockAuthoring : AuthoringBehaviour
    {
        [Tooltip("Blocked footprint (X by Z) centred on this transform. Fit To Model in the context menu fills it.")]
        public Vector2 size = new(4f, 4f);

        [Tooltip("Top of the block above this transform, in metres: the roof or the wall's top.")]
        [Min(0.25f)]
        public float height = 3f;

        /// <summary>Sets the size and height to the box round the active meshes, centred on this transform.</summary>
        [ContextMenu("Fit To Model")]
        public void FitToModel()
        {
            var reach = Vector3.zero;
            var top = 0f;
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                var bounds = renderer.localBounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var local = new Vector3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
                    var point = transform.InverseTransformPoint(
                        renderer.transform.TransformPoint(bounds.min + Vector3.Scale(bounds.size, local)));
                    reach = Vector3.Max(reach, new Vector3(Mathf.Abs(point.x), 0f, Mathf.Abs(point.z)));
                    top = Mathf.Max(top, point.y);
                }
            }

            size = new Vector2(reach.x * 2f, reach.z * 2f);
            height = Mathf.Max(top, 0.25f);
        }

        public static void Add<TWriter>(ref TWriter writer, float2 size, float height)
            where TWriter : struct, IEntityWriter =>
            writer.Add(new SightBlock { Size = size, Height = height });

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, height * 0.5f, 0f), new Vector3(size.x, height, size.y));
        }

        public class Baker : Baker<SightBlockAuthoring>
        {
            public override void Bake(SightBlockAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                Add(ref writer, authoring.size, authoring.height);
            }
        }
    }
}
