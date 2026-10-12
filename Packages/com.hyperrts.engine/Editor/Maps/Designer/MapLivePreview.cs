using System;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Generation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// Previews the Map Editor's document as it is in memory, unsaved edits included. With <see cref="Auto"/> on, an
    /// edit starts a preview once it is committed (the pointer is up) and nothing changed for a moment. The checks,
    /// measuring and drawing run on the main thread, the planning on a worker (<see cref="MapPreview.RunAsync"/>).
    /// </summary>
    public sealed class MapLivePreview
    {
        private const string AutoPref = "HyperRTS.MapEditor.AutoPreview";
        private const long DebounceMilliseconds = 750;

        private readonly MapSpecDocument _document;
        private readonly IVisualElementScheduledItem _due;
        private string _previewedJson;
        private bool _pointerHeld;
        private bool _again;
        private bool _disposed;

        /// <param name="host">An element of the window: the debounce runs on its scheduler.</param>
        /// <param name="editor">Where edits are made by pointer (the canvas): no preview while a drag is held.</param>
        public MapLivePreview(MapSpecDocument document, VisualElement host, VisualElement editor)
        {
            _document = document;
            _due = host.schedule.Execute(OnDue);
            _due.Pause();
            editor.RegisterCallback<PointerDownEvent>(_ => _pointerHeld = true, TrickleDown.TrickleDown);
            editor.RegisterCallback<PointerUpEvent>(_ => OnPointerUp(), TrickleDown.TrickleDown);
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
            _due.Pause();
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
            _due.Pause();
            _document.Changed -= OnDocumentChanged;
        }

        private void OnDocumentChanged()
        {
            if (Auto && MapSpecJson.Write(_document.spec) != _previewedJson)
            {
                _due.ExecuteLater(DebounceMilliseconds);
            }
        }

        private void OnPointerUp()
        {
            _pointerHeld = false;
            OnDocumentChanged();
        }

        private void OnDue()
        {
            _due.Pause();
            if (!_pointerHeld)
            {
                Run();
            }
        }

        private async void Start()
        {
            SetBusy(true);
            _previewedJson = MapSpecJson.Write(_document.spec);
            try
            {
                var project = MapProject.FromSpec(_document.spec, _document.Path);
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
