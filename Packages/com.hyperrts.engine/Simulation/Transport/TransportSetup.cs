using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>Adds the container and passenger components.</summary>
    public static class TransportSetup
    {
        public static void AddContainer<TSink>(ref TSink sink, in Container container) where TSink : struct, IComponentSink
        {
            sink.Add(container);
            sink.AddBuffer<Cargo>();
        }

        public static void AddPassenger<TSink>(ref TSink sink, int size) where TSink : struct, IComponentSink
        {
            sink.Add(new Passenger { Size = size });
            sink.Add<Inside>();
            sink.Add<PassengerStance>();
            sink.SetEnabled<Inside>(false);
        }
    }
}
