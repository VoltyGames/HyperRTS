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
