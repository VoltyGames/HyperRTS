namespace HyperRTS.Simulation.Navigation.Fields
{
    /// <summary>The cells a <see cref="ReverseDijkstra"/> searches: what stepping onto a settled cell costs each neighbour.</summary>
    public interface IReverseGraph
    {
        /// <summary>Offers each neighbour that may step onto the settled cell its cost through it.</summary>
        void Relax(int index, ref ReverseDijkstra search);
    }
}
