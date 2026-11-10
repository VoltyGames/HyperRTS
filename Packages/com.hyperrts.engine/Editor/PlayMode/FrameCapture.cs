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
    /// <summary>Records the Game view in Play as a PNG sequence at a fixed frame rate, to catch glitches that last a few frames.</summary>
    public static class FrameCapture
    {
        public const string Folder = "Work/FrameCapture";
        private const double TimeoutSeconds = 120.0;

        /// <summary>Records <paramref name="seconds"/> at <paramref name="fps"/> into <c>Work/FrameCapture/&lt;name&gt;/</c>; returns the PNG paths.</summary>
        public static async Task<string[]> Record(string name, float seconds, int fps)
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("HyperRTS: frame capture needs Play mode; enter Play first.");
            }

            if (seconds <= 0f || fps <= 0)
            {
                throw new ArgumentException($"HyperRTS: frame capture needs seconds and fps above zero, got {seconds} and {fps}.");
            }

            var folder = Path.Combine(Folder, name);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            var frames = Mathf.CeilToInt(seconds * fps);
            var controls = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var images = ScriptableObject.CreateInstance<ImageRecorderSettings>();
            try
            {
                Configure(controls, images, folder, fps);
                var controller = new RecorderController(controls);
                controller.PrepareRecording();
                if (!controller.StartRecording())
                {
                    throw new InvalidOperationException("HyperRTS: the Recorder did not start; see the Console.");
                }

                await FramesPassed(frames);
                controller.StopRecording();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(images);
                UnityEngine.Object.DestroyImmediate(controls);
            }

            return Directory.GetFiles(folder, "*.png").Select(path => path.Replace('\\', '/')).OrderBy(path => path).ToArray();
        }

        private static void Configure(RecorderControllerSettings controls, ImageRecorderSettings images, string folder, int fps)
        {
            controls.SetRecordModeToManual();
            controls.FrameRate = fps;
            controls.FrameRatePlayback = FrameRatePlayback.Constant;
            controls.CapFrameRate = true;
            controls.ExitPlayMode = false;

            images.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
            images.imageInputSettings = new GameViewInputSettings();
            images.FileNameGenerator.Root = OutputPath.Root.Project;
            images.FileNameGenerator.Leaf = folder.Replace('\\', '/');
            images.FileNameGenerator.FileName = "frame_" + DefaultWildcard.Frame;
            controls.AddRecorderSettings(images);
        }

        // The Recorder sets a fixed time step, so each editor frame in Play is one captured frame.
        private static Task FramesPassed(int frames)
        {
            var source = new TaskCompletionSource<bool>();
            var start = Time.frameCount;
            var deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            void Poll()
            {
                var done = Time.frameCount - start >= frames;
                var late = EditorApplication.timeSinceStartup >= deadline;
                if (!done && !late && EditorApplication.isPlaying)
                {
                    return;
                }

                EditorApplication.update -= Poll;
                if (done)
                {
                    source.SetResult(true);
                    return;
                }

                source.SetException(new TimeoutException($"HyperRTS: frame capture saw {Time.frameCount - start} of {frames} frames before Play ended or {TimeoutSeconds:0} s passed."));
            }

            EditorApplication.update += Poll;
            return source.Task;
        }
    }
}
