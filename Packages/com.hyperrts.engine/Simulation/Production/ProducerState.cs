namespace HyperRTS.Simulation.Production
{
    /// <summary>What a producer is doing this frame, replicated so clients animate and show it without the rules.</summary>
    public enum ProducerState : byte
    {
        /// <summary>Nothing queued, or still under construction.</summary>
        Idle,

        /// <summary>Building the head of its queue, slowly on low power.</summary>
        Working,

        /// <summary>The owner has no population room for the head of the queue.</summary>
        WaitingPopulation,

        /// <summary>The head is an aircraft and every landing pad of its kind here is taken.</summary>
        WaitingPad,

        /// <summary>Offline for a reason other than low power, such as an EMP.</summary>
        Offline,
    }
}
