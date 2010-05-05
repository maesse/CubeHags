using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX.Direct3D9;
using System.Drawing;

namespace CubeHags.client.gfx
{
    public class HagsTexture : IResettable
    {
        private Texture _texture;
        public Texture Texture { get { return _texture; } set { _texture = value; } }
        public ushort MaterialID = TextureManager.Instance.NextMaterialID; // Unique identifier
        private string filename;
        public Size Size;

        public HagsTexture(string filename)
        {
            _texture = TextureManager.Instance.LoadTexture(filename);
            if (_texture != null)
                Size = new Size(_texture.GetLevelDescription(0).Width, _texture.GetLevelDescription(0).Height);
            this.filename = filename;
            TextureManager.Instance.hagsTextures.Add(this);
        }

        public SlimDX.Result OnLostDevice()
        {
            if (_texture != null && !_texture.Disposed && _texture.IsDefaultPool)
            {
                _texture.Dispose();
            }
            return new SlimDX.Result();
        }

        public SlimDX.Result OnResetDevice()
        {
            if ((_texture != null && _texture.Disposed) || _texture == null)
            {
                _texture = TextureManager.Instance.LoadTexture(filename);
            }
            return new SlimDX.Result();
        }
    }
}
