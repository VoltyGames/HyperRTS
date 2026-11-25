using HyperRTS.Editor.Authoring.ModelFill;
using HyperRTS.Simulation.Destruction;

namespace HyperRTS.Editor.Destruction
{
    /// <summary>A building's sections and break clips from the sections kept inside its model.</summary>
    internal sealed class SectionsModelFill : ModelFill<SectionsAuthoring>
    {
        protected override void Fill(SectionsAuthoring sections) => new SectionsFill(sections).FromModel();
    }
}
