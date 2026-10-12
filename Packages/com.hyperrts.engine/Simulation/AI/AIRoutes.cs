using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Which attack lane a bot takes. Only integer maths on its inputs, so every machine and every replay of a match
    /// picks the same lane.
    /// </summary>
    public static class AIRoutes
    {
        /// <summary>
        /// A bot's <see cref="AIPlayer.RouteSeed"/> from the match seed and its faction: a stable hash, never 0 for a
        /// nonzero match seed. A match seed of 0 means unseeded and gives 0.
        /// </summary>
        public static uint SeedFor(uint matchSeed, byte faction)
        {
            if (matchSeed == 0)
            {
                return 0;
            }

            var hash = math.hash(new uint2(matchSeed, faction));
            return hash == 0 ? 1u : hash;
        }

        /// <summary>
        /// Index (below <paramref name="count"/>, which must be above 0) of the lane for a bot's attack: a seeded
        /// choice from the bot's faction, the faction it attacks and how many attacks it has sent, so bots differ from
        /// each other and a bot varies its route from one attack to the next.
        /// </summary>
        public static int PickLane(uint seed, byte faction, byte enemy, int attack, int count)
        {
            var random = Random.CreateFromIndex(math.hash(new uint4(seed, faction, enemy, (uint)attack)));
            return random.NextInt(count);
        }
    }
}
