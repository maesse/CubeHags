using System;
using System.Collections.Generic;
using System.Text;
using SlimDX;
using CubeHags.client.render;
using CubeHags.client.gfx;
using SlimDX.Direct3D9;
using System.Drawing;

namespace CubeHags.client.game
{
    // A bullet in space
    public class Bullet
    {
        public Vector2 Position;
        public float Radius;
        public Vector2 Velocity;

        HagsTexture Texture;

        public Bullet(Vector2 position, Vector2 Velocity, float Radius)
        {
            this.Position = position;
            this.Velocity = Velocity;
            this.Radius = Radius;
            Texture = new HagsTexture("gfx/pellet.png");
        }

        // Returns true if bullet should be removed
        public bool Update(float delta)
        {
            Vector2 endPos = new Vector2( Velocity.X, Velocity.Y);
            Position = ViewParams.VectorMA(Position, delta, endPos);

            if (Position.X <= 0f || Position.Y <= 0f || Position.X > 10000 || Position.Y > 10000)
                return true;

            Planet planet = GameWorld.Instance.TestHasCollision(this);
            if (planet != null)
            {
                return true;
            }

            return false;
        }

        public void Render()
        {
            int vertStartOffset = GameWorld.Instance.VertexList.Count;

            GameWorld.Instance.VertexList.AddRange(MiscRender.GetQuadPoints2(new RectangleF(new PointF((Position.X - Radius), (Position.Y + Radius)), new SizeF((Radius * 2f), (Radius * 2f)))));

            ulong key = SortItem.GenerateBits(SortItem.FSLayer.HUD, SortItem.Viewport.DYNAMIC, SortItem.VPLayer.WORLD, SortItem.Translucency.NORMAL, Texture.MaterialID, 0, 0, GameWorld.Instance.vb.VertexBufferID);
            int nPrimitives = (GameWorld.Instance.VertexList.Count - vertStartOffset) / 3;
            RenderDelegate del = new RenderDelegate((effect, device, setMaterial) =>
            {
                if (setMaterial)
                    device.SetTexture(0, Texture.Texture);

                device.DrawPrimitives(PrimitiveType.TriangleList, vertStartOffset, nPrimitives);
            });

            GameWorld.Instance.renderCalls.Add(new KeyValuePair<ulong, RenderDelegate>(key, del));
        }
    }
}
