using Unity.Entities;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// The announcer a side's players hear, on the announcer singleton. A game may rewrite the buffer at runtime on a
    /// client, for example to switch the set to the chosen language.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AnnouncerVoice : IBufferElementData
    {
        /// <summary>The <c>PlayerSide</c> it speaks to; 0 is players without a side.</summary>
        public byte Side;

        public UnityObjectRef<AnnouncerSet> Set;
    }
}
