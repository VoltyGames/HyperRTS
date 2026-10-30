using HyperRTS.Presentation.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace HyperRTS.Presentation.Tests.Effects
{
    /// <summary>
    /// Tiny effect rigs built in code, with graphs playing the test graphs: one bursts 10 particles times Density, the
    /// other streams 10 a second plus 1 a metre.
    /// </summary>
    public static class EffectRigKit
    {
        public const string TestGraph = "Packages/com.hyperrts.engine/Presentation/Tests/Effects/TestBurst.vfx";
        public const string TestStreamGraph = "Packages/com.hyperrts.engine/Presentation/Tests/Effects/TestStream.vfx";

        public static EffectRig Rig(string name) => new GameObject(name).AddComponent<EffectRig>();

        public static VisualEffect AddGraph(EffectRig rig, string path = TestGraph)
        {
            var child = new GameObject("Graph");
            child.transform.SetParent(rig.transform);
            var graph = child.AddComponent<VisualEffect>();
            graph.initialEventName = "";
            graph.visualEffectAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            return graph;
        }
    }
}
