using Unity.Pipeline.Commands;
using UnityEditor;

namespace HyperRTS.Editor.Animation.Bones
{
    /// <summary>The bone animation bake as a pipeline command and a menu item.</summary>
    public static class BoneCommands
    {
        private const string Menu = "Assets/HyperRTS/Bake Bone Animation Set";

        [CliCommand("bone", "bake", "Bake one bone animation set (a name or its path), or every set when none is named: the bone texture, clip table and meshes written beside it. Returns the asset paths.", Tags = new[] { "hyperrts/animation" })]
        public static object Bake([CliArg("set", "A BoneAnimationSet name or its .asset path; empty bakes every set")] string set = "") =>
            AnimationSetBake.Bake<BoneAnimationSet>(set);

        [MenuItem(Menu)]
        private static void BakeSelected() => AnimationSetBake.BakeSelected<BoneAnimationSet>();

        [MenuItem(Menu, true)]
        private static bool CanBakeSelected() => AnimationSetBake.HasSelected<BoneAnimationSet>();
    }
}
