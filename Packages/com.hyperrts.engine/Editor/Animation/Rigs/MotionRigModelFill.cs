using HyperRTS.Editor.Authoring.ModelFill;
using HyperRTS.Presentation.Animation.Rigs;

namespace HyperRTS.Editor.Animation.Rigs;

/// <summary>A Motion Rig's slots and its weapons' pivots from the parts the art names.</summary>
internal sealed class MotionRigModelFill : ModelFill<MotionRigAuthoring>
{
    protected override void Fill(MotionRigAuthoring rig) => new MotionRigFill(rig).Run();
}
