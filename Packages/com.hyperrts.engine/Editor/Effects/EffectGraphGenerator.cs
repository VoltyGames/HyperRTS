using HyperRTS.Editor.Generators;

namespace HyperRTS.Editor.Effects
{
    /// <summary>Rebuilds every VFX Graph rig from its <c>*.effect.json</c>.</summary>
    public sealed class EffectGraphGenerator : Generator
    {
        public override string Name => "Effect graphs";

        public override string Group => "HyperRTS";

        public override int Order => 30;

        public override string Description => "Each <Graph>.vfx from the <Graph>.effect.json beside it.";

        public override void Run() => EffectGraphBuilder.BuildAll();
    }
}
