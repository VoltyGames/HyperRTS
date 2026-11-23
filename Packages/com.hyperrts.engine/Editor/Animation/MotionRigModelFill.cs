using HyperRTS.Editor.Authoring;
using HyperRTS.Presentation.Animation.Rigs;

namespace HyperRTS.Editor.Animation
{
    /// <summary>A Motion Rig's slots and its weapons' pivots from the parts the art names.</summary>
    internal sealed class MotionRigModelFill : ModelFill<MotionRigAuthoring>
    {
        protected override void Fill(MotionRigAuthoring rig) => new MotionRigFill(rig).Run();
    }
}
