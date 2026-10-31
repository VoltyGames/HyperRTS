using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>The models that show a <see cref="Section"/>, at the same index: standing, and broken.</summary>
    [InternalBufferCapacity(0)]
    public struct SectionModel : IBufferElementData
    {
        public Entity Intact;
        public Entity Broken;
    }

    /// <summary>The sections on screen; presentation keeps it, and scenery sync resets it for changes nobody saw.</summary>
    public struct ShownSections : IComponentData
    {
        /// <summary>Effect rig played at a section as it breaks, +Z pointing out of the building.</summary>
        public UnityObjectRef<GameObject> Effect;

        /// <summary>The broken mask on screen.</summary>
        public ulong Broken;

        /// <summary>False until shown, so a loaded or unseen building shows its state without effects.</summary>
        public bool Seen;
    }
}
