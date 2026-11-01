using Unity.Entities;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>On a container passengers fire out of, such as a garrisoned house: their weapons reach this share further.</summary>
    public struct PassengerRange : IComponentData
    {
        public float Bonus;
    }
}
