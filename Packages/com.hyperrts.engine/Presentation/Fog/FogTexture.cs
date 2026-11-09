using HyperRTS.Simulation.Vision.Fog;
using System;
using Unity.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>
    /// One byte per fog cell as a texture, refilled from the local team's fog whenever its view changes. An eased
    /// texture fills a target array and fades toward it; a plain one is filled directly.
    /// </summary>
    public sealed class FogTexture : IDisposable
    {
        private readonly string _name;
        private readonly TextureFormat _format;
        private readonly bool _eased;
        private NativeArray<byte> _target;
        private int _version = -1;

        // Whether the texture has reached the last fill; always true for a plain texture.
        private bool _settled = true;

        public FogTexture(string name, TextureFormat format, bool eased)
        {
            _name = name;
            _format = format;
            _eased = eased;
        }

        public Texture2D Texture { get; private set; }

        /// <summary>Whether the view changed since the last fill, or there is no texture yet.</summary>
        public bool Stale(in LocalFogView view) => Texture == null || view.Version != _version;

        /// <summary>Makes the next <see cref="Stale"/> true, as when the alphas change.</summary>
        public void Invalidate() => _version = -1;

        /// <summary>
        /// Fills from the latest stamp and returns whether the texture was created for a new grid, which shows it at
        /// once rather than fading in.
        /// </summary>
        public bool Refill(in FogOfWar fog, in LocalFogView view, byte exploredAlpha, byte unexploredAlpha)
        {
            var size = fog.Grid.Size;
            var created = Texture == null || Texture.width != size.x || Texture.height != size.y;
            if (created)
            {
                Dispose();
                Texture = new Texture2D(size.x, size.y, _format, false, true)
                {
                    name = _name,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
                if (_eased)
                {
                    _target = new NativeArray<byte>(size.x * size.y, Allocator.Persistent);
                }
            }

            if (_eased)
            {
                FogTexels.Fill(fog, view.Team, exploredAlpha, unexploredAlpha, _target);
                if (created)
                {
                    Texture.GetPixelData<byte>(0).CopyFrom(_target);
                    Texture.Apply(false);
                }

                _settled = created;
            }
            else
            {
                FogTexels.Fill(fog, view.Team, exploredAlpha, unexploredAlpha, Texture.GetPixelData<byte>(0));
                Texture.Apply(false);
            }

            _version = view.Version;
            return created;
        }

        /// <summary>Moves the texture toward the last fill, and settles once it arrives.</summary>
        public void Ease(float deltaTime, float fadeTime)
        {
            if (_settled)
            {
                return;
            }

            _settled = FogEase.Step(Texture.GetPixelData<byte>(0), _target, deltaTime, fadeTime);
            Texture.Apply(false);
        }

        public void Dispose()
        {
            Object.Destroy(Texture);
            Texture = null;
            if (_target.IsCreated)
            {
                _target.Dispose();
            }
        }
    }
}
