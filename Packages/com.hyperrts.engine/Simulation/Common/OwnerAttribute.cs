using UnityEngine;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// Shows an owner number as a dropdown of the scene Match's players, 0 being neutral; every faction up to
    /// <see cref="Faction.Max"/> when the scene has no Match.
    /// </summary>
    public sealed class OwnerAttribute : PropertyAttribute
    {
    }
}
