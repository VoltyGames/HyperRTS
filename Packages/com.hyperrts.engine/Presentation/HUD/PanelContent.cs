using HyperRTS.Presentation.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Adds one element tree to the <see cref="PanelRenderer"/> and rebuilds it whenever the panel reloads.</summary>
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
