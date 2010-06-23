using System;
using System.Collections.Generic;
using System.Text;
using CubeHags.common;
using SlimDX;
using CubeHags.client.sound;

namespace CubeHags.client.game
{
    public class Cannon
    {
        float CoolDown = 150f;
        float BulletVelocity = 700f;
        float BulletRadius = 15f;
        int lastFire = Common.Instance.frameTime;
        Sound fireSound;

        public Cannon()
        {
            fireSound = SoundManager.Instance.GetSound("cannon");
        }

        public Bullet Fire(Vector2 Position, Vector2 WeaponAngle, Vector2 PlanetVelocity)
        {
            // Not time
            if (lastFire + CoolDown > Common.Instance.frameTime)
                return null;

            if (fireSound != null)
                fireSound.Play();

            Vector2 Direction = WeaponAngle;
            Direction.Y *= -1f;
            Direction.Normalize();
            Direction = Vector2.Multiply(Direction, BulletVelocity);
            Direction += PlanetVelocity;
            Bullet bullet = new Bullet(Position, Direction, BulletRadius);
            lastFire = Common.Instance.frameTime;
            return bullet;
            
        }
    }
}
