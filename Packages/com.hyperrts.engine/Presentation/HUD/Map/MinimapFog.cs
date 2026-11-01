using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Fog;
using HyperRTS.Simulation.Vision;
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
        private Texture2D _texture;
        private int _version = -1;
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
            if (view.Version == _version && _texture != null)
            {
                return;
            }

            _fogGrid.Complete(entityManager);
            if (_texture == null || _texture.width != fog.Grid.Size.x || _texture.height != fog.Grid.Size.y)
            {
                Object.Destroy(_texture);
                _texture = new Texture2D(fog.Grid.Size.x, fog.Grid.Size.y, TextureFormat.Alpha8, false, true)
                {
                    name = "Minimap Fog",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
                _layer.style.backgroundImage = new StyleBackground(_texture);
            }

            FogTexels.Fill(fog, view.Team, _explored, _unexplored,
                _texture.GetPixelData<byte>(0));
            _texture.Apply(false);
            _version = view.Version;
        }

        private void OnStyleResolved(CustomStyleResolvedEvent evt)
        {
            evt.customStyle.TryGetValue(_exploredAlpha, out var explored);
            evt.customStyle.TryGetValue(_unexploredAlpha, out var unexplored);
            _explored = (byte)Mathf.RoundToInt(explored * 255f);
            _unexplored = (byte)Mathf.RoundToInt(unexplored * 255f);
            _version = -1;
        }

        private void Release()
        {
            Object.Destroy(_texture);
            _texture = null;
            _layer.style.backgroundImage = StyleKeyword.None;
        }
    }
}
