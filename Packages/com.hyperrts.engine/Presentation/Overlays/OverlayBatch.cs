using System;
using HyperRTS.Presentation.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>One frame's screen-aligned instances in one draw, with the style values pushed to the material when they change.</summary>
    public abstract class OverlayBatch<TInstance> : IDisposable where TInstance : unmanaged
    {
        private static readonly int PixelScaleId = Shader.PropertyToID("_PixelScale");

        private readonly InstanceBuffer<TInstance> _instances;
        private readonly Material _material;
        private OverlayStyle _appliedStyle;
        private float _appliedPixelScale;
        private int _appliedVersion;

        protected OverlayBatch(Mesh billboard, Material material)
        {
            _instances = new InstanceBuffer<TInstance>(billboard);
            _material = new Material(material) { hideFlags = HideFlags.DontSave };
        }

        public void Draw(OverlayStyle style, float uiScale, int layer)
        {
            if (_instances.Count == 0)
            {
                return;
            }

            var pixelScale = style.PixelScale(Screen.height, uiScale);
            var stale = style != _appliedStyle || style.Version != _appliedVersion || pixelScale != _appliedPixelScale;
            if (stale)
            {
                _material.SetFloat(PixelScaleId, pixelScale);
                ApplyStyle(style, _material);
                _appliedStyle = style;
                _appliedPixelScale = pixelScale;
                _appliedVersion = style.Version;
            }

            _instances.Draw(_material, layer);
        }

        public void Dispose()
        {
            _instances.Dispose();
            Object.Destroy(_material);
        }

        protected void Add(in TInstance instance) => _instances.Add(instance);

        /// <summary>Writes the style values the shader reads, once per style version rather than every frame.</summary>
        protected abstract void ApplyStyle(OverlayStyle style, Material material);
    }
}
