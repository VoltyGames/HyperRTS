using System.Linq;
using System.Threading.Tasks;
using HyperRTS.Editor.Effects.Layer;
using HyperRTS.Editor.Effects.Preview;
using Unity.Pipeline.Commands;

namespace HyperRTS.Editor.Effects.Graph;

/// <summary>The effect graph builder, recipe editing and the rig capture as pipeline commands.</summary>
public static class EffectCommands
{
    [CliCommand("effect", "build", "Build one effect graph from its .effect.asset recipe, or every graph without a recipe named: each graph with its problem and shader errors.", Tags = new[] { "hyperrts/effects" })]
    public static object Build([CliArg("recipe", "A graph name (FX_Smoke_01) or its .effect.asset path; empty builds every graph")] string recipe = "")
    {
        var recipes = string.IsNullOrEmpty(recipe) ? EffectGraphBuilder.RecipePaths().ToList() : new() { EffectGraphBuilder.FindRecipe(recipe) };
        EffectGraphBuilder.BuildAll(recipes);

        return recipes.Select(path => new
        {
            recipe = path,
            graph = EffectRecipe.GraphPath(path),
            problem = EffectGraphBuilder.Problem(path),
            shaderErrors = EffectGraphBuilder.ShaderErrors(EffectRecipe.GraphPath(path)).ToList(),
        }).ToList();
    }

    [CliCommand("effect", "show", "Print a recipe's layers, one block each, as field=value text that effect set accepts.", Tags = new[] { "hyperrts/effects" })]
    public static object Show(
        [CliArg("recipe", "A graph name (FX_Smoke_01) or its .effect.asset path")] string recipe,
        [CliArg("layer", "A layer name; empty shows every layer")] string layer = "")
    {
        var editor = new RecipeEditor(recipe);
        return editor.Layers(layer).SelectMany(found => new LayerSummary(found, editor.LayerIndex(found)).Lines()).ToList();
    }

    [CliCommand("effect", "set", "Set fields of one recipe layer, save the recipe and rebuild its graph: returns each change as layer.field: before -> after.", Tags = new[] { "hyperrts/effects" })]
    public static object Set(
        [CliArg("recipe", "A graph name (FX_Smoke_01) or its .effect.asset path")] string recipe,
        [CliArg("layer", "A layer name")] string layer,
        [CliArg("changes", "Edits joined by semicolons: field=value, or field*=factor to scale a number or range. Fields are serialized paths (size.min, flipbook.columns, sixWay.opacity); values are numbers, true or false, enum names, colours r,g,b[,a], vectors x,y,z, ranges min..max, curves t:v,t:v, bursts delay:min-max,... and textures by asset name or path")] string changes,
        [CliArg("build", "Rebuild the graph after saving; false only saves")] bool build = true)
    {
        var editor = new RecipeEditor(recipe);
        var done = editor.Set(layer, RecipeEdit.ParseAll(changes));
        editor.Save();
        if (build)
        {
            EffectGraphBuilder.Build(editor.Path);
        }

        return new { recipe = editor.Path, changes = done, built = build };
    }

    [CliCommand("effect", "capture", "Render a rig at times after its burst from a fixed camera into Logs/EffectCaptures/<Rig>/[label]/ and return the PNG paths.", Tags = new[] { "hyperrts/effects" })]
    public static async Task<object> Capture(
        [CliArg("rig", "An effect rig prefab name (FX_Explosion_Vehicle_01) or its path")] string rig,
        [CliArg("times", "Seconds after the burst, comma separated")] string times = EffectCaptureSettings.DefaultTimes,
        [CliArg("distance", "Metres from the camera to the aim point")] float distance = EffectCaptureSettings.DefaultDistance,
        [CliArg("height", "Metres above the ground the camera aims at; raise it for a tall column")] float height = EffectCaptureSettings.DefaultHeight,
        [CliArg("label", "A subfolder name, so a later capture keeps this one's files")] string label = "",
        [CliArg("pitch", "Degrees the camera looks down; 0 keeps the default camera's closest-zoom pitch")] float pitch = 0f) =>
        await new EffectCapture(rig, new EffectCaptureSettings
        {
            Times = times,
            Distance = distance,
            Height = height,
            Label = label,
            Pitch = pitch > 0f ? pitch : EffectCaptureSettings.DefaultPitch,
        }).Render();
}
