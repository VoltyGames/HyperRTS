using System.Linq;
using HyperRTS.Editor.Effects;
using Unity.Pipeline.Commands;

namespace HyperRTS.Editor.Commands
{
    /// <summary>The effect graph builder as a pipeline command.</summary>
    public static class EffectCommands
    {
        [CliCommand("effect_build", "Build one effect graph from its .effect.json, or every graph without a spec (run detached): each graph with its problem and shader errors.", Tags = new[] { "hyperrts/effects" })]
        public static object Build([CliArg("spec", "A graph name (FX_Smoke_01) or its .effect.json path; empty builds every graph")] string spec = "")
        {
            var specs = string.IsNullOrEmpty(spec) ? EffectGraphBuilder.SpecPaths().ToList() : new() { EffectGraphBuilder.FindSpec(spec) };
            specs.ForEach(EffectGraphBuilder.Build);

            return specs.Select(path => new
            {
                spec = path,
                graph = EffectGraphSpec.GraphPath(path),
                problem = EffectGraphBuilder.Problem(path),
                shaderErrors = EffectGraphBuilder.ShaderErrors(EffectGraphSpec.GraphPath(path)).ToList(),
            }).ToList();
        }
    }
}
