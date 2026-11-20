using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Capture
{
    /// <summary>
    /// One recording of the Game view in Play at a fixed frame rate: a PNG sequence to catch glitches that last a few
    /// frames, or a ProRes movie with the game's sound, a master to edit and encode for sharing.
    /// </summary>
    public sealed class FrameCapture
    {
        public const string Root = "Logs/FrameCaptures";

        // A minute to start, then a second a frame, as a 4K frame of a busy scene can take that long.
        private const double StartSeconds = 60.0;
        private const double FrameSeconds = 1.0;
        private const string MovieExtension = ".mov";

        private readonly int _fps;
        private readonly int _frames;
        private readonly TaskCompletionSource<bool> _done = new();
        private int _start;
        private double _deadline;

        /// <summary>A capture of <paramref name="seconds"/> at <paramref name="fps"/> into the project-relative <paramref name="folder"/>.</summary>
        public FrameCapture(string folder, float seconds, int fps)
        {
            if (seconds <= 0f || fps <= 0)
            {
                throw new ArgumentException($"HyperRTS: frame capture needs seconds and fps above zero, got {seconds} and {fps}.");
            }

            Folder = folder.Replace('\\', '/');
            _fps = fps;
            _frames = Mathf.CeilToInt(seconds * fps);
        }

        /// <summary>Project-relative folder the PNGs land in; a movie is this path with <c>.mov</c> added.</summary>
        public string Folder { get; }

        /// <summary>The Game view size to record at, in pixels; zero keeps the Game view's own.</summary>
        public Vector2Int Size { get; set; }

        /// <summary>Records one ProRes 422 HQ movie with the game's sound instead of PNG frames.</summary>
        public bool Movie { get; set; }

        /// <summary>Records into <see cref="Folder"/>, replacing an earlier capture there; returns the PNG paths, or the movie's.</summary>
        public async Task<string[]> Record()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("HyperRTS: frame capture needs Play mode; enter Play first.");
            }

            Clear();
            var controls = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            RecorderSettings output = Movie ? MovieOutput() : ImageOutput();
            try
            {
                Configure(controls, output);
                await Run(controls);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(output);
                UnityEngine.Object.DestroyImmediate(controls);
            }

            if (Movie)
            {
                return new[] { Folder + MovieExtension };
            }

            return Directory.GetFiles(Folder, "*.png").Select(path => path.Replace('\\', '/')).OrderBy(path => path).ToArray();
        }

        private void Clear()
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, true);
            }

            File.Delete(Folder + MovieExtension);
        }

        private async Task Run(RecorderControllerSettings controls)
        {
            var controller = new RecorderController(controls);
            controller.PrepareRecording();
            if (!controller.StartRecording())
            {
                throw new InvalidOperationException("HyperRTS: the Recorder did not start; see the Console.");
            }

            _start = Time.frameCount;
            _deadline = EditorApplication.timeSinceStartup + StartSeconds + _frames * FrameSeconds;
            EditorApplication.update += Poll;
            await _done.Task;
            controller.StopRecording();
        }

        private void Configure(RecorderControllerSettings controls, RecorderSettings output)
        {
            controls.SetRecordModeToManual();
            controls.FrameRate = _fps;
            controls.FrameRatePlayback = FrameRatePlayback.Constant;
            controls.CapFrameRate = true;
            controls.ExitPlayMode = false;
            output.FileNameGenerator.Root = OutputPath.Root.Project;
            controls.AddRecorderSettings(output);
        }

        private ImageRecorderSettings ImageOutput()
        {
            var images = ScriptableObject.CreateInstance<ImageRecorderSettings>();
            images.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
            images.imageInputSettings = GameView();
            images.FileNameGenerator.Leaf = Folder;
            images.FileNameGenerator.FileName = "frame_" + DefaultWildcard.Frame;
            return images;
        }

        // The Recorder renders the sound offline beside each frame, so it stays in step however slowly frames come.
        // Its H.264 tops out at a soft 8 Mbps baseline stream, so movies are ProRes masters, encoded later for sharing.
        private MovieRecorderSettings MovieOutput()
        {
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.EncoderSettings = new ProResEncoderSettings { Format = ProResEncoderSettings.OutputFormat.ProRes422HQ };
            movie.CaptureAudio = true;
            movie.ImageInputSettings = GameView();
            movie.FileNameGenerator.Leaf = Path.GetDirectoryName(Folder)?.Replace('\\', '/');
            movie.FileNameGenerator.FileName = Path.GetFileName(Folder);
            return movie;
        }

        private GameViewInputSettings GameView()
        {
            var view = new GameViewInputSettings();
            if (Size != Vector2Int.zero)
            {
                view.OutputWidth = Size.x;
                view.OutputHeight = Size.y;
            }

            return view;
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

            _done.SetException(new TimeoutException($"HyperRTS: frame capture saw {seen} of {_frames} frames before Play ended or its time ran out."));
        }
    }
}
