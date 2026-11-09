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
    [UxmlElement]
    public sealed partial class MapCanvasBackdrop : VisualElement
    {
        private const long PollMilliseconds = 500;

        private Texture2D _image;
        private string _path;
        private long _diskTicks;
        private IVisualElementScheduledItem _poll;

        public MapCanvasBackdrop()
        {
            RegisterCallback<AttachToPanelEvent>(_ => Start());
            RegisterCallback<DetachFromPanelEvent>(_ => Stop());
        }

        /// <summary>Shows an image drawn in memory; the backdrop owns it from now on.</summary>
        public void Show(Texture2D image)
        {
            Release();
            _image = image;
            style.backgroundImage = _image;
        }

        /// <summary>Shows the map's preview image from disk, and again each time a preview rewrites it.</summary>
        public void Follow(string mapName)
        {
            _path = MapReport.ImagePath(mapName + MapReport.PreviewSuffix);
            Start();
        }

        private void Start()
        {
            Stop();
            if (_path == null)
            {
                return;
            }

            LoadFromDisk();
            // Logs/ is outside the asset database, so no import event reports the preview.
            _poll = schedule.Execute(LoadFromDisk).Every(PollMilliseconds);
        }

        private void Stop()
        {
            _poll?.Pause();
            _poll = null;
            _diskTicks = 0L;
            Show(null);
        }

        private void LoadFromDisk()
        {
            var ticks = File.Exists(_path) ? File.GetLastWriteTimeUtc(_path).Ticks : 0L;
            if (ticks == _diskTicks)
            {
                return;
            }

            _diskTicks = ticks;
            Texture2D image = null;
            if (ticks != 0L)
            {
                image = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                image.LoadImage(File.ReadAllBytes(_path));
            }

            Show(image);
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
