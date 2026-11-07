using HyperRTS.Presentation.HUD;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Hub
{
    /// <summary>
    /// The frame of a Tools page: an optional toolbar, a note for the empty state, a busy note and the body, which
    /// scrolls unless the page brings its own list.
    /// </summary>
    [UxmlElement]
    public sealed partial class PageShell : VisualElement
    {
        private readonly VisualElement _toolbar = new();
        private readonly HelpBox _empty = new() { messageType = HelpBoxMessageType.Info };
        private readonly Label _busy = new("Working...");
        private readonly ScrollView _scroll = new();
        private readonly VisualElement _body = new();

        public PageShell()
        {
            AddToClassList("hrts-page");
            _toolbar.AddToClassList("hrts-page__toolbar");
            _empty.AddToClassList("hrts-note");
            _empty.SetHidden(true);
            _busy.AddToClassList("hrts-page__busy");
            _busy.SetHidden(true);
            _scroll.AddToClassList("hrts-fill");
            _body.AddToClassList("hrts-page__body");
            hierarchy.Add(_toolbar);
            hierarchy.Add(_busy);
            hierarchy.Add(_empty);
            hierarchy.Add(_scroll);
            _scroll.Add(_body);
            RegisterCallback<AttachToPanelEvent>(_ => RaiseToolbar());
        }

        public override VisualElement contentContainer => _body;

        /// <summary>Off for a page whose body is a list or tree view that scrolls itself.</summary>
        [UxmlAttribute]
        public bool Scroll
        {
            // The hierarchy parent: a ScrollView reports itself as the logical parent of its content.
            get => _body.hierarchy.parent == _scroll.contentContainer;
            set
            {
                if (value == Scroll)
                {
                    return;
                }

                _scroll.SetHidden(!value);
                if (value)
                {
                    _scroll.Add(_body);
                }
                else
                {
                    hierarchy.Add(_body);
                }
            }
        }

        /// <summary>The note <see cref="ShowEmpty"/> shows.</summary>
        [UxmlAttribute]
        public string EmptyText
        {
            get => _empty.text;
            set => _empty.text = value;
        }

        public void ShowEmpty(bool empty) => _empty.SetHidden(!empty);

        /// <summary>Shows the busy note while a long task runs; the page locks what the task needs left alone.</summary>
        public void ShowBusy(bool busy) => _busy.SetHidden(!busy);

        // UXML adds every child to the body, so a Toolbar written first moves up into the toolbar slot.
        private void RaiseToolbar()
        {
            if (_body.childCount > 0 && _body[0] is Toolbar toolbar)
            {
                _toolbar.Add(toolbar);
            }
        }
    }
}
