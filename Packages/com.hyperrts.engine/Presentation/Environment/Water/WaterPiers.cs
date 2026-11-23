using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Water
{
    /// <summary>
    /// Hands a map's bridge piers to the water shader as globals, which draws foam round each. Map tools add it to the
    /// generated water object; globals keep the water on the SRP Batcher with its per-object sky light.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class WaterPiers : PresentationBehaviour
    {
        /// <summary>Most piers one map can have; Water.shader's MAX_WATER_PIERS matches it.</summary>
        public const int Capacity = 16;

        private static readonly int PiersId = Shader.PropertyToID("_HyperRTSWaterPiers");
        private static readonly int CountId = Shader.PropertyToID("_HyperRTSWaterPierCount");

        [Tooltip("Each pier as (x, z, half-width across the flow) in world metres.")]
        public Vector3[] points = System.Array.Empty<Vector3>();

        private readonly Vector4[] _piers = new Vector4[Capacity];

        private void OnEnable() => Publish();

        protected override void OnDisabled() => Shader.SetGlobalFloat(CountId, 0f);

        private void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                Publish();
            }
        }

        private void Publish()
        {
            if (points.Length > Capacity)
            {
                throw new System.InvalidOperationException($"{name}: the map has {points.Length} piers, over the {Capacity} the water shader holds.");
            }

            for (var i = 0; i < points.Length; i++)
            {
                _piers[i] = new Vector4(points[i].x, points[i].y, points[i].z, 0f);
            }

            Shader.SetGlobalVectorArray(PiersId, _piers);
            Shader.SetGlobalFloat(CountId, points.Length);
        }
    }
}
