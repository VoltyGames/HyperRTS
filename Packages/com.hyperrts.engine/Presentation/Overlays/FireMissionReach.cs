using HyperRTS.Simulation.Combat.Weapons;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Overlays;

/// <summary>
/// What a launcher's primary weapon can hit on a fire mission: its range band, measured from the unit's centre to
/// the point as the mission order does, and the radius one salvo covers round the point.
/// </summary>
public readonly struct FireMissionReach
{
    public readonly RangeBand Band;

    /// <summary>See <see cref="Weapon.Blast"/>.</summary>
    public readonly float Blast;

    public FireMissionReach(in Weapon weapon)
    {
        Band = weapon.Band(weapon.Range);
        Blast = weapon.Blast;
    }

    /// <summary>False unless the primary weapon fires only on order.</summary>
    public static bool TryRead(DynamicBuffer<WeaponMount> mounts, out FireMissionReach reach)
    {
        var weapons = new WeaponMounts(mounts);
        reach = weapons.FiresOnOrder ? new FireMissionReach(weapons.Primary.Weapon) : default;
        return weapons.FiresOnOrder;
    }

    public bool Covers(float3 from, float3 point) => Band.Contains(Weapon.PointGap(from, point));
}
