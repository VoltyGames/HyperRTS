using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Match
{
    /// <summary>
    /// Applies <see cref="GameSpeed"/> to <c>Time.timeScale</c> in the single-player world and restores normal speed
    /// when that world goes. Camera and HUD run on unscaled time, so they keep working while paused.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial class GameSpeedSystem : SystemBase
    {
        protected override void OnCreate()
        {
            EntityManager.CreateSingleton(GameSpeed.Normal, "GameSpeed");
        }

        protected override void OnUpdate()
        {
            var speed = SystemAPI.GetSingleton<GameSpeed>();
            UnityEngine.Time.timeScale = speed.Paused ? 0f : Mathf.Max(0f, speed.Scale);
        }

        protected override void OnDestroy() => UnityEngine.Time.timeScale = 1f;
    }
}
