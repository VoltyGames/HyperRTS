using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>Adds the container and passenger components.</summary>
    public static class TransportSetup
    {
        public static void AddContainer<TWriter>(ref TWriter writer, in Container container)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(container);
            writer.AddBuffer<Cargo>();
        }

        /// <summary>Lets a share of each hit on the container through to its passengers; see <see cref="Shelter"/>.</summary>
        public static void AddShelter<TWriter>(ref TWriter writer, float protection)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new Shelter { Protection = protection });
            writer.AddBuffer<ShelterHit>();
        }

        public static void AddPassenger<TWriter>(ref TWriter writer, int size) where TWriter : struct, IEntityWriter
        {
            writer.Add(new Passenger { Size = size });
            writer.Add<Inside>();
            writer.Add<PassengerStance>();
            writer.SetEnabled<Inside>(false);
        }
    }
}
