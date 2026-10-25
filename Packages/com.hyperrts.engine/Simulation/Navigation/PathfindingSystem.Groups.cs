using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    public partial struct PathfindingSystem
    {
        private struct BySector : IComparer<int2>
        {
            public int Compare(int2 a, int2 b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);
        }

        /// <summary>Opening group fields: who joins, where the goal sits and how a large group splits.</summary>
        private partial struct Planner
        {
            // A field toward the centre of the slots, for the frame's members that start in its region and see their
            // slot from it; fewer than two leave every member to its own search.
            private float OpenFields(int2 key, in NavAccess access, int first, out bool joined)
            {
                joined = false;
                var members = new NativeList<int>(16, Allocator.Temp);
                var centroid = float2.zero;
                foreach (var member in _members.GetValuesForKey(key))
                {
                    var query = Query(member, access);
                    if ((!_handled[member] || member == first) && query.Route == PathRoute.Search)
                    {
                        members.Add(member);
                        centroid += query.End.xz;
                    }
                }

                var cost = 0f;
                if (members.Length >= GroupFields.MinMembers)
                {
                    var region = access.Region(Query(first, access).From);
                    var center = _grid.WorldToCell(centroid / members.Length);
                    if (access.TryFindNearest(center, GridPathfinder.ReachableSearchRadius, region, out var goal))
                    {
                        cost = Join(key, access, members, region, goal, first, out joined);
                    }
                }

                members.Dispose();
                return cost;
            }

            private float Join(int2 key, in NavAccess access, NativeList<int> members, int region, int2 goal,
                int first, out bool joined)
            {
                joined = false;
                var anchor = _grid.CellCenter(goal);
                var joiners = new NativeList<int2>(members.Length, Allocator.Temp);
                var radius = 0f;
                foreach (var member in members)
                {
                    var query = Query(member, access);
                    var distance = math.distance(query.End.xz, anchor.xz);
                    var joins = access.Region(query.From) == region && distance <= GroupFields.MaxGoalArea &&
                                access.HasLineOfSight(anchor, query.End);
                    if (joins)
                    {
                        joiners.Add(new int2(_grid.Sectors.SectorOf(query.From), member));
                        radius = math.max(radius, distance);
                    }
                }

                var cost = 0f;
                if (joiners.Length >= GroupFields.MinMembers)
                {
                    joiners.Sort(new BySector());
                    var field = new GroupField
                    {
                        Key = key, Anchor = anchor, Radius = radius, WallMargin = access.WallMargin,
                        Version = _grid.Version, LastUsed = _now,
                    };
                    cost = Split(field, access, joiners, goal, first, out joined);
                }

                joiners.Dispose();
                return cost;
            }

            // Members join the current field until its corridor would pass the cell cap, then start the next.
            private float Split(GroupField field, in NavAccess access, NativeList<int2> joiners, int2 goal, int first,
                out bool joined)
            {
                joined = false;
                var cost = 0f;
                var corridor = new NavCorridor(_grid.Field.Sectors, Allocator.Persistent);
                var part = new NativeList<int>(joiners.Length, Allocator.Temp);
                var routes = new NativeList<int>(256, Allocator.Temp);
                var byComponent = new NativeHashMap<int, int2>(16, Allocator.Temp);
                foreach (var joiner in joiners)
                {
                    var path = Route(access, Query(joiner.y, access).From, goal, routes, byComponent);
                    var kept = corridor.SectorCount;
                    AddWithRings(ref corridor, path);
                    if (part.Length > 0 && corridor.Cells > GroupFields.MaxCells)
                    {
                        corridor.Truncate(kept);
                        cost += Open(field, corridor, goal, part, first, ref joined);
                        corridor = new NavCorridor(_grid.Field.Sectors, Allocator.Persistent);
                        AddWithRings(ref corridor, path);
                        part.Clear();
                    }

                    part.Add(joiner.y);
                }

                cost += Open(field, corridor, goal, part, first, ref joined);
                part.Dispose();
                routes.Dispose();
                byComponent.Dispose();
                return cost;
            }

            // Members starting in one component share its abstract path: it begins there, so it covers them all.
            private NativeArray<int> Route(in NavAccess access, int2 from, int2 goal, NativeList<int> routes,
                NativeHashMap<int, int2> byComponent)
            {
                var component = _grid.Sectors.Component(access.ClassIndex, from);
                if (!byComponent.TryGetValue(component, out var range))
                {
                    range.x = routes.Length;
                    _search.Find(access, from, goal, routes);
                    range.y = routes.Length - range.x;
                    byComponent[component] = range;
                }

                return routes.AsArray().GetSubArray(range.x, range.y);
            }

            private static void AddWithRings(ref NavCorridor corridor, NativeArray<int> sectors)
            {
                foreach (var sector in sectors)
                {
                    corridor.AddWithRing(sector);
                }
            }

            private float Open(GroupField field, NavCorridor corridor, int2 goal, NativeList<int> part, int first,
                ref bool joined)
            {
                field.Field = new FlowField(corridor, goal, Allocator.Persistent);
                var index = _fields.Fields.Length;
                _fields.Fields.Add(field);
                _plan.Builds.Add(index);
                float cost = corridor.Cells;
                foreach (var member in part)
                {
                    _handled[member] = true;
                    joined |= member == first;
                    cost += AddTrace(member, _queries[member], index);
                }

                return cost;
            }
        }
    }
}
