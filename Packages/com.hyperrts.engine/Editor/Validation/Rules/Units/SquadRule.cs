using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Squads;

namespace HyperRTS.Editor.Validation.Rules.Units
{
    /// <summary>A squad has members, no more than a replay records, each carrying a weapon the unit has, and every weapon has a carrier, counting upgraded ones.</summary>
    public sealed class SquadRule : AuthoringRule<SquadAuthoring>
    {
        protected override void Check(SquadAuthoring squad, ValidationIssues issues)
        {
            if (squad.members.Count == 0)
            {
                issues.Error(squad, $"{squad.name} has a Squad with no members.");
                return;
            }

            if (squad.members.Count > SquadRoster.MaskedMembers)
            {
                issues.Error(squad, $"{squad.name} has {squad.members.Count} squad members, but a replay records at most {SquadRoster.MaskedMembers}.");
            }

            if (!squad.TryGetComponent<WeaponAuthoring>(out var armed))
            {
                issues.Error(squad, $"{squad.name} has a Squad but no Weapon.");
                return;
            }

            CheckCarriers(squad, armed.WeaponCount, issues);
        }

        private static void CheckCarriers(SquadAuthoring squad, int weapons, ValidationIssues issues)
        {
            var carried = new bool[weapons];
            foreach (var member in squad.members)
            {
                if (!Carry(squad, carried, member.weapon, "with", issues))
                {
                    continue;
                }

                if (member.hasUpgradedWeapon)
                {
                    Carry(squad, carried, member.upgradedWeapon, "upgrading to", issues);
                }
            }

            for (var i = 0; i < weapons; i++)
            {
                if (!carried[i])
                {
                    issues.Warn(squad, $"{squad.name} has no member carrying weapon {i}, so it never fires.");
                }
            }
        }

        // Marks the weapon carried, or reports a member naming a weapon the unit lacks.
        private static bool Carry(SquadAuthoring squad, bool[] carried, int weapon, string how, ValidationIssues issues)
        {
            if (weapon >= 0 && weapon < carried.Length)
            {
                carried[weapon] = true;
                return true;
            }

            issues.Error(squad, $"{squad.name} has a member {how} weapon {weapon}, but the unit has {carried.Length} weapons.");
            return false;
        }
    }
}
