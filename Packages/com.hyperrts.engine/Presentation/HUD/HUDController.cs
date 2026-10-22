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
    [AddComponentMenu(HyperRTSMenu.Rig + "HUD")]
    [Icon(HyperRTSIcons.UI)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public class HUDController : PanelContent
    {
        [SerializeField]
        [Tooltip("HUD tree, panel shells and row templates (HUDPanelLayouts).")]
        private HUDPanelLayouts panelLayouts;

        [SerializeField]
        [Tooltip("HUD layout sheet (HUD.uss): positions, sizes and spacing.")]
        private StyleSheet styleSheet;

        [SerializeField]
        [Tooltip("HUD theme sheet (HUDTheme.uss): colours, fonts and borders. A game swaps in its own.")]
        private StyleSheet themeSheet;

        private readonly HUDContext _context = new();
        private readonly HUDPointerTracker _pointer = new();
        private readonly List<HUDPanel> _panels = new();
        private string _theme;

        /// <summary>
        /// A class on <c>hud-root</c> that restyles the whole HUD (for example a theme for the local player's side);
        /// null for none. Kept when the panel rebuilds.
        /// </summary>
        public string Theme
        {
            get => _theme;
            set
            {
                if (_theme != null)
                {
                    Content?.RemoveFromClassList(_theme);
                }

                _theme = value;
                if (_theme != null)
                {
                    Content?.AddToClassList(_theme);
                }
            }
        }

        public HUDPanelLayouts PanelLayouts => panelLayouts;

        /// <summary>Adds the panels to <paramref name="hud"/>, the HUD.uxml tree, or to its named slots.</summary>
        protected virtual void CreatePanels(VisualElement hud)
        {
            var bottom = hud.Require<VisualElement>("bottom");
            Add(new Minimap(panelLayouts), bottom);
            Add(new SelectionPanel(panelLayouts), bottom);
            Add(new CommandCard(panelLayouts), bottom);
            Add(new ResourceBar(panelLayouts), hud);
            Add(new GameOverBanner(panelLayouts), hud);
            Add(new LineCost(hud.Require<CostRow>("line-cost")), hud);
        }

        protected void Add(HUDPanel panel, VisualElement parent)
        {
            parent.Add(panel.Root);
            _panels.Add(panel);
            if (panel.BlocksPointer)
            {
                _pointer.Track(panel.Root);
            }
        }

        /// <summary>Styles a sibling of <c>hud-root</c> on the same panel with the HUD's layout and theme sheets.</summary>
        public void AddSheets(VisualElement element)
        {
            // The theme goes second so its rules win over layout ones of equal specificity.
            element.styleSheets.Add(styleSheet);
            element.styleSheets.Add(themeSheet);
        }

        protected override VisualElement Build()
        {
            var hud = panelLayouts.Root.CloneRoot();
            if (_theme != null)
            {
                hud.AddToClassList(_theme);
            }

            AddSheets(hud);
            _panels.Clear();
            _pointer.Clear();
            CreatePanels(hud);
            return hud;
        }

        private void Update()
        {
            if (Content == null)
            {
                return;
            }

            var ready = MatchView.TryGetDefault(out var view) && _context.Refresh(view);
            Content.SetHidden(!ready);
            if (!ready)
            {
                return;
            }

            foreach (var panel in _panels)
            {
                // Observers keep only the panels that need no player; the rest manage their own visibility.
                if (panel.NeedsPlayer)
                {
                    panel.Root.SetHidden(!view.HasLocalPlayer);
                    if (!view.HasLocalPlayer)
                    {
                        continue;
                    }
                }

                panel.Refresh(_context);
            }

            _context.SetPointerOverUI(_pointer.IsOverUI());
        }
    }
}
