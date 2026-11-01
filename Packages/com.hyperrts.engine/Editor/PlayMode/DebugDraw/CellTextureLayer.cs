using Unity.Entities;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Base for layers that shade a grid's cells on the ground through one <see cref="CellTexture"/>.</summary>
    public abstract class CellTextureLayer : DebugLayer
    {
        private readonly CellTexture _texture = new();

        public sealed override void Draw(EntityManager entityManager)
        {
            if (Paint(entityManager, _texture))
            {
                _texture.Draw();
            }
        }

        /// <summary>Paints the texture from the world's arrays; false when there is nothing to show yet.</summary>
        protected abstract bool Paint(EntityManager entityManager, CellTexture texture);
    }
}
