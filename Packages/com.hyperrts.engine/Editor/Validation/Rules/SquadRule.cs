using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Squads;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A squad has members, each carrying a weapon the unit has, and every weapon has a carrier, counting upgraded ones.</summary>
    public sealed class SquadRule : AuthoringRule<SquadAuthoring>
    {
        protected override void Check(SquadAuthoring squad, ValidationIssues issues)
        {
            if (squad.members.Count == 0)
            {
                issues.Error(squad, $"{squad.name} has a Squad with no members.");
                return;
            }

            if (!squad.TryGetComponent<WeaponAuthoring>(out var armed))
            {
                issues.Error(squad, $"{squad.name} has a Squad but no Weapon.");
                return;
            }

            var weapons = armed.WeaponCount;
            var carried = new bool[weapons];
            foreach (var member in squad.members)
            {
                if (member.weapon < 0 || member.weapon >= weapons)
                {
                    issues.Error(squad, $"{squad.name} has a member with weapon {member.weapon}, but the unit has {weapons} weapons.");
                    continue;
                }

                carried[member.weapon] = true;
                if (!member.hasUpgradedWeapon)
                {
                    continue;
                }

                if (member.upgradedWeapon < 0 || member.upgradedWeapon >= weapons)
                {
                    issues.Error(squad, $"{squad.name} has a member upgrading to weapon {member.upgradedWeapon}, but the unit has {weapons} weapons.");
                    continue;
                }

                carried[member.upgradedWeapon] = true;
            }

            for (var i = 0; i < weapons; i++)
            {
                if (!carried[i])
                {
                    issues.Warn(squad, $"{squad.name} has no member carrying weapon {i}, so it never fires.");
                }
            }
        }
    }
}
