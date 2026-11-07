using HyperRTS.Simulation.Common.Players;
using UnityEngine;

namespace HyperRTS.Simulation.Common.Authoring
{
    /// <summary>
    /// Shows an owner number as a dropdown of the scene Match's players, 0 being neutral; every owner up to
    /// <see cref="Owner.Max"/> when the scene has no Match.
    /// </summary>
    public sealed class OwnerAttribute : PropertyAttribute
    {
    }
}
