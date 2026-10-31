using System.Linq;
using System.Threading.Tasks;
using HyperRTS.Editor.Effects;
using HyperRTS.Editor.Effects.Preview;
using Unity.Pipeline.Commands;

namespace HyperRTS.Editor.Commands
{
    /// <summary>The effect graph builder and the rig capture as pipeline commands.</summary>
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

        [CliCommand("effect_capture", "Render a rig at times after its burst from a fixed camera into Logs/EffectCaptures/<Rig>/ and return the PNG paths.", Tags = new[] { "hyperrts/effects" })]
        public static async Task<object> Capture(
            [CliArg("rig", "An effect rig prefab name (FX_Explosion_Vehicle_01) or its path")] string rig,
            [CliArg("times", "Seconds after the burst, comma separated")] string times = EffectCapture.DefaultTimes,
            [CliArg("distance", "Metres from the camera to the aim point")] float distance = EffectCapture.DefaultDistance) =>
            await EffectCapture.Capture(rig, times, distance);
    }
}
