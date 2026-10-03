using Unity.Entities;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Lets double-click select every on-screen entity of one kind.</summary>
    public enum SelectableKind : byte
    {
        None = 0,
        Unit = 1,
        Building = 2,
    }

    public struct SelectableType : IComponentData
    {
        public SelectableKind Kind;
    }
}
