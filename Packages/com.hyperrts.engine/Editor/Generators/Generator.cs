namespace HyperRTS.Editor.Generators
{
    /// <summary>
    /// One step that (re)creates generated assets. Subclass it (engine or game) and it appears in Tools ▸ Generate;
    /// <see cref="GeneratorRunner.RunAll"/> runs every available one in <see cref="Order"/>.
    /// </summary>
    public abstract class Generator
    {
        public abstract string Name { get; }

        /// <summary>Heading the step is listed under (the engine or the game's name).</summary>
        public abstract string Group { get; }

        /// <summary>Run position, lowest first; steps that use another's output come after it.</summary>
        public abstract int Order { get; }

        /// <summary>One line on what the step makes.</summary>
        public abstract string Description { get; }

        /// <summary>What the step reads: paths, or a short description when they are many.</summary>
        public abstract string Sources { get; }

        /// <summary>What the step writes: paths, or a short description when they are many. Never edit these by hand.</summary>
        public abstract string Outputs { get; }

        /// <summary>False hides the step, e.g. engine steps that write into a read-only package.</summary>
        public virtual bool Available => true;

        public abstract void Run();
    }
}
