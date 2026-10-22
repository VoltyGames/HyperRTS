namespace HyperRTS.Simulation.Production
{
    /// <summary>Why <see cref="PurchaseRules.FindRefusal"/> refuses a purchase; locks come before the price.</summary>
    public enum PurchaseRefusal : byte
    {
        None,

        /// <summary>Research already finished or queued.</summary>
        Researched,

        /// <summary>The player has as many as its <see cref="BuildLimit"/> allows.</summary>
        AtLimit,

        /// <summary>A required building or research is missing.</summary>
        MissingPrerequisite,

        CannotAfford,
    }
}
