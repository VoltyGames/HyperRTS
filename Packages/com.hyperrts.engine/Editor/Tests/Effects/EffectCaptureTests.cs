using System;
using System.Collections;
using System.IO;
using HyperRTS.Editor.Effects.Preview;
using HyperRTS.Presentation.Effects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.VFX;

namespace HyperRTS.Editor.Tests.Effects
{
    /// <summary>
    /// The effect capture lists its times in order, refuses a bad one, and renders a rig into one PNG per time that
    /// shows the effect over a plain ground.
    /// </summary>
    public class EffectCaptureTests
    {
        private const string Folder = "Assets/_EffectCaptureTests";
        private const string Rig = "FX_CaptureTest";
        private const string TestBurst = "Packages/com.hyperrts.engine/Presentation/Tests/Effects/TestBurst.vfx";

        [TearDown]
        public void Clean()
        {
            AssetDatabase.DeleteAsset(Folder);
            var captures = Path.Combine(EffectCapture.Folder, Rig);
            if (Directory.Exists(captures))
            {
                Directory.Delete(captures, true);
            }
        }

        [Test]
        public void Times_AreSortedAndABadOneIsRefused()
        {
            Assert.That(EffectCapture.ParseTimes("1.2, 0.1,0.3"), Is.EqualTo(new[] { 0.1f, 0.3f, 1.2f }));
            Assert.Throws<ArgumentException>(() => EffectCapture.ParseTimes("0.1,soon"));
        }

        [Test]
        public void FindRig_RefusesAPrefabWithoutARig()
        {
            AssetDatabase.CreateFolder("Assets", "_EffectCaptureTests");
            var plain = new GameObject("Plain");
            PrefabUtility.SaveAsPrefabAsset(plain, Folder + "/Plain.prefab");
            UnityEngine.Object.DestroyImmediate(plain);

            Assert.Throws<ArgumentException>(() => EffectCapture.FindRig("Plain"));
        }

        // Graphs run once per editor frame, and a background Editor gives few.
        [UnityTest]
        [Timeout(900000)]
        public IEnumerator Capture_WritesOnePngPerTimeShowingTheEffect()
        {
            MakeRig();
            var capture = EffectCapture.Capture(Rig, "0.1,0.5", 12f);
            while (!capture.IsCompleted)
            {
                yield return null;
            }

            var files = capture.GetAwaiter().GetResult();
            Assert.That(files.Count, Is.EqualTo(2));
            foreach (var file in files)
            {
                Assert.That(File.Exists(file), file);
                Assert.That(HasEffect(file), $"{file} shows nothing but the ground");
            }
        }

        private static void MakeRig()
        {
            AssetDatabase.CreateFolder("Assets", "_EffectCaptureTests");
            var root = new GameObject(Rig);
            root.AddComponent<EffectRig>();
            var graph = new GameObject("Graph");
            graph.transform.SetParent(root.transform);
            var effect = graph.AddComponent<VisualEffect>();
            effect.initialEventName = "";
            effect.visualEffectAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(TestBurst);
            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{Rig}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        // Some pixel far from the corner's ground colour.
        private static bool HasEffect(string file)
        {
            var picture = new Texture2D(2, 2);
            try
            {
                picture.LoadImage(File.ReadAllBytes(file));
                var ground = picture.GetPixel(0, 0);
                foreach (var pixel in picture.GetPixels32())
                {
                    var difference = Mathf.Abs(pixel.r - ground.r * 255f) + Mathf.Abs(pixel.g - ground.g * 255f) + Mathf.Abs(pixel.b - ground.b * 255f);
                    if (difference > 90f)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(picture);
            }
        }
    }
}
