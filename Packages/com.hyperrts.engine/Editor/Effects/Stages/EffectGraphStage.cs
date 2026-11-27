using System.Linq;
using System.Threading.Tasks;
using HyperRTS.Editor.Effects.Graph;
using HyperRTS.Editor.Effects.Layer;
using HyperRTS.Editor.Effects.Preview;
using HyperRTS.Editor.Stages;

namespace HyperRTS.Editor.Effects.Stages;

/// <summary>Builds the VFX graphs of a rig's recipes; Done while every graph matches its recipe.</summary>
public sealed class EffectGraphStage : EffectRigStage
{
    public EffectGraphStage()
        : base("Graph")
    {
    }

    public override int Order => GraphOrder;

    public override bool Applies(EffectRigEntry rig) => rig.Recipes.Count > 0;

    public override StageState State(EffectRigEntry rig)
    {
        if (Log(rig).Failed)
        {
            return StageState.Failed;
        }

        return rig.Recipes.Select(EffectGraphBuilder.State).Select(ToStage).Max();
    }

    public override Task Run(EffectRigEntry rig) => Record(rig, () =>
    {
        rig.Recipes.ForEach(EffectGraphBuilder.Build);
        var built = rig.Recipes.Select(recipe => $"built {EffectRecipe.GraphPath(recipe)}");
        return Task.FromResult(string.Join("\n", built));
    });

    private static StageState ToStage(EffectGraphState state) => state switch
    {
        EffectGraphState.Built => StageState.Done,
        EffectGraphState.OutOfDate => StageState.OutOfDate,
        _ => StageState.Missing,
    };
}
