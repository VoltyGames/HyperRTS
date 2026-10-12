using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>The Library page's middle column: a search field over the items of one type, each with a thumbnail and key.</summary>
    public sealed class LibraryItemList : VisualElement
    {
        private const float RowHeight = 40f;
        private const long PreviewPollMilliseconds = 300;

        private readonly ToolbarSearchField _search = new();
        private readonly ListView _list;
        private readonly List<LibraryItemRow> _shown = new();
        private List<LibraryItemRow> _rows = new();

        public LibraryItemList()
        {
            AddToClassList("hrts-library-items");
            _search.AddToClassList("hrts-library-items__search");
            _search.RegisterValueChangedCallback(_ => Filter());
            Add(_search);
            _list = new ListView(_shown, RowHeight, MakeRow, (element, index) => Bind(element, _shown[index]))
            {
                selectionType = SelectionType.Single,
            };
            _list.AddToClassList("hrts-fill");
            _list.selectionChanged += picked =>
            {
                if (picked.FirstOrDefault() is LibraryItemRow row)
                {
                    Selected?.Invoke(row.Key);
                }
            };
            Add(_list);
            schedule.Execute(RefreshPreviews).Every(PreviewPollMilliseconds);
        }

        /// <summary>Raised with the key of the item the user picks.</summary>
        public event Action<string> Selected;

        /// <summary>Lists the items of <paramref name="type"/>; none when the library doesn't load.</summary>
        public void Show(MapLibrary library, Type type)
        {
            _rows = library == null
                ? new List<LibraryItemRow>()
                : library.Keys(type).Select(key => Row(type, key, library.FilePath(type, key))).ToList();
            Filter();
        }

        /// <summary>Highlights <paramref name="key"/> without raising <see cref="Selected"/>.</summary>
        public void Highlight(string key)
        {
            var index = _shown.FindIndex(row => row.Key == key);
            if (index < 0)
            {
                _list.ClearSelection();
                return;
            }

            _list.SetSelectionWithoutNotify(new[] { index });
            _list.ScrollToItem(index);
        }

        private void Filter()
        {
            var text = _search.value ?? string.Empty;
            _shown.Clear();
            _shown.AddRange(_rows.Where(row => row.Key.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0));
            _list.RefreshItems();
        }

        private void RefreshPreviews()
        {
            if (AssetPreview.IsLoadingAssetPreviews())
            {
                _list.RefreshItems();
            }
        }

        private static LibraryItemRow Row(Type type, string key, string file)
        {
            var row = new LibraryItemRow { Key = key, File = file };
            try
            {
                var (item, refs) = MapLibraryItemFile.Read(type, file);
                row.Asset = LibraryThumbnail.PreviewAsset(type, refs);
                row.Swatch = item is MapClimateLayer layer ? layer.mapColor : null;
            }
            catch (InvalidOperationException)
            {
                row.Broken = true;
            }

            return row;
        }

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("hrts-library-item");
            var thumb = new Image();
            thumb.AddToClassList("hrts-library-item__thumb");
            var swatch = new VisualElement();
            swatch.AddToClassList("hrts-library-item__swatch");
            thumb.Add(swatch);
            row.Add(thumb);
            var key = new Label();
            key.AddToClassList("hrts-library-item__key");
            row.Add(key);
            return row;
        }

        private static void Bind(VisualElement element, LibraryItemRow row)
        {
            var thumb = element.Q<Image>(className: "hrts-library-item__thumb");
            thumb.image = LibraryThumbnail.Image(row.Asset);
            var swatch = thumb.Q(className: "hrts-library-item__swatch");
            swatch.style.display = row.Swatch.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            swatch.style.backgroundColor = row.Swatch ?? default;
            var key = element.Q<Label>(className: "hrts-library-item__key");
            key.text = row.Key;
            key.EnableInClassList("hrts-library-item__key--broken", row.Broken);
        }
    }
}
