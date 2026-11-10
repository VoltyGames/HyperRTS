using HyperRTS.Presentation.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// Adds one element tree to the <see cref="PanelRenderer"/> and rebuilds it whenever the panel reloads or
    /// <see cref="Rebuild"/> asks.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public abstract class PanelContent : PresentationBehaviour
    {
        private int _uiVersion = -1;
        private VisualElement _content;

        /// <summary>
        /// The built tree; null until the panel first loads and again once a panel swap detaches it, until the reload
        /// rebuilds it.
        /// </summary>
        protected VisualElement Content => _content?.panel != null ? _content : null;

        protected abstract VisualElement Build();

        // Register once (not in OnEnable) so reloads never duplicate the content.
        protected override void OnAwake()
        {
            GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
        }

        protected override void OnDestroyed()
        {
            if (TryGetComponent<PanelRenderer>(out var panelRenderer))
            {
                panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            }
        }

        /// <summary>Builds the tree again in its place, such as for text the build wrote once; nothing before it loads.</summary>
        protected void Rebuild()
        {
            var root = Content?.parent;
            if (root == null)
            {
                return;
            }

            var index = root.IndexOf(_content);
            _content.RemoveFromHierarchy();
            _content = Build();
            root.Insert(index, _content);
        }

        // Fires on load and on asset changes; skip versions already built.
        private void OnUIReload(PanelRenderer panelRenderer, VisualElement root, int version)
        {
            if (root == null || version == _uiVersion)
            {
                return;
            }

            _uiVersion = version;
            _content?.RemoveFromHierarchy();
            _content = Build();
            root.Add(_content);
        }
    }
}
