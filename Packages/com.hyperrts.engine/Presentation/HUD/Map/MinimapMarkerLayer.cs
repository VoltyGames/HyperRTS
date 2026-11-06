using HyperRTS.Presentation.Common;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD.Map
{
    /// <summary>
    /// The minimap's markers as one texture the size of their layer, redrawn by a Burst job and uploaded once per
    /// refresh. Its texture goes when the layer leaves its panel, as on a HUD rebuild.
    /// </summary>
    internal sealed class MinimapMarkerLayer
    {
        private readonly VisualElement _layer;
        private Texture2D _texture;

        public MinimapMarkerLayer(VisualElement layer)
        {
            _layer = layer;
            _layer.RegisterCallback<DetachFromPanelEvent>(_ => Release());
        }

        public void Draw(NativeArray<MinimapMarker> markers, MinimapStyle style)
        {
            var size = new int2(Mathf.RoundToInt(_layer.contentRect.width), Mathf.RoundToInt(_layer.contentRect.height));
            if (size.x <= 0 || size.y <= 0)
            {
                return;
            }

            if (_texture == null || _texture.width != size.x || _texture.height != size.y)
            {
                Object.Destroy(_texture);
                _texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false, false)
                {
                    name = "Minimap Markers",
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
                _layer.style.backgroundImage = new StyleBackground(_texture);
            }

            new MinimapMarkerJob
            {
                Markers = markers,
                Pixels = _texture.GetPixelData<Color32>(0),
                Size = size,
                Outline = style.OutlineColor,
                OutlineWidth = style.OutlineWidth,
            }.Run();
            _texture.Apply(false);
        }

        private void Release()
        {
            Object.Destroy(_texture);
            _texture = null;
            _layer.style.backgroundImage = StyleKeyword.None;
        }
    }
}
