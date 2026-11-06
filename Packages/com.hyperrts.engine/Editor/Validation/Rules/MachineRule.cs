using HyperRTS.Presentation.Animation.Rigs;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A machine eases over a positive time, so its speed never divides by zero.</summary>
    public sealed class MachineRule : AuthoringRule<MachineAuthoring>
    {
        protected override void Check(MachineAuthoring machine, ValidationIssues issues)
        {
            if (machine.rampSeconds <= 0f)
            {
                issues.Error(machine, $"'{machine.name}' eases over {machine.rampSeconds} seconds; Ramp Seconds must be above 0.");
            }
        }
    }
}
