using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// The working points of one kind, copied out of the world for one look: which a faction may use, which cover a
    /// position and the nearest. Systems, the AI and the HUD all ask the same questions of it.
    /// </summary>
    public struct SquadPoints
    {
        private NativeArray<Entity> _entities;
        private NativeArray<Faction> _owners;
        private NativeArray<LocalTransform> _places;
        private NativeArray<float> _reaches;

        /// <summary>
        /// Reads every point the query matches; it must hold <typeparamref name="T"/>, <see cref="Faction"/> and
        /// <see cref="LocalTransform"/> and leave out <see cref="Dead"/> and <see cref="Offline"/> ones.
        /// </summary>
        public static SquadPoints From<T>(EntityQuery query) where T : unmanaged, IComponentData, ISquadPoint
        {
            var points = query.ToComponentDataArray<T>(Allocator.Temp);
            var reaches = new NativeArray<float>(points.Length, Allocator.Temp);
            for (var i = 0; i < points.Length; i++)
            {
                reaches[i] = points[i].Reach;
            }

            return new SquadPoints
            {
                _entities = query.ToEntityArray(Allocator.Temp),
                _owners = query.ToComponentDataArray<Faction>(Allocator.Temp),
                _places = query.ToComponentDataArray<LocalTransform>(Allocator.Temp),
                _reaches = reaches,
            };
        }

        /// <summary>Whether the faction or an ally has a point.</summary>
        public readonly bool HasAllied(byte faction, in FactionRelations relations) =>
            Find(float3.zero, faction, relations, false) >= 0;

        /// <summary>Whether <paramref name="here"/> is within the reach of a point the faction may use.</summary>
        public readonly bool Covers(float3 here, byte faction, in FactionRelations relations) =>
            Find(here, faction, relations, true) >= 0;

        /// <summary>The nearest point the faction may use wherever it is, or null.</summary>
        public readonly Entity NearestAllied(float3 here, byte faction, in FactionRelations relations) =>
            At(Find(here, faction, relations, false));

        /// <summary>The nearest point the faction may use that covers <paramref name="here"/>, or null.</summary>
        public readonly Entity NearestCovering(float3 here, byte faction, in FactionRelations relations) =>
            At(Find(here, faction, relations, true));

        /// <summary>Whether <paramref name="point"/> still works and <paramref name="here"/> is within its reach.</summary>
        public readonly bool Reaches(Entity point, float3 here)
        {
            var index = _entities.IndexOf(point);
            return index >= 0 && IsWithin(index, here);
        }

        /// <summary>Whether <paramref name="point"/> is a working point of this kind.</summary>
        public readonly bool Contains(Entity point) => _entities.IndexOf(point) >= 0;

        private readonly Entity At(int index) => index >= 0 ? _entities[index] : Entity.Null;

        private readonly bool IsWithin(int index, float3 here) =>
            math.distancesq(here.xz, _places[index].Position.xz) <= _reaches[index] * _reaches[index];

        private readonly int Find(float3 here, byte faction, in FactionRelations relations, bool mustCover)
        {
            var found = -1;
            var best = float.MaxValue;
            for (var i = 0; i < _entities.Length; i++)
            {
                var distance = math.distancesq(here.xz, _places[i].Position.xz);
                var usable = relations.IsAllied(faction, _owners[i].Value) && (!mustCover || IsWithin(i, here));
                if (usable && distance < best)
                {
                    best = distance;
                    found = i;
                }
            }

            return found;
        }
    }
}
