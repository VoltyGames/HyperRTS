using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HyperRTS.Presentation.Common;
using UnityEngine;

namespace HyperRTS.Input.Cameras.CostProbe
{
    /// <summary>
    /// Run with <c>-camera-cost &lt;points file&gt; -camera-cost-report &lt;path&gt;</c>: times frames at each point and height of
    /// the <see cref="CameraCostPlan"/>, writes a <see cref="CameraCostReport"/> and quits 0, or quits 1 with the reason.
    /// </summary>
    public sealed class CameraCostProbe : PresentationBehaviour
    {
        public const string PointsFlag = "-camera-cost";
        public const string ReportFlag = "-camera-cost-report";
        public const int ErrorCode = 1;

        // Seconds after the match is live for the first frames to render at full cost, and before the run gives up.
        private const float WarmSeconds = 5f;
        private const float LiveTimeoutSeconds = 300f;

        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private CameraCostPlan _plan;
        private string _reportPath;

        /// <summary>True when a built player's command line asks for a cost run; the game reads it to start its match without a menu.</summary>
        public static bool IsRequested(string[] args) => !Application.isEditor && Array.IndexOf(args, PointsFlag) >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            var args = Environment.GetCommandLineArgs();
            if (!IsRequested(args))
            {
                return;
            }

            var probe = new GameObject(nameof(CameraCostProbe)).AddComponent<CameraCostProbe>();
            DontDestroyOnLoad(probe.gameObject);
            if (probe.TryRead(args))
            {
                probe.StartCoroutine(probe.Run());
            }
        }

        private bool TryRead(string[] args)
        {
            try
            {
                _plan = CameraCostPlan.Parse(File.ReadAllText(Flag(args, PointsFlag)));
                _reportPath = Flag(args, ReportFlag);
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or FormatException)
            {
                Fail(exception.Message);
                return false;
            }

            if (!FrameTimingManager.IsFeatureEnabled())
            {
                Fail("frame timing stats are off (Player Settings, Other Settings, Frame Timing Stats), so there are no GPU times.");
                return false;
            }

            return true;
        }

        private IEnumerator Run()
        {
            var start = Time.unscaledTime;
            CameraController controller;
            while (!IsLive(out controller))
            {
                if (Time.unscaledTime - start > LiveTimeoutSeconds)
                {
                    Fail("the match camera never came live.");
                    yield break;
                }

                yield return null;
            }

            controller.Pinned = true;
            yield return new WaitForSecondsRealtime(WarmSeconds);

            var report = new CameraCostReport();
            var gpu = new List<float>();
            var cpu = new List<float>();
            foreach (var point in _plan.points)
            {
                foreach (var height in _plan.heights)
                {
                    controller.LookAt(new Vector3(point.x, 0f, point.z), height, point.yaw);
                    yield return Measure(gpu, cpu);
                    if (gpu.Count == 0)
                    {
                        Fail($"the GPU reported no frame times at '{point.name}', {height} m.");
                        yield break;
                    }

                    report.results.Add(CameraCostResult.From(point.name, height, gpu, cpu));
                }
            }

            Finish(report);
        }

        private IEnumerator Measure(List<float> gpu, List<float> cpu)
        {
            for (var frame = 0; frame < _plan.settleFrames; frame++)
            {
                yield return null;
            }

            gpu.Clear();
            cpu.Clear();
            for (var frame = 0; frame < _plan.sampleFrames; frame++)
            {
                yield return null;
                Sample(gpu, cpu);
            }
        }

        private static bool IsLive(out CameraController controller)
        {
            controller = FindAnyObjectByType<CameraController>();
            return controller != null && MatchView.TryGetDefault(out var view) && view.HasMap;
        }

        private void Sample(List<float> gpu, List<float> cpu)
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timing) == 1 && _timing[0].gpuFrameTime > 0)
            {
                gpu.Add((float)_timing[0].gpuFrameTime);
                cpu.Add((float)_timing[0].cpuMainThreadFrameTime);
            }
        }

        private void Finish(CameraCostReport report)
        {
            try
            {
                File.WriteAllText(_reportPath, JsonUtility.ToJson(report, true));
            }
            catch (IOException exception)
            {
                Fail(exception.Message);
                return;
            }

            Debug.Log($"[HyperRTS] Camera cost report written to {_reportPath}.");
            Application.Quit(0);
        }

        private void Fail(string reason)
        {
            Debug.LogError($"[HyperRTS] Camera cost run failed: {reason}");
            Application.Quit(ErrorCode);
        }

        private static string Flag(string[] args, string flag)
        {
            var index = Array.IndexOf(args, flag);
            if (index < 0 || index + 1 >= args.Length)
            {
                throw new ArgumentException($"{flag} needs a value.");
            }

            return args[index + 1];
        }
    }
}
