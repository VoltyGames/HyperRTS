using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Core.Selection
{
    /// <summary>Placeholder UI Toolkit marquee drawn from <see cref="SelectionInputSystem"/>'s drag state.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class SelectionDragBoxUI : MonoBehaviour
    {
        [SerializeField]
        private Color fillColor = new(0.3f, 0.7f, 1f, 0.15f);

        [SerializeField]
        private Color borderColor = new(0.3f, 0.7f, 1f, 0.8f);

        [SerializeField]
        private float borderThickness = 1f;

        private VisualElement _marquee;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null)
            {
                Debug.LogWarning($"{nameof(SelectionDragBoxUI)}: the UIDocument has no PanelSettings assigned.", this);
                return;
            }

            _marquee = new VisualElement { pickingMode = PickingMode.Ignore };
            var style = _marquee.style;
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
            root.Add(_marquee);
        }

        private void OnDisable()
        {
            _marquee?.RemoveFromHierarchy();
            _marquee = null;
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
