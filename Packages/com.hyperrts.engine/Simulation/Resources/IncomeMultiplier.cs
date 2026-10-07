using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>On a player: multiplies the resources its harvesters deliver (an AI difficulty bonus).</summary>
    public struct IncomeMultiplier : IComponentData
    {
        public float Value;
    }
}
