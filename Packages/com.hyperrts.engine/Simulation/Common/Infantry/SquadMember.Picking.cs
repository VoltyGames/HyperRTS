using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Common.Infantry
{
    public partial struct SquadMember
    {
        /// <summary>
        /// One living member chosen by <paramref name="seed"/>, or -1 when all are dead. Members weigh by the share of a
        /// hit from <paramref name="from"/> they take, so exposed ones are hit more often than ones in cover.
        /// </summary>
        public static int PickExposed(DynamicBuffer<SquadMember> members, float3 squad, uint seed,
            in CoverRules rules, float3 from, float piercing)
        {
            var total = 0f;
            for (var i = 0; i < members.Length; i++)
            {
                total += members[i].Alive ? members[i].ExposureTo(rules, squad, from, piercing) : 0f;
            }

            var roll = Random.CreateFromIndex(seed).NextFloat() * total;
            var last = -1;
            for (var i = 0; i < members.Length; i++)
            {
                if (!members[i].Alive)
                {
                    continue;
                }

                last = i;
                roll -= members[i].ExposureTo(rules, squad, from, piercing);
                if (roll < 0f)
                {
                    return i;
                }
            }

            return last;
        }

        /// <summary>The living member standing nearest <paramref name="point"/>, or -1 when all are dead.</summary>
        public static int FindNearest(DynamicBuffer<SquadMember> members, float3 squad, float3 point)
        {
            var nearest = -1;
            var best = float.MaxValue;
            for (var i = 0; i < members.Length; i++)
            {
                var distance = math.distancesq(members[i].Position(squad).xz, point.xz);
                if (members[i].Alive && distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            return nearest;
        }
    }
}
