using Unity.Pipeline.Commands;

namespace HyperRTS.Editor.Animation.Rigid
{
    /// <summary>The rigid animation bake as a pipeline command.</summary>
    public static class RigidCommands
    {
        [CliCommand("rigid", "bake", "Bake one rigid animation set (a name or its path), or every set when none is named: the position and rotation textures and the clip table. Returns the asset paths.", Tags = new[] { "hyperrts/animation" })]
        public static object Bake([CliArg("set", "A RigidAnimationSet name or its .asset path; empty bakes every set")] string set = "") =>
            AnimationSetBake.Bake<RigidAnimationSet>(set);
    }
}
