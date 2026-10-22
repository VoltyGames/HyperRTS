using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Airdrops;
using HyperRTS.Simulation.Vision;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A strategic asset names a stealthy aircraft, a bomb prefab and a charging point ability on its structure.</summary>
    public sealed class StrategicAssetRule : AuthoringRule<StrategicAssetAuthoring>
    {
        protected override void Check(StrategicAssetAuthoring asset, ValidationIssues issues)
        {
            CheckAircraft(asset, issues);
            if (asset.bomb == null)
            {
                issues.Error(asset, "Strategic asset has no bomb prefab.");
            }

            CheckAbility(asset, issues);
            CheckPrefabReference(asset, asset.aircraft, "Aircraft", issues);
            CheckPrefabReference(asset, asset.bomb, "Bomb", issues);
        }

        private static void CheckAircraft(StrategicAssetAuthoring asset, ValidationIssues issues)
        {
            if (asset.aircraft == null)
            {
                issues.Error(asset, "Strategic asset has no aircraft prefab.");
                return;
            }

            if (!asset.aircraft.TryGetComponent<FlightAuthoring>(out _))
            {
                issues.Error(asset, $"Aircraft '{asset.aircraft.name}' has no Flight.");
            }

            if (!asset.aircraft.TryGetComponent<StealthAuthoring>(out _))
            {
                issues.Warn(asset, $"Aircraft '{asset.aircraft.name}' has no Stealth, so it is detectable all the way in.");
            }
        }

        private static void CheckAbility(StrategicAssetAuthoring asset, ValidationIssues issues)
        {
            if (!asset.TryGetComponent<AbilityAuthoring>(out var abilities))
            {
                return;
            }

            var entry = abilities.abilities.Find(each => each.name == asset.ability);
            if (entry == null)
            {
                issues.Error(asset, $"No Ability entry named '{asset.ability}' on this structure.");
                return;
            }

            if (entry.target != AbilityTarget.Point || !entry.startsOnCooldown)
            {
                issues.Error(asset, $"Ability '{asset.ability}' must target a point and start on cooldown.");
            }
        }
    }
}
