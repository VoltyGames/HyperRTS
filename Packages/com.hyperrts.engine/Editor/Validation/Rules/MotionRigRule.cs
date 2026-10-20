using HyperRTS.Presentation.Animation;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Motion rig slots name parts under the rig and weapon poses the unit has.</summary>
    public sealed class MotionRigRule : AuthoringRule<MotionRigAuthoring>
    {
        protected override void Check(MotionRigAuthoring rig, ValidationIssues issues)
        {
            foreach (var problem in rig.Problems())
            {
                issues.Error(rig, problem);
            }
        }
    }
}
