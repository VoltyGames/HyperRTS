using HyperRTS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Hud
{
    /// <summary>Builds the in-game HUD (resources, minimap, selection, command card, banner) and keeps it in sync with ECS.</summary>
    [AddComponentMenu(HyperRTSMenu.UI + "HUD")]
    [Icon(HyperRTSIcons.UI)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PanelRenderer))]
    public class HudController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("HUD style sheet (Hud.uss).")]
        private StyleSheet styleSheet;

        private readonly HudContext _context = new();
        private readonly HudPointerTracker _pointer = new();
        private VisualElement _root;
        private ResourceBar _resources;
        private SelectionPanel _selection;
        private CommandCard _commands;
        private Minimap _minimap;
        private GameOverBanner _banner;
        private int _uiVersion = -1;

        // Register once (not in OnEnable) so reloads never duplicate the HUD.
        private void Awake()
        {
            GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDestroy()
        {
            if (TryGetComponent<PanelRenderer>(out var panelRenderer))
            {
                panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            }
        }

        private void OnUIReload(PanelRenderer panelRenderer, VisualElement root, int version)
        {
            if (root == null || version == _uiVersion)
            {
                return;
            }

            _uiVersion = version;
            _root?.RemoveFromHierarchy();
            _root = Build();
            root.Add(_root);
        }

        private VisualElement Build()
        {
            var hud = HudElements.Box("hud-root");
            hud.pickingMode = PickingMode.Ignore;
            if (styleSheet != null)
            {
                hud.styleSheets.Add(styleSheet);
            }

            _resources = new ResourceBar();
            _selection = new SelectionPanel();
            _commands = new CommandCard();
            _minimap = new Minimap();
            _banner = new GameOverBanner();

            var bottom = HudElements.Box("hud-bottom", hud);
            bottom.pickingMode = PickingMode.Ignore;
            bottom.Add(_minimap.Root);
            bottom.Add(_selection.Root);
            bottom.Add(_commands.Root);
            hud.Add(_resources.Root);
            hud.Add(_banner.Root);

            _pointer.Clear();
            _pointer.Track(_resources.Root);
            _pointer.Track(_minimap.Root);
            _pointer.Track(_selection.Root);
            _pointer.Track(_commands.Root);
            return hud;
        }

        private void Update()
        {
            if (_root == null)
            {
                return;
            }

            var ready = _context.Refresh();
            HudElements.SetVisible(_root, ready);
            if (!ready)
            {
                return;
            }

            _resources.Refresh(_context);
            _selection.Refresh(_context);
            _commands.Refresh(_context);
            _minimap.Refresh(_context);
            _banner.Refresh(_context);
            _context.SetPointerOverUI(_pointer.IsOverUI());
        }
    }
}
