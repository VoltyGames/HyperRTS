using System;
using HyperRTS.Presentation.Overlays.Bars;
using HyperRTS.Presentation.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Overlays.Pips
{
    /// <summary>One frame's owner-coloured pips over units too small on screen to pick out, all in one draw.</summary>
    public sealed class UnitPips : IDisposable
    {
        private static readonly int PixelScaleId = Shader.PropertyToID("_PixelScale");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int OffsetId = Shader.PropertyToID("_Offset");
        private static readonly int BackId = Shader.PropertyToID("_Back");

        private readonly InstanceBuffer<OverlayInstance> _pips;
        private readonly Material _material;
        private OverlayStyle _appliedStyle;
        private float _appliedPixelScale;
        private int _appliedVersion;

        public UnitPips(Mesh billboard, Material material)
        {
            _pips = new InstanceBuffer<OverlayInstance>(billboard);
            _material = new Material(material) { hideFlags = HideFlags.DontSave };
        }

        /// <summary>Whether a unit <paramref name="screenHeight"/> reference pixels tall is too small to pick out.</summary>
        public static bool Shows(float screenHeight, OverlayStyle style) => screenHeight < style.smallUnitHeight;

        public void Add(Vector3 top, Color owner) => _pips.Add(new OverlayInstance(Matrix4x4.Translate(top), owner));

        public void Draw(OverlayStyle style, float uiScale, int layer)
        {
            if (_pips.Count == 0)
            {
                return;
            }

            var pixelScale = HealthBars.PixelScale(Screen.height, uiScale, style);
            var stale = style != _appliedStyle || style.Version != _appliedVersion || pixelScale != _appliedPixelScale;
            if (stale)
            {
                Apply(style, pixelScale);
            }

            _pips.Draw(_material, layer);
        }

        public void Dispose()
        {
            _pips.Dispose();
            Object.Destroy(_material);
        }

        private void Apply(OverlayStyle style, float pixelScale)
        {
            _material.SetFloat(PixelScaleId, pixelScale);
            _material.SetFloat(SizeId, style.unitPipSize);
            _material.SetFloat(OffsetId, style.unitPipOffset);
            _material.SetColor(BackId, style.markerBack);
            _appliedStyle = style;
            _appliedPixelScale = pixelScale;
            _appliedVersion = style.Version;
        }
    }
}
