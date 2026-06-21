using Unity.Entities;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Kind of a selectable entity; lets double-click select all of one kind on screen.</summary>
    public enum SelectableKind : byte
    {
        None = 0,
        Unit = 1,
        Building = 2,
    }

    /// <summary>
    /// The type of a selectable entity.
    /// </summary>
    public struct SelectableType : IComponentData
    {
        public SelectableKind Kind;
    }
}
