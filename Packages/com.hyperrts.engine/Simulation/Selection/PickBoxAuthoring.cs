using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>The box clicks, hovers and right-clicks pick this entity by; drag it in the Scene view.</summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Selection/Pick Box")]
    [Icon(HyperRTSIcons.PickBoxAuthoring)]
    [HelpURL(HyperRTSDocs.Selection)]
    [DisallowMultipleComponent]
    public class PickBoxAuthoring : AuthoringBehaviour
    {
        [Tooltip("Centre of the box in this object's space.")]
        public Vector3 center;

        [Tooltip("Full size of the box along this object's axes.")]
        public Vector3 size = Vector3.one;

        /// <summary>Wraps the box round every renderer below this object.</summary>
        public void FitToRenderers()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            var scale = transform.lossyScale;
            center = transform.InverseTransformPoint(bounds.center);
            size = new Vector3(bounds.size.x / scale.x, bounds.size.y / scale.y, bounds.size.z / scale.z);
        }

        public static void AddPickBox(EntityWriter writer, float3 center, float3 size) =>
            writer.Add(new PickBox { Center = center, HalfSize = size * 0.5f });

        public class Baker : Baker<PickBoxAuthoring>
        {
            public override void Bake(PickBoxAuthoring authoring)
            {
                var writer = this.Writer();
                AddPickBox(writer, authoring.center, authoring.size);
            }
        }
    }
}
