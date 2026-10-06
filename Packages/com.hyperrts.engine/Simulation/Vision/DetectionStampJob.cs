using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Stamps every <see cref="Detector"/> into <see cref="FogOfWar.Detected"/> for its team. Single-threaded because
    /// overlapping circles OR into the same cells.
    /// </summary>
    [BurstCompile]
    internal partial struct DetectionStampJob : IJobEntity
    {
        public FogOfWar Fog;
        public FactionRelations Relations;

        private void Execute(in LocalTransform transform, in Detector detector, in Faction faction)
        {
            var team = Relations.TeamOf(faction.Value);
            if (team == 0 || team >= FactionRelations.MaxTeams || detector.Radius <= 0f)
            {
                return;
            }

            var bit = (byte)(1 << team);
            var center = transform.Position.xz;
            var radiusSq = detector.Radius * detector.Radius;
            var min = math.max(Fog.WorldToCell(transform.Position - detector.Radius), 0);
            var max = math.min(Fog.WorldToCell(transform.Position + detector.Radius), Fog.Size - 1);

            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var cellCenter = Fog.Min + (new float2(x, y) + 0.5f) * Fog.CellSize;
                    if (math.distancesq(cellCenter, center) <= radiusSq)
                    {
                        var index = Fog.Index(new int2(x, y));
                        Fog.Detected[index] = (byte)(Fog.Detected[index] | bit);
                    }
                }
            }
        }
    }
}
