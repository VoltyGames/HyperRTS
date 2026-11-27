using Unity.Entities;

namespace HyperRTS.Editor.PlayMode.Cheats;

/// <summary>The player picked in the cheats window, read from the authoritative world just now.</summary>
internal readonly record struct ChosenPlayer(EntityManager Manager, Entity Player, byte Owner);
