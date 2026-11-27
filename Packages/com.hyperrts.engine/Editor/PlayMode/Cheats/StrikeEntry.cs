using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Strikes;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Cheats;

/// <summary>
/// A strike the game has, found from its prefab: one strike ability of a structure, or a unit with a strategic
/// weapon. It builds the prefab and fires it for a player.
/// </summary>
internal readonly struct StrikeEntry
{
    public readonly Entity Prefab;
    public readonly int TypeId;

    /// <summary>0 for a launcher unit, which fires a fire mission instead of an ability.</summary>
    public readonly int AbilityId;

    public readonly string Name;
    public readonly string Title;

    /// <summary>The spawn grouping's heading, or null without a grouping.</summary>
    public readonly string Heading;

    private StrikeEntry(EntityManager entityManager, Entity prefab, int abilityId, ISpawnGrouping grouping)
    {
        var info = entityManager.GetComponentData<EntityInfo>(prefab);
        Prefab = prefab;
        TypeId = info.TypeId;
        AbilityId = abilityId;
        Name = info.Name.ToString();
        Title = Name;
        if (abilityId != 0)
        {
            var abilities = entityManager.GetBuffer<Ability>(prefab, true);
            Title = $"{Name}: {abilities[Ability.Find(abilities, abilityId)].Name}";
        }

        Heading = grouping?.FindHeading(Name);
    }

    public bool IsLauncher => AbilityId == 0;

    /// <summary>Every strike ability of a structure prefab and every strategic launcher unit prefab, by heading then title.</summary>
    public static List<StrikeEntry> Find(EntityManager entityManager)
    {
        var grouping = TypeDiscovery.Instances<ISpawnGrouping>().FirstOrDefault();
        var found = new List<StrikeEntry>();
        foreach (var prefab in SpawnCheat.Prefabs(entityManager))
        {
            if (entityManager.HasBuffer<StrikeAbility>(prefab))
            {
                foreach (var strike in entityManager.GetBuffer<StrikeAbility>(prefab, true))
                {
                    found.Add(new StrikeEntry(entityManager, prefab, strike.AbilityId, grouping));
                }
            }

            if (entityManager.HasComponent<UnitTag>(prefab) && Rounds(entityManager, prefab).Max > 0)
            {
                found.Add(new StrikeEntry(entityManager, prefab, 0, grouping));
            }
        }

        var headings = grouping?.Headings ?? new List<string>();
        return found.OrderBy(entry => SpawnPicker.HeadingIndex(headings, entry.Heading))
            .ThenBy(entry => entry.Title).ToList();
    }

    /// <summary>The living entity <paramref name="owner"/> owns of each prefab type.</summary>
    public static Dictionary<int, Entity> FindOwned(EntityManager entityManager, byte owner)
    {
        var owned = new Dictionary<int, Entity>();
        using var query = entityManager.CreateEntityQuery(typeof(EntityInfo), typeof(Owner));
        using var entities = query.ToEntityArray(Allocator.Temp);
        using var infos = query.ToComponentDataArray<EntityInfo>(Allocator.Temp);
        using var owners = query.ToComponentDataArray<Owner>(Allocator.Temp);
        for (var i = 0; i < entities.Length; i++)
        {
            if (owners[i].Value == owner && !entityManager.HasEnabled<Dead>(entities[i]))
            {
                owned.TryAdd(infos[i].TypeId, entities[i]);
            }
        }

        return owned;
    }

    /// <summary>Why <paramref name="instance"/> can't fire at <paramref name="point"/>, or null. Low power isn't one: Fire powers it.</summary>
    public string FindBlocker(EntityManager entityManager, Entity instance, float3 point)
    {
        if (!MatchCheats.StrategicAssetsEnabled(entityManager))
        {
            return "Strategic assets are off";
        }

        if (entityManager.HasComponent<Offline>(instance))
        {
            var offline = entityManager.GetComponentData<Offline>(instance);
            if ((offline.Reasons & OfflineReason.UnderConstruction) != 0)
            {
                return "Under construction";
            }

            if (offline.HasReasonOtherThan(OfflineReason.Unpowered))
            {
                return $"Offline: {offline.Reasons & ~OfflineReason.Unpowered}";
            }
        }

        return IsLauncher ? null : FindRangeBlocker(entityManager, instance, point);
    }

    /// <summary>The rounds as "current/max" for a launcher, otherwise "Ready" or the cooldown left; "Off" while strategic assets are.</summary>
    public string Status(EntityManager entityManager, Entity instance)
    {
        if (!MatchCheats.StrategicAssetsEnabled(entityManager))
        {
            return "Off";
        }

        if (IsLauncher)
        {
            var (current, max) = Rounds(entityManager, instance);
            return $"Rounds {current}/{max}";
        }

        var cooldown = Cooldown(entityManager, instance);
        if (cooldown.IsReady)
        {
            return "Ready";
        }

        var seconds = (int)math.ceil(cooldown.Remaining);
        return $"Cooldown {seconds / 60}:{seconds % 60:00}";
    }

    /// <summary>Strategic assets are on, and a launcher's strategic rounds are full or a strike's cooldown is over.</summary>
    public bool IsReady(EntityManager entityManager, Entity instance)
    {
        if (!MatchCheats.StrategicAssetsEnabled(entityManager))
        {
            return false;
        }

        if (IsLauncher)
        {
            var (current, max) = Rounds(entityManager, instance);
            return current == max;
        }

        return Cooldown(entityManager, instance).IsReady;
    }

    /// <summary>Spawns one finished for <paramref name="owner"/> at <paramref name="point"/>, logging why when the ground is blocked.</summary>
    public void Build(EntityManager entityManager, byte owner, float3 point)
    {
        if (SpawnCheat.SpawnAt(entityManager, Prefab, owner, point.xz, 0f) == Entity.Null)
        {
            Debug.LogWarning($"[HyperRTS] {Name} not built: the ground there is blocked.");
        }
    }

    /// <summary>Powers <paramref name="instance"/> and fills its strategic rounds or ends its strike's cooldown, without firing.</summary>
    public void Ready(EntityManager entityManager, Entity instance)
    {
        StopPowerDraw(entityManager, instance);
        if (IsLauncher)
        {
            FillRounds(entityManager, instance);
            return;
        }

        var cooldowns = entityManager.GetBuffer<AbilityCooldown>(instance);
        cooldowns.ElementAt(AbilityCooldown.Find(cooldowns, AbilityId)).Remaining = 0f;
    }

    /// <summary>
    /// Readies <paramref name="instance"/> and sends the player's command at <paramref name="point"/>, as input and
    /// AI do: a UseAbility for a strike, a fire mission for a launcher.
    /// </summary>
    public void Fire(EntityManager entityManager, Entity player, Entity instance, float3 point)
    {
        Ready(entityManager, instance);
        var command = new PlayerCommand { Position = point };
        if (IsLauncher)
        {
            command.Type = CommandType.FireMission;
        }
        else
        {
            command.Type = CommandType.UseAbility;
            command.Argument = AbilityId;
        }

        using var caster = new NativeArray<Entity>(new[] { instance }, Allocator.Temp);
        PlayerCommands.Address(ref command, entityManager.GetBuffer<PlayerCommandSubject>(player), caster);
        entityManager.GetBuffer<PlayerCommand>(player).Add(command);
    }

    private AbilityCooldown Cooldown(EntityManager entityManager, Entity instance)
    {
        var cooldowns = entityManager.GetBuffer<AbilityCooldown>(instance, true);
        return cooldowns[AbilityCooldown.Find(cooldowns, AbilityId)];
    }

    private static (int Current, int Max) Rounds(EntityManager entityManager, Entity entity)
    {
        int current = 0, max = 0;
        if (!entityManager.HasBuffer<WeaponMount>(entity))
        {
            return (0, 0);
        }

        foreach (var mount in entityManager.GetBuffer<WeaponMount>(entity, true))
        {
            if (mount.Weapon.Strategic)
            {
                current += mount.Ammo.Current;
                max += mount.Ammo.Max;
            }
        }

        return (current, max);
    }

    // An aimed ability a structure can't move for only fires in reach; the command is dropped otherwise.
    private string FindRangeBlocker(EntityManager entityManager, Entity instance, float3 point)
    {
        var abilities = entityManager.GetBuffer<Ability>(instance, true);
        var ability = abilities[Ability.Find(abilities, AbilityId)];
        if (ability.Target == AbilityTarget.None || ability.Range <= 0f)
        {
            return null;
        }

        // Measured edge to edge from the caster, as Ability.InRange does for a ground aim.
        var from = entityManager.GetComponentData<LocalTransform>(instance).Position;
        var gap = EntityRadius.EdgeDistance(from, EntityRadius.Get(entityManager, instance), point, 0f);
        return gap > ability.Range ? "Out of range of the view" : null;
    }

    // Too little power takes a consumer offline and the ability command is dropped. One that draws nothing is never
    // unpowered; the reason is cleared now since the power system only sets it.
    private static void StopPowerDraw(EntityManager entityManager, Entity instance)
    {
        var draws = entityManager.HasComponent<PowerSupply>(instance)
            && entityManager.GetComponentData<PowerSupply>(instance).Amount < 0f;
        if (!draws)
        {
            return;
        }

        entityManager.SetComponentData(instance, new PowerSupply { Amount = 0f });
        var offline = entityManager.GetComponentData<Offline>(instance);
        offline.Set(OfflineReason.Unpowered, false);
        entityManager.SetComponentData(instance, offline);
        entityManager.SetComponentEnabled<Offline>(instance, offline.Reasons != OfflineReason.None);
    }

    private static void FillRounds(EntityManager entityManager, Entity instance)
    {
        var mounts = entityManager.GetBuffer<WeaponMount>(instance);
        for (var i = 0; i < mounts.Length; i++)
        {
            if (mounts[i].Weapon.Strategic)
            {
                ref var ammo = ref mounts.ElementAt(i).Ammo;
                ammo.Current = ammo.Max;
                ammo.ReloadElapsed = 0f;
            }
        }
    }
}
