using Unity.Entities;

namespace HyperRTS.Simulation.Combat.Weapons;

/// <summary>An entity's <see cref="WeaponMount"/>s, primary first, with the rules over them.</summary>
public struct WeaponMounts
{
    /// <summary>The mount that sets how the entity engages, which orders, engagement and the AI read.</summary>
    public const int PrimaryIndex = 0;

    private DynamicBuffer<WeaponMount> _mounts;

    public WeaponMounts(DynamicBuffer<WeaponMount> mounts) => _mounts = mounts;

    public readonly int Length => _mounts.Length;

    public readonly WeaponMount this[int index] => _mounts[index];

    public readonly bool HasPrimary => _mounts.Length > PrimaryIndex;

    public readonly WeaponMount Primary => _mounts[PrimaryIndex];

    /// <summary>The primary fires only at a ground point or a unit it is ordered onto.</summary>
    public readonly bool FiresOnOrder => HasPrimary && Primary.Weapon.Role == WeaponRole.OnOrder;

    /// <summary>The primary's weapon; false when there is none or it is an interceptor.</summary>
    public readonly bool TryGetPrimary(out Weapon weapon)
    {
        var armed = HasPrimary && Primary.Weapon.Role != WeaponRole.Interceptor;
        weapon = armed ? Primary.Weapon : default;
        return armed;
    }

    /// <summary>Reloads the rounds that refill docked, or those that refill anywhere when <paramref name="docked"/> is false.</summary>
    public void Reload(float deltaTime, bool docked)
    {
        for (var i = 0; i < _mounts.Length; i++)
        {
            var mount = _mounts[i];
            var refillsDocked = mount.Ammo.Refill == AmmoRefill.Docked;
            if (refillsDocked == docked)
            {
                mount.Ammo.Reload(deltaTime);
                _mounts[i] = mount;
            }
        }
    }

    /// <summary>Some mount of the matching refill kind has a reload to advance or reset.</summary>
    public readonly bool NeedsReload(bool docked)
    {
        foreach (var mount in _mounts)
        {
            if ((mount.Ammo.Refill == AmmoRefill.Docked) == docked && mount.Ammo.NeedsReload)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every usable weapon with limited rounds is empty, and there is at least one.</summary>
    public readonly bool IsSpent
    {
        get
        {
            var limited = false;
            foreach (var mount in _mounts)
            {
                if (!mount.IsLimited)
                {
                    continue;
                }

                if (!mount.Ammo.Empty)
                {
                    return false;
                }

                limited = true;
            }

            return limited;
        }
    }
}
