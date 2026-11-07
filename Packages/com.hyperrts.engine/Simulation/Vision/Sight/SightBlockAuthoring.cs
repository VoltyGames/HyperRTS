using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Vision.Sight
{
    /// <summary>
    /// Blocks sight over a box centred on this transform: solid below its height (a house, a wall, a wreck), porous
    /// below its porous top (a tree crown, smoke), and concealing still infantry if set (ruins, trenches).
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Vision + "Sight Block")]
    [Icon(HyperRTSIcons.Vision)]
    [HelpURL(HyperRTSDocs.Vision)]
    [DisallowMultipleComponent]
    public class SightBlockAuthoring : AuthoringBehaviour
    {
        [Tooltip("Blocked footprint (X by Z) centred on this transform. Fit To Model in the context menu fills it.")]
        public Vector2 size = new(4f, 4f);

        [Tooltip("Solid top above this transform, in metres: the roof or the wall's top. 0 for cover sight seeps through.")]
        [Min(0f)]
        public float height = 3f;

        [Tooltip("Share of sight lost per metre through the porous part: 0.1 hides what lies past 10 m of it. 0 for none.")]
        [Min(0f)]
        public float density;

        [Tooltip("Top of the porous part above this transform, in metres, such as a tree's crown.")]
        [Min(0f)]
        public float porousTop;

        [Tooltip("Still infantry on it are concealed, as in ruins and trenches. Porous cover conceals without it.")]
        public bool conceals;

        /// <summary>Sets the size and height to the box round the active meshes, centred on this transform.</summary>
        [InspectorButton("Fit to model")]
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

        public static void Add(EntityWriter writer, in SightBlock block) =>
            writer.Add(block);

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            var top = Mathf.Max(height, density > 0f ? porousTop : 0f);
            Gizmos.DrawWireCube(new Vector3(0f, top * 0.5f, 0f), new Vector3(size.x, top, size.y));
        }

        public class Baker : Baker<SightBlockAuthoring>
        {
            public override void Bake(SightBlockAuthoring authoring)
            {
                var writer = this.Writer();
                Add(writer, new SightBlock
                {
                    Size = authoring.size,
                    Height = authoring.height,
                    Density = authoring.density,
                    PorousTop = authoring.porousTop,
                    Conceals = authoring.conceals,
                });
            }
        }
    }
}
