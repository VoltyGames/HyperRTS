namespace HyperRTS.Simulation.Production;

/// <summary>Why <see cref="Buyer.FindRefusal"/> refuses a purchase; locks come before the price.</summary>
public enum PurchaseRefusal : byte
{
    None,

    /// <summary>
    /// The player has as many as its <see cref="BuildLimit"/> allows; for research, it is finished or queued.
    /// </summary>
    AtLimit,

    /// <summary>A required building or research is missing.</summary>
    MissingPrerequisite,

    CannotAfford,
}
