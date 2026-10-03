using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Interaction;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace HyperRTS.Input.Commands
{
    /// <summary>Drives building placement mode: moves the snapped ghost and confirms it as a PlaceBuilding command.</summary>
    // After CommandInputSystem, so a cancelling right-click can't also become a smart command this frame.
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(CommandInputSystem))]
    public partial class PlacementInputSystem : SystemBase
    {
        private RTSInputActions _actions;
        private Camera _camera;

        protected override void OnCreate()
        {
            _actions = new RTSInputActions();
            _actions.Commands.Enable();

            SingletonUtility.Ensure<PlacementState>(EntityManager);
            SingletonUtility.Ensure<PointerState>(EntityManager);
            RequireForUpdate<LocalPlayer>();
        }

        protected override void OnDestroy()
        {
            _actions?.Dispose();
            _actions = null;
        }

        protected override void OnUpdate()
        {
            var placement = SystemAPI.GetSingleton<PlacementState>();
            if (!placement.Active)
            {
                return;
            }

            var commands = _actions.Commands;
            if (placement.Prefab == Entity.Null || commands.Cancel.WasPressedThisFrame() ||
                commands.Command.WasPressedThisFrame())
            {
                SystemAPI.SetSingleton(new PlacementState());
                return;
            }

            if (SystemAPI.GetSingleton<PointerState>().OverUI || !TryGetGround(out var ground))
            {
                return;
            }

            MoveGhost(ref placement, ground);
            if (commands.Confirm.WasPressedThisFrame() && placement.Valid)
            {
                Place(ref placement, commands.Queue.IsPressed());
            }

            SystemAPI.SetSingleton(placement);
        }

        private void MoveGhost(ref PlacementState placement, float3 ground)
        {
            // The HUD may leave the footprint unset; the prefab's obstacle is the authoritative size.
            if (math.all(placement.Footprint <= 0f) && SystemAPI.HasComponent<NavObstacle>(placement.Prefab))
            {
                placement.Footprint = SystemAPI.GetComponent<NavObstacle>(placement.Prefab).Size;
            }

            if (!SystemAPI.TryGetSingleton<MapSettings>(out var map))
            {
                placement.Position = ground;
                placement.Valid = true;
                return;
            }

            SystemAPI.TryGetSingleton<NavGrid>(out var grid);
            CompleteDependency();
            placement.Position = PlacementMath.Snap(in map, ground, placement.Footprint);
            placement.Valid = PlacementMath.IsValid(in map, in grid, placement.Position, placement.Footprint);
        }

        private void Place(ref PlacementState placement, bool queue)
        {
            var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
            EntityManager.GetBuffer<PlayerCommand>(player).Add(new PlayerCommand
            {
                Type = CommandType.PlaceBuilding,
                Prefab = placement.Prefab,
                Position = placement.Position,
                Queue = queue,
            });

            // Shift keeps placing copies of the same building.
            if (!queue)
            {
                placement = new PlacementState();
            }
        }

        private bool TryGetGround(out float3 ground)
        {
            ground = default;
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return false;
                }
            }

            var hasPhysics = SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physics);
            CompleteDependency();
            var screen = (float2)_actions.Commands.Point.ReadValue<Vector2>();
            return WorldPointer.TryPick(_camera, screen, hasPhysics, in physics, EntityManager, out _, out ground);
        }
    }
}
