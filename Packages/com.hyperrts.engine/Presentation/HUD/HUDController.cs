using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// Builds the in-game HUD (resources, minimap, selection, command card, banner) and keeps it in sync with ECS.
    /// Games add or replace panels by overriding <see cref="CreatePanels"/>.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.UI + "HUD")]
    [Icon(HyperRTSIcons.UI)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class HUDController : PanelContent
    {
        [SerializeField]
        [Tooltip("HUD style sheet (HUD.uss).")]
        private StyleSheet styleSheet;

        private readonly HUDContext _context = new();
        private readonly HUDPointerTracker _pointer = new();
        private readonly List<IHUDPanel> _panels = new();

        /// <summary>Adds the panels: the bottom row's go in <paramref name="bottom"/>, the rest on <paramref name="hud"/>.</summary>
        protected virtual void CreatePanels(VisualElement hud, VisualElement bottom)
        {
            Add(new Minimap(), bottom);
            Add(new SelectionPanel(), bottom);
            Add(new CommandCard(), bottom);
            Add(new ResourceBar(), hud);
            Add(new GameOverBanner(), hud);
        }

        protected void Add(IHUDPanel panel, VisualElement parent)
        {
            parent.Add(panel.Root);
            _panels.Add(panel);
            if (panel.BlocksPointer)
            {
                _pointer.Track(panel.Root);
            }
        }

        protected override VisualElement Build()
        {
            var hud = HUDElements.Box("hud-root");
            hud.pickingMode = PickingMode.Ignore;
            if (styleSheet != null)
            {
                hud.styleSheets.Add(styleSheet);
            }

            var bottom = HUDElements.Box("hud-bottom", hud);
            bottom.pickingMode = PickingMode.Ignore;
            _panels.Clear();
            _pointer.Clear();
            CreatePanels(hud, bottom);
            return hud;
        }

        private void Update()
        {
            if (Content == null)
            {
                return;
            }

            var ready = MatchView.TryGetDefault(out var view) && _context.Refresh(view);
            Content.SetVisible(ready);
            if (!ready)
            {
                return;
            }

            foreach (var panel in _panels)
            {
                panel.Refresh(_context);
            }

            _context.SetPointerOverUI(_pointer.IsOverUI());
        }
    }
}
