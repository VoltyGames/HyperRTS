using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>One recording of the Game view in Play as a PNG sequence at a fixed frame rate, to catch glitches that last a few frames.</summary>
    public sealed class FrameCapture
    {
        public const string Root = "Logs/FrameCaptures";
        private const double TimeoutSeconds = 120.0;

        private readonly int _fps;
        private readonly int _frames;
        private readonly TaskCompletionSource<bool> _done = new();
        private int _start;
        private double _deadline;

        /// <summary>A capture of <paramref name="seconds"/> at <paramref name="fps"/> into <c>Logs/FrameCaptures/&lt;name&gt;/</c>.</summary>
        public FrameCapture(string name, float seconds, int fps)
        {
            if (seconds <= 0f || fps <= 0)
            {
                throw new ArgumentException($"HyperRTS: frame capture needs seconds and fps above zero, got {seconds} and {fps}.");
            }

            Folder = Path.Combine(Root, name).Replace('\\', '/');
            _fps = fps;
            _frames = Mathf.CeilToInt(seconds * fps);
        }

        /// <summary>Project-relative folder the PNGs land in.</summary>
        public string Folder { get; }

        /// <summary>Records into <see cref="Folder"/>, replacing an earlier capture there; returns the PNG paths.</summary>
        public async Task<string[]> Record()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("HyperRTS: frame capture needs Play mode; enter Play first.");
            }

            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, true);
            }

            var controls = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var images = ScriptableObject.CreateInstance<ImageRecorderSettings>();
            try
            {
                Configure(controls, images);
                var controller = new RecorderController(controls);
                controller.PrepareRecording();
                if (!controller.StartRecording())
                {
                    throw new InvalidOperationException("HyperRTS: the Recorder did not start; see the Console.");
                }

                _start = Time.frameCount;
                _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
                EditorApplication.update += Poll;
                await _done.Task;
                controller.StopRecording();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(images);
                UnityEngine.Object.DestroyImmediate(controls);
            }

            return Directory.GetFiles(Folder, "*.png").Select(path => path.Replace('\\', '/')).OrderBy(path => path).ToArray();
        }

        private void Configure(RecorderControllerSettings controls, ImageRecorderSettings images)
        {
            controls.SetRecordModeToManual();
            controls.FrameRate = _fps;
            controls.FrameRatePlayback = FrameRatePlayback.Constant;
            controls.CapFrameRate = true;
            controls.ExitPlayMode = false;

            images.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
            images.imageInputSettings = new GameViewInputSettings();
            images.FileNameGenerator.Root = OutputPath.Root.Project;
            images.FileNameGenerator.Leaf = Folder;
            images.FileNameGenerator.FileName = "frame_" + DefaultWildcard.Frame;
            controls.AddRecorderSettings(images);
        }

        // The Recorder sets a fixed time step, so each editor frame in Play is one captured frame.
        private void Poll()
        {
            var seen = Time.frameCount - _start;
            var done = seen >= _frames;
            var late = EditorApplication.timeSinceStartup >= _deadline;
            if (!done && !late && EditorApplication.isPlaying)
            {
                return;
            }

            EditorApplication.update -= Poll;
            if (done)
            {
                _done.SetResult(true);
                return;
            }

            _done.SetException(new TimeoutException($"HyperRTS: frame capture saw {seen} of {_frames} frames before Play ended or {TimeoutSeconds:0} s passed."));
        }
    }
}
