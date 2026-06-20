using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Core.Selection
{
    /// <summary>Placeholder UI Toolkit marquee drawn from <see cref="SelectionInputSystem"/>'s drag state.</summary>
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

        // PanelRenderer (Unity 6.5+) doesn't expose rootVisualElement; the root arrives via the reload callback.
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

        // Fires when the UI is (re)loaded; may fire twice on first load, so rebuild idempotently.
        private void OnUIReload(PanelRenderer panelRenderer, VisualElement root)
        {
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

            if (!SelectionInputSystem.IsDragging)
            {
                _marquee.style.display = DisplayStyle.None;
                return;
            }

            var rect = ScreenToPanelRect(SelectionInputSystem.DragStartScreen, SelectionInputSystem.DragCurrentScreen);
            _marquee.style.display = DisplayStyle.Flex;
            _marquee.style.left = rect.x;
            _marquee.style.top = rect.y;
            _marquee.style.width = rect.width;
            _marquee.style.height = rect.height;
        }

        // Bottom-left screen points -> top-left panel rect (assumes PanelSettings ConstantPixelSize, scale 1).
        private static Rect ScreenToPanelRect(Vector2 a, Vector2 b)
        {
            var xMin = Mathf.Min(a.x, b.x);
            var xMax = Mathf.Max(a.x, b.x);
            var yMinScreen = Mathf.Min(a.y, b.y);
            var yMaxScreen = Mathf.Max(a.y, b.y);
            return new Rect(xMin, Screen.height - yMaxScreen, xMax - xMin, yMaxScreen - yMinScreen);
        }
    }
}
