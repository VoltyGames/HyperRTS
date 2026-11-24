using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>The sections on screen; presentation keeps it, and scenery sync resets it for changes nobody saw.</summary>
    public struct ShownSections : IComponentData
    {
        /// <summary>Effect rig played at a section as it breaks, +Z pointing out of the building.</summary>
        public UnityObjectRef<GameObject> Effect;

        /// <summary>Dust wave played at the foot of a ground floor section as it breaks.</summary>
        public UnityObjectRef<GameObject> DustWave;

        /// <summary>The broken mask on screen.</summary>
        public ulong Broken;

        /// <summary>The sections whose break clip is still playing.</summary>
        public ulong Breaking;

        /// <summary>The broken sections whose pieces lie where their break clip left them, shown in place of the heap.</summary>
        public ulong Settled;

        /// <summary>False until shown, so a loaded or unseen building shows its state without effects.</summary>
        public bool Seen;
    }
}
