using Unity.Entities;

namespace HyperRTS.Core.Selection
{
    /// <summary>
    /// Selection state. Enableable, so selecting/deselecting toggles the enabled bit rather than causing
    /// a structural change; query the live selection with <c>WithAll&lt;Selected&gt;()</c>.
    /// </summary>
    public struct Selected : IComponentData, IEnableableComponent { }
}
