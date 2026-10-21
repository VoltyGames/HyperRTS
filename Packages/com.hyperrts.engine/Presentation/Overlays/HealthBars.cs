using System;
using HyperRTS.Presentation.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>One frame's screen-aligned health bars, all in one instanced draw.</summary>
    public sealed class HealthBars : IDisposable
    {
        // Sizes are authored in pixels on a 1080p screen.
        private const float ReferenceHeight = 1080f;

        private static readonly int PixelScaleId = Shader.PropertyToID("_PixelScale");
        private static readonly int OffsetId = Shader.PropertyToID("_Offset");
        private static readonly int BackId = Shader.PropertyToID("_Back");
        private static readonly int HighId = Shader.PropertyToID("_High");
        private static readonly int MidId = Shader.PropertyToID("_Mid");
        private static readonly int LowId = Shader.PropertyToID("_Low");
        private static readonly int RankPipId = Shader.PropertyToID("_RankPip");
        private static readonly int MemberPipId = Shader.PropertyToID("_MemberPip");

        private readonly InstanceDataBatch _batch;
        private readonly Material _material;

        public HealthBars(Mesh billboard, Material material)
        {
            _batch = new InstanceDataBatch(billboard, "_Bar", "_Size");
            _material = new Material(material) { hideFlags = HideFlags.DontSave };
        }

        /// <summary>Reference pixels to screen pixels at the current resolution and UI scale.</summary>
        public static float PixelScale(float screenHeight, float uiScale) => screenHeight / ReferenceHeight * uiScale;

        public void Add(Vector3 anchor, in HealthBarData bar, OverlayStyle style)
        {
            var matrix = Matrix4x4.Translate(anchor);
            var first = new Vector4(bar.Fraction, bar.Segments, bar.Rank, 1f);
            var second = new Vector4(bar.Width, style.barHeight, bar.Members, bar.Alive);
            _batch.Add(matrix, first, second);
        }

        public void Draw(OverlayStyle style, float uiScale, int layer)
        {
            if (_batch.Count == 0)
            {
                return;
            }

            _material.SetFloat(PixelScaleId, PixelScale(Screen.height, uiScale));
            _material.SetFloat(OffsetId, style.barOffset);
            _material.SetColor(BackId, style.healthBack);
            _material.SetColor(HighId, style.healthHigh);
            _material.SetColor(MidId, style.healthMid);
            _material.SetColor(LowId, style.healthLow);
            _material.SetColor(RankPipId, style.rankPip);
            _material.SetColor(MemberPipId, style.memberPip);
            _batch.Draw(_material, layer);
        }

        public void Dispose() => Object.Destroy(_material);
    }
}
