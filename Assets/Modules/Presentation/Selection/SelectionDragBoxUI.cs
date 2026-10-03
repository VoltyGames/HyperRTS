using HyperRTS.Simulation.Selection;
using HyperRTS.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Selection
{
    /// <summary>Placeholder UI Toolkit marquee drawn from the <see cref="SelectionDragState"/> singleton.</summary>
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

        // PanelRenderer doesn't expose rootVisualElement; the root arrives via the reload callback.
        // Register once here, not in OnEnable, to avoid creating duplicate code-generated elements.
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

        // Fires on first load and on every asset change; the version skips redundant reloads.
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
            if (_marquee == null)
            {
                return;
            }

            if (!TryGetDragState(out var drag) || !drag.IsDragging)
            {
                _marquee.style.display = DisplayStyle.None;
                return;
            }

            var rect = ScreenToPanelRect(drag.StartScreen, drag.CurrentScreen);
            _marquee.style.display = DisplayStyle.Flex;
            _marquee.style.left = rect.x;
            _marquee.style.top = rect.y;
            _marquee.style.width = rect.width;
            _marquee.style.height = rect.height;
        }

        // Reads the SelectionDragState singleton published by the input layer. False until a
        // simulation world with the singleton exists (before play, or on a server with no input).
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

        // Bottom-left screen points -> top-left panel rect (assumes PanelSettings ConstantPixelSize, scale 1).
        private static Rect ScreenToPanelRect(float2 a, float2 b)
        {
            var xMin = Mathf.Min(a.x, b.x);
            var xMax = Mathf.Max(a.x, b.x);
            var yMinScreen = Mathf.Min(a.y, b.y);
            var yMaxScreen = Mathf.Max(a.y, b.y);
            return new Rect(xMin, Screen.height - yMaxScreen, xMax - xMin, yMaxScreen - yMinScreen);
        }
    }
}
