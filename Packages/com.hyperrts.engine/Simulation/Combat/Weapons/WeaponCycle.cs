using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Weapons;

/// <summary>A weapon's firing state between shots: cooldown, the running salvo and the muzzle next in turn.</summary>
public struct WeaponCycle
{
    public float CooldownRemaining;

    /// <summary>Shots fired so far in the running salvo.</summary>
    public int SalvoShot;

    /// <summary>Where the running salvo last aimed, so its remaining shots still fly if the target is lost.</summary>
    public float3 SalvoAim;

    /// <summary>Salvos completed so far, so an order can tell its own salvo has finished.</summary>
    public uint SalvosFired;

    public byte NextMuzzle;

    public readonly bool CoolingDown => CooldownRemaining > 0f;

    public void Tick(float deltaTime) => CooldownRemaining = math.max(0f, CooldownRemaining - deltaTime);

    /// <summary>Counts a shot: the next shot of a salvo waits <see cref="Weapon.SalvoInterval"/>, the last the cooldown.</summary>
    public void StartCooldown(in Weapon weapon)
    {
        SalvoShot++;
        if (SalvoShot < weapon.SalvoSize)
        {
            CooldownRemaining = weapon.SalvoInterval;
            return;
        }

        SalvoShot = 0;
        SalvosFired++;
        CooldownRemaining = weapon.Cooldown;
    }

    /// <summary>Drops the rest of a salvo that can't go on; the cooldown counts from its last shot.</summary>
    public void EndSalvo(in Weapon weapon)
    {
        if (SalvoShot == 0)
        {
            return;
        }

        SalvoShot = 0;
        CooldownRemaining = math.max(0f, CooldownRemaining + weapon.Cooldown - weapon.SalvoInterval);
    }

    /// <summary>The <see cref="WeaponMuzzle"/> index the next round leaves from, moving the turn on.</summary>
    public int TakeMuzzle(in Weapon weapon)
    {
        var index = weapon.FirstMuzzle + NextMuzzle;
        NextMuzzle = (byte)((NextMuzzle + 1) % weapon.MuzzleCount);
        return index;
    }
}
