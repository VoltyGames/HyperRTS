namespace HyperRTS.Presentation.HUD.Commands
{
    /// <summary>How a <see cref="CommandButton"/> presents itself.</summary>
    public enum CommandLook
    {
        /// <summary>A picture filling the button, with initials and the name when there is no texture.</summary>
        Picture,

        /// <summary>A short caption.</summary>
        Text,

        /// <summary>A caption under a chevron, to turn the card's page.</summary>
        Page,
    }
}
