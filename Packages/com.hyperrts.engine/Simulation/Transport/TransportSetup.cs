using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>Adds the container and passenger components.</summary>
    public static class TransportSetup
    {
        public static void AddContainer(EntityWriter writer, in Container container)
        {
            writer.Add(container);
            writer.AddBuffer<Cargo>();
        }

        /// <summary>Lets a share of each hit on the container through to its passengers; see <see cref="Shelter"/>.</summary>
        public static void AddShelter(EntityWriter writer, float protection)
        {
            writer.Add(new Shelter { Protection = protection });
            writer.AddBuffer<ShelterHit>();
        }

        public static void AddPassenger(EntityWriter writer, int size)
        {
            writer.Add(new Passenger { Size = size });
            writer.Add<Inside>();
            writer.Add<PassengerStance>();
            writer.SetEnabled<Inside>(false);
        }
    }
}
