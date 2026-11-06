using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Spatial;

namespace HyperRTS.Simulation.Combat.Damage
{
    public partial struct DamageSystem
    {
        private partial struct ResolveJob
        {
            /// <summary>Each living member but the one a direct hit already struck takes the blast by its own distance.</summary>
            private void SplashMembers(in DamageEvent hit, in SpatialEntry squad, int struck)
            {
                var members = Squads.Members(squad.Entity);
                for (var i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    var gap = EntityRadius.EdgeDistance(member.Position(squad.Position), SquadMember.Radius, hit.Position, 0f);
                    if (i != struck && member.Alive && gap <= hit.Profile.Radius)
                    {
                        var factor = hit.Profile.SplashFactor(gap);
                        Apply(squad.Entity, hit, new Blow
                        {
                            Amount = hit.Profile.Amount * factor,
                            From = hit.Position,
                            Member = i,
                            Suppression = hit.Profile.Suppression * factor / members.Length,
                        });
                    }
                }
            }
        }
    }
}
