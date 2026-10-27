using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Resources
{
    public partial struct GatherSystem
    {
        private partial struct GatherJob
        {
            // A vehicle drives into a bay of the target's dock, waiting in line while every bay is taken, and first
            // finishes leaving the bay it worked at; anything else walks to the edge.
            private WalkUp Arrive(Entity target, Entity entity, ref HarvestState harvest, MoveControl move,
                in LocalTransform transform, in NavAgent agent)
            {
                if (IsLeaving(target, entity, ref harvest, ref move.Destination.ValueRW, transform, agent))
                {
                    Reach.Approach(target, transform, agent, move);
                    move.Destination.ValueRW.ExitRun =
                        Bays.Pose(harvest.Dock, harvest.Bay).ExitRun(transform.Position.xz);
                    return WalkUp.Walking;
                }

                if (!agent.UsesBays || !Bays.Has(target))
                {
                    return Reach.Approach(target, transform, agent, move);
                }

                if (!HoldsBay(target, entity, ref harvest, transform))
                {
                    WaitInLine(target, ref harvest, move, transform, agent);
                    return WalkUp.Walking;
                }

                var facing = math.normalizesafe(transform.Forward().xz);
                var bay = Bays.Pose(target, harvest.Bay);
                return ReachMath.ApproachBay(move, bay, transform.Position.xz, facing);
            }

            // Still driving out of another dock's bay, which frees once the hull is clear of the stop.
            private bool IsLeaving(Entity target, Entity entity, ref HarvestState harvest,
                ref MoveDestination destination, in LocalTransform transform, in NavAgent agent)
            {
                var dock = harvest.Dock;
                if (dock == Entity.Null || dock == target)
                {
                    return false;
                }

                var holds = harvest.Place == 0 && Bays.Has(dock) && Bays.Occupant(dock, harvest.Bay) == entity;
                if (holds && !Bays.Pose(dock, harvest.Bay).IsClear(transform.Position.xz, agent.HalfLength * 2f))
                {
                    Bays.Hold(dock, harvest.Bay, entity);
                    return true;
                }

                if (holds)
                {
                    Bays.Free(dock, harvest.Bay, entity);
                }

                harvest.Dock = Entity.Null;
                harvest.Place = 0;
                destination.ExitRun = 0f;
                return false;
            }

            // Keeps the bay it holds, or claims one; in line it tries again every half second.
            private bool HoldsBay(Entity dock, Entity entity, ref HarvestState harvest, in LocalTransform transform)
            {
                var waits = harvest.Dock == dock && harvest.Place > 0;
                if (harvest.Dock == dock && !waits && Bays.Occupant(dock, harvest.Bay) == entity)
                {
                    Bays.Hold(dock, harvest.Bay, entity);
                    return true;
                }

                if (waits && harvest.RetryIn > 0f)
                {
                    harvest.RetryIn -= DeltaTime;
                    return false;
                }

                var firstInLineAt = waits && harvest.Place == 1 ? harvest.Bay : -1;
                if (!Bays.TryClaim(dock, entity, transform.Position.xz, firstInLineAt, out var bay))
                {
                    harvest.RetryIn = RetrySeconds;
                    return false;
                }

                harvest.Dock = dock;
                harvest.Bay = bay;
                harvest.Place = 0;
                return true;
            }

            private void WaitInLine(Entity dock, ref HarvestState harvest, MoveControl move, in LocalTransform transform,
                in NavAgent agent)
            {
                var place = harvest.Dock == dock ? harvest.Place : 0;
                harvest.Place = Bays.Queue(dock, ref harvest.Bay, place);
                harvest.Dock = dock;
                var bay = Bays.Pose(dock, harvest.Bay);
                ReachMath.WaitInLine(move, bay, harvest.Place, agent.HalfLength * 2f, transform.Position.xz);
            }
        }
    }
}
