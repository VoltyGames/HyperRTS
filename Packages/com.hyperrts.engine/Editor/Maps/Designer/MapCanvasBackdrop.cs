using System.IO;
using HyperRTS.Editor.Maps.Report;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The map canvas's background: the newest preview image, covering the playable bounds, north up. That is the
    /// one the live preview drew in memory, or the one a preview wrote to disk when that file changes afterwards.
    /// </summary>
    public sealed class MapCanvasBackdrop : VisualElement
    {
        private Texture2D _image;
        private long _diskTicks;

        public MapCanvasBackdrop()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("hrts-map-canvas__image");
            RegisterCallback<DetachFromPanelEvent>(_ => OnDetached());
        }

        /// <summary>Shows an image drawn in memory; the backdrop owns it from now on.</summary>
        public void Show(Texture2D image)
        {
            Release();
            _image = image;
            style.backgroundImage = _image;
        }

        /// <summary>Loads the map's preview image from disk when that file changed since it was last read.</summary>
        public void Poll(string mapName)
        {
            var path = MapReport.ImagePath(mapName + MapReport.PreviewSuffix);
            var ticks = File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0L;
            if (ticks == _diskTicks)
            {
                return;
            }

            _diskTicks = ticks;
            Texture2D image = null;
            if (ticks != 0L)
            {
                image = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                image.LoadImage(File.ReadAllBytes(path));
            }

            Show(image);
        }

        /// <summary>Drops the image; attached again, the next poll reloads the one on disk.</summary>
        private void OnDetached()
        {
            Release();
            _diskTicks = 0L;
        }

        private void Release()
        {
            if (_image != null)
            {
                Object.DestroyImmediate(_image);
            }

            _image = null;
        }
    }
}
