using HyperRTS.Simulation.Common;

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

        public static void AddPassenger<TWriter>(ref TWriter writer, int size) where TWriter : struct, IEntityWriter
        {
            writer.Add(new Passenger { Size = size });
            writer.Add<Inside>();
            writer.Add<PassengerStance>();
            writer.SetEnabled<Inside>(false);
        }
    }
}
