using Unity.Entities;

namespace HyperRTS.Editor.PlayMode.Cheats
{
    /// <summary>The player picked in the cheats window, read from the authoritative world just now.</summary>
    internal readonly struct ChosenPlayer
    {
        public readonly EntityManager Manager;
        public readonly Entity Player;
        public readonly byte Owner;

        public ChosenPlayer(EntityManager manager, Entity player, byte owner)
        {
            Manager = manager;
            Player = player;
            Owner = owner;
        }
    }
}
