using HyperRTS.Editor.Generators;

namespace HyperRTS.Editor.Effects
{
    /// <summary>Rebuilds every VFX Graph rig from its <c>*.effect.json</c>.</summary>
    public sealed class EffectGraphGenerator : Generator
    {
        public override string Name => "Effect graphs";

        public override string Group => "HyperRTS";

        public override int Order => 30;

        public override string Description => "The VFX Graph rigs, compiled from their recipes.";

        public override string Sources => "Every <Graph>.effect.json";

        public override string Outputs => "The <Graph>.vfx beside each recipe";

        public override void Run() => EffectGraphBuilder.BuildAll();
    }
}
