using UnityEngine;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>One horizon field standing raised at its crop's height: its area inside the margins, in the grid.</summary>
    public struct HorizonRaisedField
    {
        /// <summary>Grid metres, x along the rows.</summary>
        public Rect area;

        /// <summary>Index of the crop in <see cref="HorizonFields.crops"/>.</summary>
        public int crop;
    }
}
