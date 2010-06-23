using System;
using System.Collections.Generic;
using System.Text;
using SlimDX;
using CubeHags.client.gfx;
using CubeHags.client.render;
using SlimDX.Direct3D9;
using System.Drawing;
using CubeHags.client.map.Source;
using CubeHags.client.sound;

namespace CubeHags.client.game
{
    public class PlayerPlanet : Planet
    {
        public float MaxSpeed = 400;
        public Vector2 WeaponVector = new Vector2(1, 0);
        
        HagsTexture CannonTexture;
        Cannon Cannon;

        Vector2 forw = new Vector2(0, -1);
        Vector2 side = new Vector2(-1f, 0);
        static Vector2 xAxis = new Vector2(1, 0);
        Vector2 GunPoint = new Vector2(0, 0);

        public PlayerPlanet(float radius, Vector2 pos) : base(radius, pos)
        {
            CannonTexture = new HagsTexture("gfx/cannon.png");
            Position = new Vector2(10000 / 2, 10000 / 2);
            Cannon = new Cannon();
        }

        void Die()
        {
            SoundManager.Instance.GetSound("dead").Play();
        }

        // Update player input and weapon placement
        public void UpdatePlayer(Input.UserCommand cmd, float delta)
        {
            wishVel.X = wishVel.Y = 0f;

            float scale = CommandScale(cmd);
            if (scale != 0f)
            {
                for (int i = 0; i < 2; i++)
                {
                    wishVel[i] = (scale * forw[i] * cmd.forwardmove) + (scale * side[i] * cmd.rightmove);
                }
            }

            // Handle weapon movement
            float weaponSens = 40f;
            WeaponVector = Vector2.Multiply(WeaponVector, weaponSens);
            WeaponVector.X += cmd.DX;
            WeaponVector.Y += cmd.DY;
            WeaponVector.Normalize();

            // Handle shooting
            if ((cmd.buttons & 1) == 1)
            {
                Bullet bullet = Cannon.Fire(Position + GunPoint, WeaponVector, Velocity);
                if (bullet != null)
                    GameWorld.Instance.Bullets.Add(bullet);
            }

            base.Update(delta);
        }

        // Get angle from vector to X-Axis
        public static float VectorToAngle(Vector2 v)
        {
            return (float)Math.Atan2(-v.Y, Vector2.Dot(v, xAxis));
        }

        public Vector2 TurnVector(Vector2 v, float a)
        {
            Vector2 u = new Vector2();
            u.X = (v.X * (float)Math.Cos(a)) - (v.Y * (float)Math.Sin(a));
            u.Y = (v.X * (float)Math.Sin(a)) + (v.Y * (float)Math.Cos(a));

        }

        public new void Render()
        {
            // Planet
            int vertStartOffset1 = GameWorld.Instance.VertexList.Count;
            GameWorld.Instance.VertexList.AddRange(MiscRender.GetQuadPoints2(new RectangleF(new PointF((Position.X - Radius), (Position.Y + Radius)), new SizeF((Radius * 2), (Radius * 2)))));
            int nPrimitives1 = (GameWorld.Instance.VertexList.Count - vertStartOffset1) / 3;

            // Weapon
            RectangleF GunRect = new RectangleF(Radius*0.9f, (CannonTexture.Size.Height/2), CannonTexture.Size.Width, CannonTexture.Size.Height);
            VertexPosTex[] v2 = MiscRender.GetQuadPoints2(GunRect);
            // Center of rotation
            Vector2 GunCenter = new Vector2(0, GunRect.Y - (GunRect.Height/2));
            // Rotate and translate to the center of the planet
            Matrix rotMatrix = Matrix.Transformation2D(GunCenter, 0f, new Vector2(1f, 1f), GunCenter, VectorToAngle(WeaponVector), Position);
            // Apply transforms
            for (int i = 0; i < v2.Length; i++)
                v2[i].Position = Vector3.TransformCoordinate(v2[i].Position, rotMatrix);
            int vertStartOffset2 = GameWorld.Instance.VertexList.Count;
            GameWorld.Instance.VertexList.AddRange(v2);
            int nPrimitives2 = (GameWorld.Instance.VertexList.Count - vertStartOffset2) / 3;
            GunPoint = Vector2.TransformCoordinate(new Vector2(Radius * 0.9f + CannonTexture.Size.Width, 0), Matrix.Transformation2D(GunCenter, 0f, new Vector2(1f, 1f), GunCenter, VectorToAngle(WeaponVector), Vector2.Zero));
            // Render delegate
            ulong key = SortItem.GenerateBits(SortItem.FSLayer.HUD, SortItem.Viewport.DYNAMIC, SortItem.VPLayer.WORLD, SortItem.Translucency.NORMAL, Texture.MaterialID, 0, 0, GameWorld.Instance.vb.VertexBufferID);
            RenderDelegate del = new RenderDelegate((effect, device, setMaterial) =>
            {
                // Planet
                device.SetTexture(0, Texture.Texture);
                device.DrawPrimitives(PrimitiveType.TriangleList, vertStartOffset1, nPrimitives1);

                // Weapon
                device.SetTexture(0, CannonTexture.Texture);
                device.DrawPrimitives(PrimitiveType.TriangleList, vertStartOffset2, nPrimitives2);
            });
            GameWorld.Instance.renderCalls.Add(new KeyValuePair<ulong, RenderDelegate>(key, del));
        }

        // Scale input command values
        private float CommandScale(CubeHags.client.Input.UserCommand cmd)
        {
            int max = Math.Abs((int)cmd.forwardmove);
            if (Math.Abs((int)cmd.rightmove) > max)
            {
                max = Math.Abs((int)cmd.rightmove);
            }
            if (Math.Abs((int)cmd.upmove) > max)
                max = Math.Abs((int)cmd.upmove);

            if (max == 0)
                return 0f;

            float total = (float)Math.Sqrt(cmd.forwardmove * cmd.forwardmove + cmd.rightmove * cmd.rightmove + cmd.upmove * cmd.upmove);
            float scale = (float)MaxSpeed * max / (127f * total);
            return scale;
        }
    }
}
