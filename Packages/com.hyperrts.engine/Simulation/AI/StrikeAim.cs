using HyperRTS.Simulation.Strikes;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Where a strike does the most harm: on the enemy structure with the most enemy structures in its blast, never
    /// where the blast would catch the AI's own or allied structures.
    /// </summary>
    internal struct StrikeAim
    {
        private ComponentLookup<BomberStrike> _bombers;
        private ComponentLookup<MissileStrike> _missiles;

        public static StrikeAim Create(ref SystemState state) => new()
        {
            _bombers = state.GetComponentLookup<BomberStrike>(true),
            _missiles = state.GetComponentLookup<MissileStrike>(true),
        };

        public void Update(ref SystemState state)
        {
            _bombers.Update(ref state);
            _missiles.Update(ref state);
        }

        /// <summary>Whether the ability is a strike of <paramref name="caster"/>'s, and how far its blast reaches.</summary>
        public readonly bool IsStrike(Entity caster, int abilityId, out float reach)
        {
            reach = 0f;
            if (_bombers.TryGetComponent(caster, out var bomber) && bomber.AbilityId == abilityId)
            {
                reach = bomber.Bombs.Damage.SplashRadius + bomber.Bombs.ZoneRadius;
                return true;
            }

            if (_missiles.TryGetComponent(caster, out var missile) && missile.AbilityId == abilityId)
            {
                reach = missile.Blast.SplashRadius;
                return true;
            }

            return false;
        }

        /// <summary>The best aim among the enemy structures, or false when every one would hit a friendly one.</summary>
        public readonly bool TryFind(in AISnapshot snapshot, byte owner, float reach, out float3 point)
        {
            point = default;
            var buildings = snapshot.Buildings;
            var best = 0;
            for (var i = 0; i < buildings.Length; i++)
            {
                if (!snapshot.Relations.IsHostile(owner, buildings.Owners[i].Value))
                {
                    continue;
                }

                var score = Score(snapshot, owner, buildings.Position(i), reach);
                if (score > best)
                {
                    best = score;
                    point = buildings.Position(i);
                }
            }

            return best > 0;
        }

        /// <summary>Enemy structures within the blast, or 0 when a friendly one is in it too.</summary>
        private static int Score(in AISnapshot snapshot, byte owner, float3 point, float reach)
        {
            var buildings = snapshot.Buildings;
            var count = 0;
            for (var i = 0; i < buildings.Length; i++)
            {
                if (buildings.DistanceSq(i, point) > reach * reach)
                {
                    continue;
                }

                if (!snapshot.Relations.IsHostile(owner, buildings.Owners[i].Value))
                {
                    return 0;
                }

                count++;
            }

            return count;
        }
    }
}
