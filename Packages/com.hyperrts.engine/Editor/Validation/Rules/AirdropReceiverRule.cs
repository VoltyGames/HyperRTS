using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Airdrops;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>An airdrop receiver names a flying aircraft, a ghost crate and the resource the crate pays.</summary>
    public sealed class AirdropReceiverRule : AuthoringRule<AirdropReceiverAuthoring>
    {
        protected override void Check(AirdropReceiverAuthoring receiver, ValidationIssues issues)
        {
            if (receiver.aircraft == null)
            {
                issues.Error(receiver, "Airdrop receiver has no aircraft prefab.");
            }
            else if (!receiver.aircraft.TryGetComponent<FlightAuthoring>(out _))
            {
                issues.Error(receiver, $"Aircraft '{receiver.aircraft.name}' has no Flight.");
            }

            if (receiver.crate == null)
            {
                issues.Error(receiver, "Airdrop receiver has no crate prefab.");
            }
            else
            {
                WarnIfNotGhost(receiver, receiver.crate, "The crate is not a ghost, so clients never see it.", issues);
            }

            if (receiver.type == null)
            {
                issues.Error(receiver, "Airdrop receiver has no resource type.");
            }

            CheckPrefabReference(receiver, receiver.aircraft, "Aircraft", issues);
            CheckPrefabReference(receiver, receiver.crate, "Crate", issues);
        }
    }
}
