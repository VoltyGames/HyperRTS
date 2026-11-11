using System;
using HyperRTS.Presentation.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Overlays.Bars
{
    /// <summary>One frame's screen-aligned health bars, all in one draw.</summary>
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
        private static readonly int SuppressedId = Shader.PropertyToID("_Suppressed");
        private static readonly int PinnedId = Shader.PropertyToID("_Pinned");
        private static readonly int RetreatId = Shader.PropertyToID("_Retreat");
        private static readonly int HeavyCoverId = Shader.PropertyToID("_HeavyCover");
        private static readonly int LightCoverId = Shader.PropertyToID("_LightCover");

        private readonly InstanceBuffer<HealthBarInstance> _bars;
        private readonly Material _material;
        private OverlayStyle _appliedStyle;
        private float _appliedPixelScale;
        private int _appliedVersion;

        public HealthBars(Mesh billboard, Material material)
        {
            _bars = new InstanceBuffer<HealthBarInstance>(billboard);
            _material = new Material(material) { hideFlags = HideFlags.DontSave };
        }

        /// <summary>Reference pixels to screen pixels at the current resolution and UI scale.</summary>
        public static float PixelScale(float screenHeight, float uiScale) => screenHeight / ReferenceHeight * uiScale;

        public void Add(Vector3 anchor, in HealthBarData bar, OverlayStyle style)
        {
            _bars.Add(new HealthBarInstance
            {
                Bar = new Vector4(bar.Fraction, bar.Segments, bar.Rank, 1f),
                Size = new Vector4(bar.Width, style.barHeight, bar.Squad.Members, bar.Squad.Alive),
                Squad = new Vector4(bar.Squad.Suppression, (float)bar.Squad.Level, (float)bar.Squad.Cover,
                    bar.Squad.Retreating ? 1f : 0f),
                Anchor = anchor,
            });
        }

        public void Draw(OverlayStyle style, float uiScale, int layer)
        {
            if (_bars.Count == 0)
            {
                return;
            }

            var pixelScale = PixelScale(Screen.height, uiScale);
            var stale = style != _appliedStyle || style.Version != _appliedVersion || pixelScale != _appliedPixelScale;
            if (stale)
            {
                Apply(style, pixelScale);
            }

            _bars.Draw(_material, layer);
        }

        public void Dispose()
        {
            _bars.Dispose();
            Object.Destroy(_material);
        }

        // Style values are read once per style version, not every frame.
        private void Apply(OverlayStyle style, float pixelScale)
        {
            _material.SetFloat(PixelScaleId, pixelScale);
            _material.SetFloat(OffsetId, style.barOffset);
            _material.SetColor(BackId, style.healthBack);
            _material.SetColor(HighId, style.healthHigh);
            _material.SetColor(MidId, style.healthMid);
            _material.SetColor(LowId, style.healthLow);
            _material.SetColor(RankPipId, style.rankPip);
            _material.SetColor(MemberPipId, style.memberPip);
            _material.SetColor(SuppressedId, style.suppressed);
            _material.SetColor(PinnedId, style.pinned);
            _material.SetColor(RetreatId, style.retreating);
            _material.SetColor(HeavyCoverId, style.heavyCover);
            _material.SetColor(LightCoverId, style.lightCover);
            _appliedStyle = style;
            _appliedPixelScale = pixelScale;
            _appliedVersion = style.Version;
        }
    }
}
