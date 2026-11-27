using Unity.Pipeline.Commands;

namespace HyperRTS.Editor.Animation.Bones;

/// <summary>The bone animation bake as a pipeline command.</summary>
public static class BoneCommands
{
    [CliCommand("bone", "bake", "Bake one bone animation set (a name or its path), or every set when none is named: the bone texture, clip table and meshes written beside it. Returns the asset paths.", Tags = new[] { "hyperrts/animation" })]
    public static object Bake([CliArg("set", "A BoneAnimationSet name or its .asset path; empty bakes every set")] string set = "") =>
        AnimationSetBake.Bake<BoneAnimationSet>(set);
}
