using System.Collections.Generic;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Sectors;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Paths
{
    /// <summary>Opens group fields for a frame's requests: who joins, where the goal sits and how a large group splits.</summary>
    internal struct GroupOpener
    {
        private PathBatch _batch;
        private NavGrid _grid;
        private GroupFields _fields;
        private PathPlan _plan;
        private double _now;
        private NativeParallelMultiHashMap<int2, int> _members;
        private NativeHashSet<int2> _tried;
        private SectorSearch _search;

        // Abstract paths a split shares per start component: ranges into the routes, by component.
        private NativeList<int> _routes;
        private NativeHashMap<int, int2> _byComponent;

        public GroupOpener(PathBatch batch, in NavGrid grid, in GroupFields fields, in PathPlan plan, double now)
        {
            _batch = batch;
            _grid = grid;
            _fields = fields;
            _plan = plan;
            _now = now;
            _members = new NativeParallelMultiHashMap<int2, int>(math.max(1, batch.Count), Allocator.Temp);
            _tried = new NativeHashSet<int2>(8, Allocator.Temp);
            _search = new SectorSearch(Allocator.Temp);
            _routes = new NativeList<int>(256, Allocator.Temp);
            _byComponent = new NativeHashMap<int, int2>(16, Allocator.Temp);
        }

        public void Dispose()
        {
            _members.Dispose();
            _tried.Dispose();
            _search.Dispose();
            _routes.Dispose();
            _byComponent.Dispose();
        }

        public void AddMember(int2 key, int request) => _members.Add(key, request);

        // Opens the key's fields the first time a frame asks; cost is what that spent.
        public bool TryOpen(int2 key, in NavAccess access, int first, out float cost)
        {
            if (!_tried.Add(key))
            {
                cost = 0f;
                return false;
            }

            cost = OpenFields(key, access, first);
            return true;
        }

        private struct BySector : IComparer<int2>
        {
            public int Compare(int2 a, int2 b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);
        }

        // A field toward the centre of the slots, for the frame's members that start in its region and see their
        // slot from it; fewer than two leave every member to its own search.
        private float OpenFields(int2 key, in NavAccess access, int first)
        {
            var members = new NativeList<int>(16, Allocator.Temp);
            var centroid = float2.zero;
            foreach (var member in _members.GetValuesForKey(key))
            {
                var query = _batch.Query(member, access);
                if (!_batch.IsHandled(member) && query.Route == PathRoute.Search)
                {
                    members.Add(member);
                    centroid += query.End.xz;
                }
            }

            var cost = 0f;
            if (members.Length >= GroupFields.MinMembers)
            {
                var region = access.Region(_batch.Query(first, access).From);
                var center = _grid.Grid.Cell(centroid / members.Length);
                if (access.TryFindNearest(center, GridPathfinder.ReachableSearchRadius, region, out var goal))
                {
                    cost = Join(key, access, members, region, goal);
                }
            }

            members.Dispose();
            return cost;
        }

        private float Join(int2 key, in NavAccess access, NativeList<int> members, int region, int2 goal)
        {
            var anchor = _grid.Grid.CenterPoint(goal);
            var joiners = new NativeList<int2>(members.Length, Allocator.Temp);
            var radius = 0f;
            foreach (var member in members)
            {
                var query = _batch.Query(member, access);
                var distance = math.distance(query.End.xz, anchor.xz);
                var joins = access.Region(query.From) == region && distance <= GroupFields.MaxGoalArea &&
                            access.HasLineOfSight(anchor, query.End);
                if (joins)
                {
                    joiners.Add(new int2(_grid.Sectors.Layout.Index(query.From), member));
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
                cost = Split(field, access, joiners, goal);
            }

            joiners.Dispose();
            return cost;
        }

        // Members join the current field until its corridor would pass the cell cap, then start the next.
        private float Split(GroupField field, in NavAccess access, NativeList<int2> joiners, int2 goal)
        {
            var cost = 0f;
            var corridor = new NavCorridor(_grid.Field.Layout, Allocator.Persistent);
            var part = new NativeList<int>(joiners.Length, Allocator.Temp);
            _routes.Clear();
            _byComponent.Clear();
            foreach (var joiner in joiners)
            {
                var path = Route(access, _batch.Query(joiner.y, access).From, goal);
                var kept = corridor.SectorCount;
                corridor.AddWithRings(path);
                if (part.Length > 0 && corridor.Cells > GroupFields.MaxCells)
                {
                    corridor.Truncate(kept);
                    cost += Open(field, corridor, goal, part);
                    corridor = new NavCorridor(_grid.Field.Layout, Allocator.Persistent);
                    corridor.AddWithRings(path);
                    part.Clear();
                }

                part.Add(joiner.y);
            }

            cost += Open(field, corridor, goal, part);
            part.Dispose();
            return cost;
        }

        // Members starting in one component share its abstract path: it begins there, so it covers them all.
        private NativeArray<int> Route(in NavAccess access, int2 from, int2 goal)
        {
            var component = _grid.Sectors.Component(access.ClassIndex, from);
            if (!_byComponent.TryGetValue(component, out var range))
            {
                range.x = _routes.Length;
                _search.Find(access, from, goal, _routes);
                range.y = _routes.Length - range.x;
                _byComponent[component] = range;
            }

            return _routes.AsArray().GetSubArray(range.x, range.y);
        }

        private float Open(GroupField field, NavCorridor corridor, int2 goal, NativeList<int> part)
        {
            field.Field = new FlowField(corridor, goal, Allocator.Persistent);
            var index = _fields.Fields.Length;
            _fields.Fields.Add(field);
            _plan.Builds.Add(index);
            float cost = corridor.Cells;
            foreach (var member in part)
            {
                cost += _batch.AddTrace(member, index);
            }

            return cost;
        }
    }
}
