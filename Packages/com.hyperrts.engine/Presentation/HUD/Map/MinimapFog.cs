using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Fog;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD.Map
{
    /// <summary>
    /// The local team's fog of war as a minimap layer: dim where explored, dark where never seen. Its texture goes
    /// when the layer leaves its panel, as on a HUD rebuild.
    /// </summary>
    internal sealed class MinimapFog
    {
        private readonly CustomStyleProperty<float> _exploredAlpha = new("--minimap-explored-alpha");
        private readonly CustomStyleProperty<float> _unexploredAlpha = new("--minimap-unexplored-alpha");
        private readonly LiveSingleton<FogOfWar> _fogGrid = new();
        private readonly LiveSingleton<LocalFogView> _localView = new();

        private readonly VisualElement _layer;
        private readonly FogTexture _fogTexture = new("Minimap Fog", TextureFormat.Alpha8, false);
        private byte _explored;
        private byte _unexplored;

        public MinimapFog(VisualElement layer)
        {
            _layer = layer;
            _layer.RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
            _layer.RegisterCallback<DetachFromPanelEvent>(_ => Release());
        }

        public void Refresh(EntityManager entityManager)
        {
            var hasView = _localView.TryGet(entityManager, out var view);
            if (!hasView || !view.Active || !_fogGrid.TryGet(entityManager, out var fog))
            {
                _layer.SetHidden(true);
                return;
            }

            _layer.SetHidden(false);
            if (!_fogTexture.Stale(view))
            {
                return;
            }

            _fogGrid.Complete(entityManager);
            if (_fogTexture.Refill(fog, view, _explored, _unexplored))
            {
                _layer.style.backgroundImage = new StyleBackground(_fogTexture.Texture);
            }
        }

        private void OnStyleResolved(CustomStyleResolvedEvent evt)
        {
            evt.customStyle.TryGetValue(_exploredAlpha, out var explored);
            evt.customStyle.TryGetValue(_unexploredAlpha, out var unexplored);
            _explored = (byte)Mathf.RoundToInt(explored * 255f);
            _unexplored = (byte)Mathf.RoundToInt(unexplored * 255f);
            _fogTexture.Invalidate();
        }

        private void Release()
        {
            _fogTexture.Dispose();
            _layer.style.backgroundImage = StyleKeyword.None;
        }
    }
}
