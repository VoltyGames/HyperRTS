using HyperRTS.Client.Common;
using HyperRTS.Client.Interaction;
using HyperRTS.Core;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Selection
{
    /// <summary>Drag-box marquee with corner accents, placed from <see cref="SelectionDragState"/>.</summary>
    [AddComponentMenu(HyperRTSMenu.Rig + "Selection Drag Box UI")]
    [Icon(HyperRTSIcons.Selection)]
    [HelpURL(HyperRTSDocs.Selection)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HUDController))]
    public class SelectionDragBoxUI : PanelContent
    {
        private readonly LiveQuery _dragQuery = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<SelectionDragState>()));

        private static readonly string[] Corners = { "tl", "tr", "bl", "br" };

        // The box sits beside hud-root, so it takes the HUD sheets itself and a game theme restyles it.
        protected override VisualElement Build()
        {
            var marquee = new VisualElement { pickingMode = PickingMode.Ignore };
            GetComponent<HUDController>().AddSheets(marquee);
            marquee.AddToClassList("drag-box");
            marquee.style.display = DisplayStyle.None;
            foreach (var corner in Corners)
            {
                var accent = new VisualElement { pickingMode = PickingMode.Ignore };
                accent.AddToClassList("drag-box__corner");
                accent.AddToClassList("drag-box__corner--" + corner);
                marquee.Add(accent);
            }

            return marquee;
        }

        private void Update()
        {
            var marquee = Content;
            if (marquee == null)
            {
                return;
            }

            if (!TryGetDragState(out var drag) || !drag.IsDragging)
            {
                marquee.style.display = DisplayStyle.None;
                return;
            }

            var rect = ScreenToPanelRect(marquee.panel, drag.StartScreen, drag.CurrentScreen);
            marquee.style.display = DisplayStyle.Flex;
            marquee.style.left = rect.x;
            marquee.style.top = rect.y;
            marquee.style.width = rect.width;
            marquee.style.height = rect.height;
        }

        // False until a world with the singleton exists (edit mode, headless server).
        private bool TryGetDragState(out SelectionDragState state)
        {
            state = default;
            return DefaultWorld.TryGetEntityManager(out var entityManager)
                && _dragQuery.In(entityManager).TryGetSingleton(out state);
        }

        // Input reports bottom-left screen pixels; panels are top-left and may be scaled.
        private static Rect ScreenToPanelRect(IPanel panel, float2 a, float2 b)
        {
            var min = ToPanel(panel, new Vector2(Mathf.Min(a.x, b.x), Mathf.Max(a.y, b.y)));
            var max = ToPanel(panel, new Vector2(Mathf.Max(a.x, b.x), Mathf.Min(a.y, b.y)));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Vector2 ToPanel(IPanel panel, Vector2 screen) =>
            RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
    }
}
