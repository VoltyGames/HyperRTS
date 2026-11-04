namespace HyperRTS.Simulation.Vision.Sight
{
    /// <summary>
    /// One fog cell of <see cref="SightBlockers"/>: its ground and what blocks sight on it, packed together because a
    /// sight line reads them all at every step. Heights are in <see cref="SightBlockers.Step"/>s above the ground,
    /// densities in <see cref="SightBlockers.DensityStep"/>s.
    /// </summary>
    public struct SightCell
    {
        /// <summary>Terrain height at the cell's centre, so sight lines needn't sample the heightfield.</summary>
        public float Ground;

        public byte Solid;
        public byte Porous;
        public byte PorousTop;

        /// <summary>Crop cover from the map's <see cref="CropCover"/>, cleared where vehicles flatten it.</summary>
        public byte Crop;

        public byte CropTop;

        /// <summary>Set where a block conceals, such as ruins and trenches; woods and crops conceal too.</summary>
        public byte Conceals;

        public readonly float SolidHeight => Solid * SightBlockers.Step;

        public readonly bool IsConcealing => (Conceals | Porous | Crop) != 0;

        /// <summary>Share of sight lost crossing <paramref name="length"/> metres at <paramref name="height"/> above the ground.</summary>
        public readonly float Loss(float height, float length)
        {
            var porous = height < PorousTop * SightBlockers.Step ? Porous : 0;
            var crop = height < CropTop * SightBlockers.Step ? Crop : 0;
            return (porous + crop) * SightBlockers.DensityStep * length;
        }

        /// <summary>Whether sight through the cell differs from <paramref name="other"/>'s, crops aside.</summary>
        public readonly bool BlocksUnlike(in SightCell other)
        {
            if (Solid != other.Solid)
            {
                return true;
            }

            return Porous != other.Porous || PorousTop != other.PorousTop;
        }

        /// <summary>Drops the blocks stamped on the cell; its ground and crops stay.</summary>
        public void ClearBlocks()
        {
            Solid = 0;
            Porous = 0;
            PorousTop = 0;
            Conceals = 0;
        }
    }
}
