namespace HyperRTS.Presentation.Animation
{
    /// <summary>Watches a count (shots, releases) and tells whether it changed within a number of seconds.</summary>
    public struct ChangeTimer
    {
        public uint Value;

        /// <summary>Seconds since <see cref="Value"/> changed; infinite until it first does.</summary>
        public float Since;

        /// <summary>False until the first count is read, so a count already high when first seen is not a change.</summary>
        public bool Started;

        /// <summary>Takes this frame's count; true when it differs from the last one.</summary>
        public bool Update(uint value, float deltaTime)
        {
            if (!Started)
            {
                Started = true;
                Value = value;
                Since = float.PositiveInfinity;
                return false;
            }

            var changed = value != Value;
            Value = value;
            Since = changed ? 0f : Since + deltaTime;
            return changed;
        }

        public readonly bool Within(float seconds) => Since < seconds;
    }
}
