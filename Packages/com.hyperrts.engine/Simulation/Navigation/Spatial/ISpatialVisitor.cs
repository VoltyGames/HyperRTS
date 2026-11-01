namespace HyperRTS.Simulation.Navigation.Spatial
{
    /// <summary>Callback for <see cref="SpatialIndex.Query{T}"/>; a struct so Burst can inline it.</summary>
    public interface ISpatialVisitor
    {
        void Visit(in SpatialEntry entry);
    }
}
