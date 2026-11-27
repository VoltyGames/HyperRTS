using Unity.Entities;

namespace HyperRTS.Simulation.Abilities;

/// <summary>What a strike ability launches, which picks the component that describes it.</summary>
public enum StrikeKind : byte
{
    /// <summary>A bomber flies in and drops bombs (<c>BomberStrike</c>).</summary>
    Bomber,

    /// <summary>A missile climbs and lands (<c>MissileStrike</c>).</summary>
    Missile,
}

/// <summary>
/// One of a structure's abilities that is a strike. Casting it is refused while the match switches strategic
/// assets off.
/// </summary>
[InternalBufferCapacity(2)]
public struct StrikeAbility : IBufferElementData
{
    public int AbilityId;

    /// <summary>Metres from the aim point the strike can harm, which the AI keeps its own structures out of.</summary>
    public float Reach;

    public StrikeKind Kind;

    /// <summary>Index of the entry for <paramref name="abilityId"/>, or -1.</summary>
    public static int Find(DynamicBuffer<StrikeAbility> strikes, int abilityId)
    {
        for (var i = 0; i < strikes.Length; i++)
        {
            if (strikes[i].AbilityId == abilityId)
            {
                return i;
            }
        }

        return -1;
    }
}
