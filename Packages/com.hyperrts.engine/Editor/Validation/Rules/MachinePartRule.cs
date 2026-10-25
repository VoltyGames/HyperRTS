using HyperRTS.Simulation.Animation;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A Raise or Extend part moves over a span that ends after it starts, and a Point part has an aim.</summary>
    public sealed class MachinePartRule : AuthoringRule<MachinePartAuthoring>
    {
        protected override void Check(MachinePartAuthoring part, ValidationIssues issues)
        {
            var unfolds = part.motion is MachineMotion.Raise or MachineMotion.Extend;
            if (unfolds && part.to <= part.from)
            {
                issues.Error(part, $"'{part.name}' moves over the span {part.from} to {part.to}; To must be above From.");
            }

            if (part.motion == MachineMotion.Point && part.aim == null)
            {
                issues.Error(part, $"'{part.name}' points at nothing; set its Aim.");
            }
        }
    }
}
