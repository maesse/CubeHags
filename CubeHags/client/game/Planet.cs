using System;
using System.Collections.Generic;
using System.Text;
using SlimDX;
using CubeHags.client.gfx;
using CubeHags.client.render;
using System.Drawing;
using SlimDX.Direct3D9;
using CubeHags.client.sound;

namespace CubeHags.client.game
{
    public class Planet
    {
        public float Radius;
        public Vector2 Position;
        public Vector2 Velocity;
        public HagsTexture Texture;
        public Vector2 wishVel = Vector2.Zero; // Used for simulating user input
        public float MaximumSpeed = 1f;
        Sound HitSound;

        float pm_friction = 5f;
        float pm_accelerate = 5f;

        public Planet(float Radius, Vector2 pos)
        {
            this.Radius = Radius;
            this.Position = pos;
            HitSound = SoundManager.Instance.GetSound("explosion");
        }

        // Create drawcall for this planet
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

        public void Hit(Vector2 HitSourcePosition)
        {
            float radLow = 20f;
            float radMid = 100f;
            float radLarge = 200f;

            HitSound.Play();

            if (Radius < radLow)
            {
                // Remove
                Remove(true);
                return;
            }
            else
            {
                Vector2 deltaPos = Position - HitSourcePosition;
                
                
                // Split up into smaller plannets
                float newRad = Radius / 2f;
                Planet[] planets = new Planet[2];
                

                // Remove
                Remove(false);
            }
            
        }

        public void Remove(bool animate)
        {

        }

        public void Update(float time)
        {
            // Velocity after friction has been applied
            Vector2 newVel = Friction(time);
            
            // Wanted velocity
            Vector2 wishdir = new Vector2(wishVel.X, wishVel.Y);
            float wishspeed = wishdir.Length() * MaximumSpeed;
            wishdir.Normalize();

            // Apply acceleration
            Velocity = newVel = Accelerate(wishdir, wishspeed, pm_accelerate, newVel, time);

            // Move towards position
            Position = ViewParams.VectorMA(Position, time, newVel);

            if (Velocity.Equals(Vector2.Zero))
                return;

            // Do collision detection
            Planet plan;
            if ((plan = GameWorld.Instance.TestHasCollision(this, true)) != null)
            {
                plan.wishVel = Vector2.Zero;
                plan.Velocity = Vector2.Zero;
                wishVel = Vector2.Zero;
                Velocity = Vector2.Zero;
            }

            // Don't let the planet leave the game arena
            if (Position.X - Radius <= 0f || Position.X + Radius >= GameWorld.Instance.GameArenaSize.X )
            {
                // Touching arena side on the X-Axis
                wishVel.X = wishVel.X * -1;
                Velocity.X = Velocity.X * -1;
            }
            if(Position.Y - Radius <= 0f || Position.Y + Radius >= GameWorld.Instance.GameArenaSize.Y) 
            {
                // Touching arena side on the Y-Axis
                wishVel.Y = wishVel.Y * -1;
                Velocity.Y = Velocity.Y * -1;
            }
        }

        // Handles ground and water friction
        private Vector2 Friction(float delta)
        {
            Vector2 velocity = Velocity;

            // Check current speed
            float speed = velocity.Length();
            if (speed < 1f)
            {
                velocity[0] = 0;
                velocity[1] = 0;
                return velocity;
            }

            // Calculate speed-drop
            float drop = speed * pm_friction * delta;

            float newspeed = speed - drop;
            if (newspeed < 0f)
                newspeed = 0;
            newspeed /= speed;

            // Apply drop
            velocity[0] *= newspeed;
            velocity[1] *= newspeed;

            return velocity;
        }

        // Q2 style acceleration
        private Vector2 Accelerate(Vector2 wishdir, float wishspeed, float accel, Vector2 vel, float delta)
        {
            // Calculate requested added speed
            float currentspeed = Vector2.Dot(vel, wishdir);
            float addspeed = wishspeed - currentspeed;
            if (addspeed <= 0f)
                return vel;

            // Calculate allowed added speed
            float accelspeed = accel * delta * wishspeed;
            if (accelspeed > addspeed)
                accelspeed = addspeed;

            // Apply
            vel += accelspeed * wishdir;
            return vel;
        }
    }
}
