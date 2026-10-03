using HyperRTS.Simulation.Selection;
using HyperRTS.Core;
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
    [RequireComponent(typeof(PanelRenderer))]
    public class SelectionDragBoxUI : MonoBehaviour
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

        private VisualElement _marquee;
        private int _uiVersion = -1;

        private EntityQuery _dragQuery;
        private World _queryWorld;

        // PanelRenderer delivers its root through this callback. Register once (not in OnEnable) to avoid duplicate elements.
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

        // Fires on load and on asset changes; skip versions already built.
        private void OnUIReload(PanelRenderer panelRenderer, VisualElement root, int version)
        {
            if (root == null || version == _uiVersion)
            {
                return;
            }

            _uiVersion = version;
            _marquee?.RemoveFromHierarchy();
            _marquee = BuildMarquee();
            root.Add(_marquee);
        }

        private VisualElement BuildMarquee()
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
            if (_marquee?.panel == null)
            {
                return;
            }

            if (!TryGetDragState(out var drag) || !drag.IsDragging)
            {
                _marquee.style.display = DisplayStyle.None;
                return;
            }

            var rect = ScreenToPanelRect(_marquee.panel, drag.StartScreen, drag.CurrentScreen);
            _marquee.style.display = DisplayStyle.Flex;
            _marquee.style.left = rect.x;
            _marquee.style.top = rect.y;
            _marquee.style.width = rect.width;
            _marquee.style.height = rect.height;
        }

        // False until a world with the singleton exists (edit mode, headless server).
        private bool TryGetDragState(out SelectionDragState state)
        {
            state = default;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            if (world != _queryWorld)
            {
                _dragQuery = world.EntityManager.CreateEntityQuery(typeof(SelectionDragState));
                _queryWorld = world;
            }

            return _dragQuery.TryGetSingleton(out state);
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
