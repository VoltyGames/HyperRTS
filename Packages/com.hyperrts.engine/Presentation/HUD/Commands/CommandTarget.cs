namespace HyperRTS.Presentation.HUD.Commands
{
    /// <summary>What a <see cref="CommandSpec"/> is for.</summary>
    public enum CommandTarget
    {
        /// <summary>A prefab a builder places or a producer trains or researches.</summary>
        Prefab,

        /// <summary>An ability or support power, by name.</summary>
        Ability,

        /// <summary>A plain order such as Retreat or a stance.</summary>
        Order,
    }
}
