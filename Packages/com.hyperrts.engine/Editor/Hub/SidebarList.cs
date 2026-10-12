using UnityEngine.UIElements;

namespace HyperRTS.Editor.Hub
{
    /// <summary>The Tools window's sidebar: one row per page, its title and an optional badge pill.</summary>
    internal static class SidebarList
    {
        private const float RowHeight = 22f;

        /// <summary>A list over <paramref name="pages"/> that reads each row's badge from <paramref name="badges"/>.</summary>
        public static ListView Create(HubPage[] pages, string[] badges)
        {
            var list = new ListView(pages, RowHeight, MakeItem, (element, index) => Bind(element, pages[index], badges[index]))
            {
                selectionType = SelectionType.Single,
            };
            list.AddToClassList("hrts-sidebar");
            return list;
        }

        private static VisualElement MakeItem()
        {
            var item = new VisualElement();
            item.AddToClassList("hrts-sidebar__item");
            var title = new Label();
            title.AddToClassList("hrts-sidebar__title");
            item.Add(title);
            var badge = new Label();
            badge.AddToClassList("hrts-badge");
            item.Add(badge);
            return item;
        }

        private static void Bind(VisualElement item, HubPage page, string badge)
        {
            item.Q<Label>(className: "hrts-sidebar__title").text = page.Title;
            var pill = item.Q<Label>(className: "hrts-badge");
            pill.text = badge;
            pill.style.display = string.IsNullOrEmpty(badge) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
