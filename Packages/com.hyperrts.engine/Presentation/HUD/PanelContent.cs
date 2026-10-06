using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Adds one element tree to the <see cref="PanelRenderer"/> and rebuilds it whenever the panel reloads.</summary>
    [RequireComponent(typeof(PanelRenderer))]
    public abstract class PanelContent : MonoBehaviour
    {
        private int _uiVersion = -1;

        /// <summary>The built tree; null until the panel first loads.</summary>
        protected VisualElement Content { get; private set; }

        protected abstract VisualElement Build();

        // Register once (not in OnEnable) so reloads never duplicate the content.
        protected virtual void Awake()
        {
            GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
        }

        protected virtual void OnDestroy()
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
            Content?.RemoveFromHierarchy();
            Content = Build();
            root.Add(Content);
        }
    }
}
