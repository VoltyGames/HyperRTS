using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>One unit's fire mission: the parts of it a job gathers, and the steps that approach the point, aim and end the order.</summary>
    internal struct MissionRunner
    {
        private EnabledRefRW<ActiveOrder> _busy;
        private RefRW<PointAim> _aim;
        private EnabledRefRW<PointAim> _aiming;
        private UnitMove _moves;

        public MissionRunner(EnabledRefRW<ActiveOrder> busy, RefRW<PointAim> aim, EnabledRefRW<PointAim> aiming,
            in UnitMove moves)
        {
            _busy = busy;
            _aim = aim;
            _aiming = aiming;
            _moves = moves;
        }

        /// <summary>One frame of the unit's order with its primary <paramref name="mount"/>; range is measured from the unit's centre.</summary>
        public void Run(float3 position, in WeaponMount mount, in Order order)
        {
            if (!_busy.ValueRO || order.Type != OrderType.FireMission)
            {
                // A new order or a stop drops the aim; a salvo already under way still finishes.
                if (_aiming.ValueRO)
                {
                    _aiming.ValueRW = false;
                }

                return;
            }

            if (!mount.Ready || !mount.Weapon.FiresAtGround)
            {
                End();
            }
            else if (!_aiming.ValueRO)
            {
                Approach(position, order.Position, mount.Weapon);
            }
            else if (mount.Weapon.SalvosFired != _aim.ValueRO.SalvosAtStart)
            {
                // Only the order's own salvo counts, which the weapon completes once.
                End();
            }
        }

        /// <summary>Halts and aims once the point is in range and the weapon is between salvos, else moves to the near edge of its range.</summary>
        private void Approach(float3 position, float3 point, in Weapon weapon)
        {
            var gap = math.distance(position.xz, point.xz);
            var band = weapon.Band(weapon.Range);
            if (band.Contains(gap))
            {
                _moves.Halt();
                if (weapon.SalvoShot == 0)
                {
                    _aim.ValueRW = new PointAim { Value = point, SalvosAtStart = weapon.SalvosFired };
                    _aiming.ValueRW = true;
                }

                return;
            }

            if (!_moves.HasDestination)
            {
                End();
                return;
            }

            // Asked every frame, so a deployed unit packs up first and goes once packed.
            var away = math.normalizesafe(position.xz - point.xz, new float2(0f, 1f));
            var goal = point.xz + away * band.Nearest(gap);
            _moves.MoveTo(new float3(goal.x, position.y, goal.y), EngagementSystem.RepathDistance);
        }

        private void End()
        {
            _busy.ValueRW = false;
            _aiming.ValueRW = false;
            _moves.Halt();
        }
    }
}
