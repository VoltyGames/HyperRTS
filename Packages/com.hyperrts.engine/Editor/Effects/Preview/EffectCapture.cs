using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HyperRTS.Presentation.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>
    /// Plays a rig above the open scene as the Effect Preview does and renders it at chosen times after the burst, from a
    /// fixed RTS-like camera and sun over a grey ground, into <c>Logs/EffectCaptures/&lt;Rig&gt;/</c>.
    /// </summary>
    public static class EffectCapture
    {
        public const string Folder = "Logs/EffectCaptures";
        public const string DefaultTimes = "0.1,0.3,0.6,1.2,2.5";
        public const float DefaultDistance = 24f;

        private const int Width = 960;
        private const int Height = 540;
        private const float Pitch = 40f;
        private const float AimHeight = 2.5f;
        private const int StartFrames = 300;
        private const double FrameSeconds = 45.0;

        // Far above any map, so the open scene's own contents stay out of the picture.
        private static readonly Vector3 Sky = new(0f, 10000f, 0f);

        /// <summary>The rig prefab named <paramref name="rig"/> (or at that path); throws naming it when there is none.</summary>
        public static GameObject FindRig(string rig)
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

        /// <summary>Seconds in a list such as <c>0.1,0.3,2.5</c>, ascending; throws on a bad entry.</summary>
        public static List<float> ParseTimes(string times)
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

        /// <summary>
        /// Renders the rig at each time after its burst, <paramref name="distance"/> metres from the aim; returns the
        /// PNG paths. Graphs only run on editor frames, so this awaits them.
        /// </summary>
        public static async Task<List<string>> Capture(string rig, string times, float distance)
        {
            var prefab = FindRig(rig);
            var seconds = ParseTimes(string.IsNullOrWhiteSpace(times) ? DefaultTimes : times);
            if (seconds.Count == 0)
            {
                throw new ArgumentException("HyperRTS: no capture times given.");
            }

            var folder = Path.Combine(Folder, prefab.name);
            Directory.CreateDirectory(folder);
            var files = new List<string>();
            var set = new GameObject("HyperRTS Effect Capture") { hideFlags = HideFlags.DontSave };
            var target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            using var player = new EffectPreviewPlayer(SceneManager.GetActiveScene()) { Pivot = Sky, Paused = true };
            try
            {
                var camera = Lay(set, distance);
                camera.targetTexture = target;
                player.Load(prefab);
                await NextFrame();
                player.Play(true);

                // A graph runs once a camera sees it, and sees it once it has run, so render until it starts.
                for (var frame = 0; frame < StartFrames && player.AllCulled; frame++)
                {
                    player.Step(0f);
                    camera.Render();
                    await NextFrame();
                }

                var at = 0f;
                foreach (var time in seconds)
                {
                    if (time > at)
                    {
                        player.Simulate(time - at);
                        at = time;
                    }

                    await NextFrame();
                    player.Step(0f);
                    camera.Render();
                    var file = Path.Combine(folder, $"{prefab.name}_{time.ToString("0.0#", CultureInfo.InvariantCulture)}.png");
                    File.WriteAllBytes(file, Read(target));
                    files.Add(file.Replace('\\', '/'));
                }
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

            return files;
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
                    return;
                }

                EditorApplication.update -= Poll;
                if (Time.frameCount == start)
                {
                    source.SetException(new TimeoutException(
                        $"HyperRTS: the Editor ran no frame in {FrameSeconds:0} s, as when it is in the background; bring it forward (hardpower dev editor wake) and capture again."));
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
        private static Camera Lay(GameObject set, float distance)
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
            var aim = Sky + new Vector3(0f, AimHeight, 0f);
            cameraObject.transform.SetPositionAndRotation(aim - Quaternion.Euler(Pitch, 0f, 0f) * Vector3.forward * distance, Quaternion.Euler(Pitch, 0f, 0f));

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

        private static byte[] Read(RenderTexture target)
        {
            var previous = RenderTexture.active;
            var picture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                picture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                return picture.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(picture);
            }
        }
    }
}
