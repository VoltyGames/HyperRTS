using HyperRTS.Client.Common;
using HyperRTS.Presentation.Fog;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// The local team's fog of war as a minimap layer: dim where explored, dark where never seen. Its texture goes
    /// when the layer leaves its panel, as on a HUD rebuild.
    /// </summary>
    internal sealed class MinimapFog
    {
        private const byte ExploredAlpha = 110;
        private const byte UnexploredAlpha = 215;

        private readonly LiveQuery _fogQuery = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<FogOfWar>()));

        private readonly LiveQuery _localView = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalFogView>()));

        private readonly VisualElement _layer;
        private Texture2D _texture;
        private int _version = -1;

        public MinimapFog(VisualElement layer)
        {
            _layer = layer;
            _layer.style.unityBackgroundImageTintColor = Color.black;
            _layer.RegisterCallback<DetachFromPanelEvent>(_ => Release());
        }

        public void Refresh(EntityManager entityManager)
        {
            var hasView = _localView.In(entityManager).TryGetSingleton(out LocalFogView view);
            var query = _fogQuery.In(entityManager);
            if (!hasView || !view.Active || !query.TryGetSingleton(out FogOfWar fog))
            {
                _layer.SetHidden(true);
                return;
            }

            _layer.SetHidden(false);
            if (view.Version == _version && _texture != null)
            {
                return;
            }

            query.CompleteDependency();
            if (_texture == null || _texture.width != fog.Size.x || _texture.height != fog.Size.y)
            {
                Object.Destroy(_texture);
                _texture = new Texture2D(fog.Size.x, fog.Size.y, TextureFormat.Alpha8, false, true)
                {
                    name = "Minimap Fog",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
                _layer.style.backgroundImage = new StyleBackground(_texture);
            }

            FogTexels.Fill(fog.Visible, fog.Explored, view.Team, ExploredAlpha, UnexploredAlpha,
                _texture.GetPixelData<byte>(0));
            _texture.Apply(false);
            _version = view.Version;
        }

        private void Release()
        {
            Object.Destroy(_texture);
            _texture = null;
            _layer.style.backgroundImage = StyleKeyword.None;
        }
    }
}
