using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Damage
{
    public partial struct DamageSystem
    {
        private partial struct ResolveJob
        {
            /// <summary>A blast aimed at a squad strikes the member nearest it; -1 lets a direct hit roll.</summary>
            private int Nearest(in DamageEvent hit)
            {
                if (hit.Radius <= 0f || !Members.TryGetBuffer(hit.Target, out var members)
                    || !Transforms.TryGetComponent(hit.Target, out var squad))
                {
                    return -1;
                }

                return SquadMember.FindNearest(members, squad.Position, hit.Position);
            }

            /// <summary>Each living member but the one a direct hit already struck takes the blast by its own distance.</summary>
            private void SplashMembers(in DamageEvent hit, in SpatialEntry squad, int struck)
            {
                var members = Members[squad.Entity];
                for (var i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    var gap = EntityRadius.EdgeDistance(member.Position(squad.Position), SquadMember.Radius, hit.Position, 0f);
                    if (i != struck && member.Alive && gap <= hit.Radius)
                    {
                        var factor = hit.SplashFactor(gap);
                        Apply(squad.Entity, hit.Amount * factor, hit, hit.Position, 0u, i, hit.Suppression * factor / members.Length);
                    }
                }
            }

            /// <summary>Adds the suppression of a hit to the squad, as much as the struck member's cover lets through.</summary>
            private void Suppress(Entity squad, in SquadMember member, float amount, in DamageEvent hit, float3 from)
            {
                if (amount > 0f && Suppressions.TryGetComponent(squad, out var suppression))
                {
                    suppression.Add(amount * member.SuppressionFrom(Cover, Transforms[squad].Position, from, hit.CoverPiercing));
                    Suppressions[squad] = suppression;
                }
            }
        }
    }
}
