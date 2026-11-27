using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Destruction;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction;

/// <summary>One building's data for a <see cref="SectionViewSystem"/> frame, so its steps take one argument.</summary>
internal struct SectionViewContext
{
    public Entity Entity;
    public DynamicBuffer<Section> Sections;
    public DynamicBuffer<SectionModel> Models;
    public SectionHoles Holes;
    public DynamicBuffer<SectionLook> Looks;
    public DynamicBuffer<SectionPart> Carried;
    public LocalToWorld Place;
    public float Now;
    public ModelCopies Copies;
}
