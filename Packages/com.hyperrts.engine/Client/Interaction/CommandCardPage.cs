using Unity.Entities;

namespace HyperRTS.Client.Interaction
{
    /// <summary>
    /// Client singleton: which command card page shows, 0 for the first and 1 for a builder's advanced options. A
    /// singleton so Escape, handled by input or a front end, can turn the card back.
    /// </summary>
    public struct CommandCardPage : IComponentData
    {
        public byte Page;
    }
}
