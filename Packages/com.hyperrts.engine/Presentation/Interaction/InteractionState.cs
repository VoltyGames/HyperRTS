using HyperRTS.Simulation.Orders;

namespace HyperRTS.Presentation.Interaction
{
    /// <summary>The client's modes that Escape undoes, in the order it undoes them.</summary>
    public struct InteractionState
    {
        public PlacementState Placement;
        public PendingCommand Pending;
        public CommandCardPage Page;

        /// <summary>
        /// Finishes a wall being drawn, else cancels a building placement, else a targeted command waiting for a click,
        /// else turns the command card back to its first page; false when there was nothing to undo.
        /// </summary>
        public bool Cancel()
        {
            if (Placement.Drawing)
            {
                Placement.Drawing = false;
                return true;
            }

            if (Placement.Active)
            {
                Placement = new PlacementState();
                return true;
            }

            if (Pending.Type != CommandType.None)
            {
                Pending = new PendingCommand();
                return true;
            }

            if (Page.Page > 0)
            {
                Page = new CommandCardPage();
                return true;
            }

            return false;
        }
    }
}
