using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Common.Infantry
{
    /// <summary>How hard incoming fire keeps a squad's heads down.</summary>
    public enum SuppressionLevel : byte
    {
        Steady,
        Suppressed,
        Pinned,
    }

    /// <summary>
    /// A squad's suppression from 0 to 1: it rises with the fire that lands on the squad and falls once the fire has
    /// stopped for <see cref="SquadRules.DecayDelay"/> seconds. <see cref="SquadRules"/> turns it into a level.
    /// </summary>
    public struct Suppression : IComponentData
    {
        [GhostField(Quantization = 100)] public float Value;

        /// <summary>Whether the squad is running for a retreat point, sent so the HUD can show it.</summary>
        [GhostField] public bool Retreating;

        /// <summary>Server only: seconds since the last fire landed.</summary>
        [GhostField(SendData = false)] public float Quiet;

        public void Add(float amount)
        {
            Value = math.saturate(Value + amount);
            Quiet = 0f;
        }

        public void Decay(in SquadRules rules, float deltaTime)
        {
            Quiet += deltaTime;
            if (Quiet > rules.DecayDelay)
            {
                Value = math.max(0f, Value - rules.DecayRate * deltaTime);
            }
        }

        public void Clear()
        {
            Value = 0f;
            Quiet = 0f;
        }
    }

    /// <summary>
    /// Multipliers a squad's state sets each frame on its speed, rate of fire and the damage it takes (1 is normal).
    /// A speed of 0 freezes the squad where it stands and keeps its orders.
    /// </summary>
    public struct Pace : IComponentData
    {
        public float Speed;
        public float FireRate;
        public float DamageTaken;

        public static Pace Normal => new() { Speed = 1f, FireRate = 1f, DamageTaken = 1f };
    }
}
