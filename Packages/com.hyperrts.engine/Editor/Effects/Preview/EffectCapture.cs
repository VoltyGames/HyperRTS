using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HyperRTS.Editor.Common;
using HyperRTS.Input.Cameras;
using HyperRTS.Presentation.Effects.Rig;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>
    /// Plays a rig above the open scene as the Effects page does and renders it at chosen times after the burst, from an
    /// RTS camera in front and one turned to the side, over a grey ground, into <c>Logs/EffectCaptures/&lt;Rig&gt;/</c>, or its label subfolder.
    /// </summary>
    public sealed class EffectCapture
    {
        public const string Root = "Logs/EffectCaptures";
        private const int Width = 960;
        private const int Height = 540;
        // The default camera's pitch at its closest zoom, where effects are seen largest.
        private static readonly float Pitch = new CameraZoom().pitchAtMin;
        // Frames for a fresh graph to run once, and for particles to draw so their shaders compile.
        private const int ReadyFrames = 2;
        private const int WarmFrames = 4;
        private const double FrameSeconds = 45.0;

        // Far above any map, so the open scene's own contents stay out of the picture.
        private static readonly Vector3 Sky = new(0f, 10000f, 0f);

        // Front, then turned a quarter about the aim, so a quad that does not face the camera shows edge-on.
        private static readonly (string Name, float Yaw)[] Views = { ("front", 0f), ("side", 90f) };

        private readonly GameObject _prefab;
        private readonly EffectCaptureSettings _settings;

        /// <summary>
        /// A capture of the rig prefab named <paramref name="rig"/> (or at that path) as <paramref name="settings"/> say.
        /// Throws naming a missing rig or a bad time.
        /// </summary>
        public EffectCapture(string rig, EffectCaptureSettings settings)
        {
            _prefab = FindRig(rig);
            _settings = settings;
            Times = ParseTimes(string.IsNullOrWhiteSpace(settings.Times) ? EffectCaptureSettings.DefaultTimes : settings.Times);
            if (Times.Count == 0)
            {
                throw new ArgumentException("HyperRTS: no capture times given.");
            }

            var label = settings.Label.Trim();
            Folder = label.Length == 0 ? $"{Root}/{_prefab.name}" : $"{Root}/{_prefab.name}/{label}";
        }

        /// <summary>Seconds after the burst, ascending.</summary>
        public IReadOnlyList<float> Times { get; }

        /// <summary>Project-relative folder the PNGs land in.</summary>
        public string Folder { get; }

        /// <summary>Renders the rig at each time and returns the PNG paths. Graphs only run on editor frames, so this awaits them.</summary>
        public async Task<List<string>> Render()
        {
            Directory.CreateDirectory(Folder);
            var set = new GameObject("HyperRTS Effect Capture") { hideFlags = HideFlags.DontSave };
            var target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            using var player = new EffectPreviewPlayer(SceneManager.GetActiveScene()) { Pivot = Sky, Paused = true };
            try
            {
                var camera = Lay(set);
                camera.targetTexture = target;
                await WarmUp(player, camera);
                player.Load(_prefab);
                await Frames(player, camera, ReadyFrames, 0f);
                player.Paused = true;
                player.Play(false);
                await Frames(player, camera, ReadyFrames, 0f);
                return await Shoot(player, camera, target);
            }
            finally
            {
                foreach (var renderer in set.GetComponentsInChildren<Renderer>())
                {
                    Object.DestroyImmediate(renderer.sharedMaterial);
                }

                Object.DestroyImmediate(set);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        // A graph drops events until it has run once and compiles its shaders on first draw, so play a throwaway copy first.
        private async Task WarmUp(EffectPreviewPlayer player, Camera camera)
        {
            player.Load(_prefab);
            await Frames(player, camera, ReadyFrames, 0f);
            player.Play(false);
            await Frames(player, camera, WarmFrames, EffectPreviewPlayer.StepSeconds);
        }

        private static async Task Frames(EffectPreviewPlayer player, Camera camera, int count, float deltaTime)
        {
            for (var frame = 0; frame < count; frame++)
            {
                player.Step(deltaTime);
                camera.Render();
                await NextFrame();
            }
        }

        private async Task<List<string>> Shoot(EffectPreviewPlayer player, Camera camera, RenderTexture target)
        {
            var files = new List<string>();
            var at = 0f;
            foreach (var time in Times)
            {
                if (time > at)
                {
                    player.Simulate(time - at);
                    at = time;
                }

                await NextFrame();
                player.Step(0f);
                foreach (var view in Views)
                {
                    Aim(camera, view.Yaw);
                    camera.Render();
                    var file = $"{Folder}/{_prefab.name}_{time.ToString("0.0#", CultureInfo.InvariantCulture)}_{view.Name}.png";
                    EditorTextures.WritePng(target, file);
                    files.Add(file);
                }
            }

            return files;
        }

        private static GameObject FindRig(string rig)
        {
            var path = rig.Replace('\\', '/');
            var match = AssetDatabase.FindAssets("t:Prefab " + Path.GetFileNameWithoutExtension(path))
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(candidate => candidate == path || Path.GetFileNameWithoutExtension(candidate) == path);
            var prefab = match == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(match);
            if (prefab == null || !prefab.TryGetComponent<EffectRig>(out _))
            {
                throw new ArgumentException($"HyperRTS: no effect rig prefab is named '{rig}'.");
            }

            return prefab;
        }

        private static List<float> ParseTimes(string times)
        {
            var parsed = new List<float>();
            foreach (var entry in times.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var text = entry.Trim();
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || seconds < 0f)
                {
                    throw new ArgumentException($"HyperRTS: '{text}' is not a time in seconds.");
                }

                parsed.Add(seconds);
            }

            parsed.Sort();
            return parsed;
        }

        private static Task NextFrame()
        {
            var source = new TaskCompletionSource<bool>();
            var start = Time.frameCount;
            var deadline = EditorApplication.timeSinceStartup + FrameSeconds;
            void Poll()
            {
                if (Time.frameCount == start && EditorApplication.timeSinceStartup < deadline)
                {
                    // An Edit-mode frame runs only when asked for, and one request can pass before this waits on it.
                    EditorApplication.QueuePlayerLoopUpdate();
                    return;
                }

                EditorApplication.update -= Poll;
                if (Time.frameCount == start)
                {
                    source.SetException(new TimeoutException(
                        $"HyperRTS: the Editor ran no frame in {FrameSeconds:0} s, as when it is in the background; bring it forward and capture again."));
                }
                else
                {
                    source.SetResult(true);
                }
            }

            EditorApplication.update += Poll;
            EditorApplication.QueuePlayerLoopUpdate();
            return source.Task;
        }

        // Camera, sun and ground under one parent.
        private Camera Lay(GameObject set)
        {
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(set.transform);
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
            camera.fieldOfView = 35f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 500f;
            Aim(camera, Views[0].Yaw);

            var sun = new GameObject("Sun");
            sun.transform.SetParent(set.transform);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.AddComponent<Light>().type = LightType.Directional;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.SetParent(set.transform);
            ground.transform.position = Sky;
            ground.transform.localScale = Vector3.one * 20f;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.32f, 0.32f, 0.3f) };
            ground.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            return camera;
        }

        private void Aim(Camera camera, float yaw)
        {
            var rotation = Quaternion.Euler(Pitch, yaw, 0f);
            var aim = Sky + new Vector3(0f, _settings.Height, 0f);
            camera.transform.SetPositionAndRotation(aim - rotation * Vector3.forward * _settings.Distance, rotation);
        }
    }
}
