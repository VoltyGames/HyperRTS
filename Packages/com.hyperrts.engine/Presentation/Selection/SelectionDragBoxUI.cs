using HyperRTS.Core;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Interaction;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Selection
{
    /// <summary>Drag-box marquee drawn from <see cref="SelectionDragState"/>.</summary>
    [AddComponentMenu(HyperRTSMenu.Selection + "Selection Drag Box UI")]
    [Icon(HyperRTSIcons.Selection)]
    [HelpURL(HyperRTSDocs.Roadmap)]
    [DisallowMultipleComponent]
    public class SelectionDragBoxUI : PanelContent
    {
        [Header("Drag Box")]
        [SerializeField]
        [Tooltip("Fill colour of the drag selection box.")]
        private Color fillColor = new(0.3f, 0.7f, 1f, 0.15f);

        [SerializeField]
        [Tooltip("Border colour of the drag selection box.")]
        private Color borderColor = new(0.3f, 0.7f, 1f, 0.8f);

        [SerializeField]
        [Tooltip("Border thickness in pixels.")]
        private float borderThickness = 1f;

        private readonly LiveQuery _dragQuery = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<SelectionDragState>()));

        protected override VisualElement Build()
        {
            var marquee = new VisualElement { pickingMode = PickingMode.Ignore };
            var style = marquee.style;
            style.position = Position.Absolute;
            style.display = DisplayStyle.None;
            style.backgroundColor = fillColor;
            style.borderTopWidth = borderThickness;
            style.borderBottomWidth = borderThickness;
            style.borderLeftWidth = borderThickness;
            style.borderRightWidth = borderThickness;
            style.borderTopColor = borderColor;
            style.borderBottomColor = borderColor;
            style.borderLeftColor = borderColor;
            style.borderRightColor = borderColor;
            return marquee;
        }

        private void Update()
        {
            var marquee = Content;
            if (marquee?.panel == null)
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
