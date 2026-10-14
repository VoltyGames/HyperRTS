using System;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Generation;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// Previews the Map Editor's document; with <see cref="Auto"/> on, also when it opens and whenever its file
    /// changes on disk. The checks, measuring and drawing run on the main thread, the planning on a worker
    /// (<see cref="MapPreview.RunAsync"/>).
    /// </summary>
    public sealed class MapLivePreview
    {
        private const string AutoPref = "HyperRTS.MapEditor.AutoPreview";

        private readonly MapSpecDocument _document;
        private bool _again;
        private bool _disposed;

        public MapLivePreview(MapSpecDocument document)
        {
            _document = document;
            _document.Changed += OnDocumentChanged;
        }

        /// <summary>A preview finished: its report and image (null on errors), which the handler takes over.</summary>
        public event Action<MapPreviewResult> Finished;

        /// <summary>Raised when a preview starts or ends.</summary>
        public event Action BusyChanged;

        public static bool Auto
        {
            get => EditorPrefs.GetBool(AutoPref, true);
            set => EditorPrefs.SetBool(AutoPref, value);
        }

        public bool Busy { get; private set; }

        /// <summary>Previews now, whatever <see cref="Auto"/> says; queued behind one that is running.</summary>
        public void Run()
        {
            if (Busy)
            {
                _again = true;
                return;
            }

            Start();
        }

        public void Dispose()
        {
            _disposed = true;
            _document.Changed -= OnDocumentChanged;
        }

        private void OnDocumentChanged()
        {
            if (Auto)
            {
                Run();
            }
        }

        private async void Start()
        {
            SetBusy(true);
            try
            {
                var project = MapProject.FromSpec(_document.Spec, _document.Path);
                var result = await MapPreview.RunAsync(project);
                Deliver(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                SetBusy(false);
            }

            if (_again && !_disposed)
            {
                _again = false;
                Start();
            }
        }

        private void Deliver(MapPreviewResult result)
        {
            var image = result.Image;
            if (image != null)
            {
                image.hideFlags = HideFlags.HideAndDontSave;
            }

            if (_disposed)
            {
                if (image != null)
                {
                    UnityEngine.Object.DestroyImmediate(image);
                }

                return;
            }

            Finished?.Invoke(result);
        }

        private void SetBusy(bool busy)
        {
            Busy = busy;
            if (!_disposed)
            {
                BusyChanged?.Invoke();
            }
        }
    }
}
