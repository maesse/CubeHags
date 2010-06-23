using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using CubeHags.common;
using SlimDX;
using CubeHags.client.gfx;
using CubeHags.client.map.Source;
using CubeHags.client.render;
using SlimDX.Direct3D9;
using CubeHags.client.sound;

namespace CubeHags.client.game
{
    public sealed class GameWorld : RenderGroup
    {
        // Singleton implementation
        static readonly GameWorld _Instance = new GameWorld();
        public static GameWorld Instance {get {return _Instance;}}
        // List of textures to use for planets
        List<HagsTexture> PlanetTextures = new List<HagsTexture>();
        bool Loaded = false;

        // Startup planet generation parameters
        int START_PLANETS = 400;
        int MAX_RADIUS = 100;
        int MIN_RADIUS = 40;

        public Vector2 GameArenaSize = new Vector2(10000, 10000); // Size of the game arena
        public List<Planet> Planets = new List<Planet>(); // List of spawned planets
        public List<Bullet> Bullets = new List<Bullet>();
        List<Bullet> deadBullets = new List<Bullet>();

        public PlayerPlanet Player = new PlayerPlanet(100, new SlimDX.Vector2(0, 0)); // User controlled planet
        public Vector2 CameraView = new Vector2(0, 0); // Centered on Player
        
        // Render buffers
        public List<VertexPosTex> VertexList = new List<VertexPosTex>();
        public List<KeyValuePair<ulong, RenderDelegate>> renderCalls = new List<KeyValuePair<ulong, RenderDelegate>>();

        // Smooth zooming
        public float ZoomAmount = 0.5f; // Raw zoom value updated by mouse events
        float RealZoomAmount = 0.0001f; // Smoothed zoom value, approaching ZoomValue over time
        float VelocityScale = 1f; // Velocity zooming value
        float cameraScale = 0f; // Actual camera zooming

        // Starfield
        HagsTexture smallStars;
        HagsTexture bigStars;
        // Arena border
        HagsAtlas arenaBorder;

        GameWorld()
        {
            LoadRessources();
            InitWorld();

            vb = new HagsVertexBuffer();
            base.Init();
            Loaded = true;
        }

        // Render GameArena "walls"
        void R_Arena()
        {
            int vertStartOffset = VertexList.Count;

            VertexPosTex[] verts = MiscRender.GetQuadPoints2(new RectangleF(new PointF(0, GameArenaSize.Y), new SizeF(GameArenaSize.X, GameArenaSize.Y)));
            VertexList.AddRange(verts);

            ulong key = SortItem.GenerateBits(SortItem.FSLayer.HUD, SortItem.Viewport.DYNAMIC, SortItem.VPLayer.WORLD, SortItem.Translucency.NORMAL, arenaBorder.Texture.MaterialID, 0, 0, vb.VertexBufferID);
            int nPrimitives = (VertexList.Count - vertStartOffset) / 3;
            RenderDelegate del = new RenderDelegate((effect, device, setMaterial) =>
            {
                device.SetTexture(0, arenaBorder.Texture.Texture);
                device.DrawPrimitives(PrimitiveType.TriangleList, vertStartOffset, nPrimitives);
            });
            renderCalls.Add(new KeyValuePair<ulong, RenderDelegate>(key, del));
        }

        // Render far-away stars
        void R_SmallStars()
        {
            int vertStartOffset = VertexList.Count;
            float movescale = 0.02f;
            VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF((-Player.Position.X * movescale), (Player.Position.Y * movescale) - GameArenaSize.Y), new SizeF(GameArenaSize.X * 2, GameArenaSize.Y * 2)), new RectangleF(PointF.Empty, new SizeF(GameArenaSize.X * 2, GameArenaSize.Y * 2)), smallStars.Size));
            // Fuzzy cloud-ish effect..
            VertexList.AddRange(MiscRender.GetQuadPoints2(new RectangleF(new PointF( - (Player.Position.X * movescale), (GameArenaSize.Y) - (Player.Position.Y * movescale)), new SizeF(GameArenaSize.X * 2, GameArenaSize.Y * 2)), new RectangleF(PointF.Empty, new SizeF(GameArenaSize.X * 2, GameArenaSize.Y * 2)), smallStars.Size));

            ulong key = SortItem.GenerateBits(SortItem.FSLayer.HUD, SortItem.Viewport.DYNAMIC, SortItem.VPLayer.SKYBOX, SortItem.Translucency.NORMAL, smallStars.MaterialID, 0, 0, vb.VertexBufferID);
            int nPrimitives = (VertexList.Count - vertStartOffset) / 3;
            RenderDelegate del = new RenderDelegate((effect, device, setMaterial) =>
            {
                device.SetTexture(0, smallStars.Texture);
                device.DrawPrimitives(PrimitiveType.TriangleList, vertStartOffset, nPrimitives);
            });
            renderCalls.Add(new KeyValuePair<ulong, RenderDelegate>(key, del));
        }

        // Render bigger layer of stars
        void R_BigStars()
        {
            int vertStartOffset = VertexList.Count;
            float movescale = 0.1f;

            // After ~50% zoom-in, ease of the scaling factor
            float scale = RealZoomAmount;
            if (scale > 0.4f)
                scale = 0.4f + ((scale - 0.4f) / 2f);
            scale += VelocityScale / 4; // Add in a bit of velocity scaling
            
            VertexPosTex[] verts = MiscRender.GetQuadPoints(new RectangleF(new PointF(-Player.Position.X * movescale-5000, Player.Position.Y * movescale-5000), new SizeF(GameArenaSize.X+10000, GameArenaSize.Y+10000)), new RectangleF(PointF.Empty, new SizeF(GameArenaSize.X, GameArenaSize.Y)), smallStars.Size);

            // Apply scaling to vertices
            Matrix trans = Matrix.Scaling(scale, scale, 1f);
            for (int i = 0; i < verts.Length; i++)
            {
                Vector4 vec = Vector3.Transform(verts[i].Position, trans);
                verts[i].Position.X = vec.X;
                verts[i].Position.Y = vec.Y;
            }

            VertexList.AddRange(verts);
            
            // Rendercall
            ulong key = SortItem.GenerateBits(SortItem.FSLayer.HUD, SortItem.Viewport.DYNAMIC, SortItem.VPLayer.SKYBOX, SortItem.Translucency.NORMAL, smallStars.MaterialID, 0, 0, vb.VertexBufferID);
            int nPrimitives = (VertexList.Count - vertStartOffset) / 3;
            RenderDelegate del = new RenderDelegate((effect, device, setMaterial) =>
            {
                device.SetTexture(0, bigStars.Texture);
                device.DrawPrimitives(PrimitiveType.TriangleList, vertStartOffset, nPrimitives);
            });
            renderCalls.Add(new KeyValuePair<ulong, RenderDelegate>(key, del));
        }

        public void Frame(Input.UserCommand cmd, float delta)
        {
            if (!Loaded)
                return;
            // Clear up from last frame
            renderCalls.Clear();
            VertexList.Clear();

            // Render starfield
            R_SmallStars();
            R_BigStars();
            R_Arena();

            // Update and update other planets
            for (int i = 0; i < Planets.Count; i++)
            {
                Planets[i].Update(delta);
                // Check visibility
                //if(PlanetIsVisible(Planets[i]))
                Planets[i].Render();
            }

            // handle player input
            Player.UpdatePlayer(cmd, delta);
            // Render player planet
            Player.Render();

            // Update and draw bullets
            for (int i = 0; i < Bullets.Count; i++)
            {
                // Test for remove
                if (Bullets[i].Update(delta))
                    deadBullets.Add(Bullets[i]);
                else
                    Bullets[i].Render();
            }

            // Remove dead bullets
            if (deadBullets.Count > 0)
            {
                for (int i = deadBullets.Count-1; i > 0; i--)
                {
                    Bullets.Remove(deadBullets[i]);
                }
                deadBullets.Clear();
            }

            // Send rendercalls to renderer
            if (VertexList.Count > 0)
            {
                vb.SetVB<VertexPosTex>(VertexList.ToArray(), VertexList.Count * VertexPosTex.SizeInBytes, VertexPosTex.Format, Usage.WriteOnly);
                Renderer.Instance.drawCalls.AddRange(renderCalls);
            }

            // Set camera view
            CameraView = new Vector2(-Player.Position.X , -Player.Position.Y );
            cameraScale = UpdateCameraZoom(delta);
        }

        bool PlanetIsVisible(Planet planet)
        {
            // Visible area
            RectangleF rect = new Rectangle(new Point(-MAX_RADIUS, -MAX_RADIUS), new Size(Renderer.Instance.RenderSize.Width+MAX_RADIUS, Renderer.Instance.RenderSize.Height+MAX_RADIUS));
            rect.X += CameraView.X;
            rect.Y += CameraView.Y;
            return PlanetHitsRect(planet, rect);
        }

        bool PlanetHitsRect(Planet planet, RectangleF rect)
        {
            return !(planet.Position.X < rect.X ||
                planet.Position.X > rect.X + rect.Width ||
                planet.Position.Y < rect.Y ||
                planet.Position.Y > rect.Y + rect.Height);
        }

        // Finds a spawnpoint for a planet
        bool Spawn(Planet planet)
        {
            // Get valid random spawn position
            Vector2 randomPos;
            int tries = 0;
            do
            {
                // Give up after 20 tries
                if (tries++ == 20)
                    return false;

                randomPos = new Vector2((float)Common.Rand.Next((int)(planet.Radius),(int)(GameArenaSize.X-planet.Radius)), (float)Common.Rand.Next((int)(planet.Radius),(int)(GameArenaSize.Y-planet.Radius)));
                planet.Position = randomPos;
                planet.wishVel = new Vector2((float)Common.Rand.NextDouble() - 0.5f, (float)Common.Rand.NextDouble() - 0.5f);
                planet.MaximumSpeed = Common.Rand.Next(50, 200);
            } 
            while (TestHasCollision(planet, false) != null);

            // Got valid spawnpoint
            return true;
        }

        public Planet TestHasCollision(Bullet bullet)
        {
            // Test collision against all planets
            Planet plan;
            for (int i = 0; i < Planets.Count; i++)
            {
                plan = Planets[i];

                if (TestPlanetsHasCollision2(bullet, plan))
                    return plan;
            }
            return null;
        }

        public Planet TestHasCollision(Planet planet, bool clipPlayer)
        {
            // Test collision agains player
            if (!clipPlayer)
                if (TestPlanetsHasCollision(planet, Player))
                    return Player;

            // Test collision against all planets
            Planet plan;
            for (int i = 0; i < Planets.Count; i++)
            {
                plan = Planets[i];
                // Ignore planet if it has the same position (== the same planet)
                if (plan.Position.Equals(planet.Position))
                    continue;

                if (TestPlanetsHasCollision(planet, plan))
                    return plan;
            }
            return null;
        }

        // Do Circle intersection test
        bool TestPlanetsHasCollision2(Bullet a, Planet b)
        {
            Vector2 delta = Vector2.Subtract(a.Position, b.Position);
            if (delta.Length() < b.Radius + a.Radius)
                return true;

            return false;
        }

        // Do Circle intersection test
        bool TestPlanetsHasCollision(Planet a, Planet b)
        {
            Vector2 delta = Vector2.Subtract(a.Position, b.Position);
            if (delta.Length() < b.Radius + a.Radius)
                return true;

            return false;
        }

        // Handle zoom amount updating
        float UpdateCameraZoom(float delta)
        {
            // Scrollwheel Zoom smoothing
            float val = delta/0.2f; // Smooth zoom over 0.2s
            Vector2 real = Vector2.Lerp(new Vector2(RealZoomAmount), new Vector2(ZoomAmount), val);
            RealZoomAmount = real.X;

            // Velocity zooming +smoothing
            float maxScale = 1.3f;
            float scale = maxScale - ((Player.Velocity.Length() / Player.MaxSpeed) * (maxScale - 1f));
            real = Vector2.Lerp(new Vector2(VelocityScale), new Vector2(scale), (delta/0.1f));
            scale = VelocityScale = real.X;

            scale *= RealZoomAmount;
            return scale;
        }

        // Prepare the renderer for World rendercalls
        internal void SetupRender(Device device, Effect effect)
        {
            // Set camera position
            float scale = cameraScale;
            Vector2 cam = new Vector2(CameraView.X +(Renderer.Instance.RenderSize.Width/2), CameraView.Y + ((float)Renderer.Instance.RenderSize.Height/2f) );
            
            // Set World * View * Projection
            Matrix worldViewProj = Matrix.Transformation2D(new Vector2(Renderer.Instance.RenderSize.Width / 2f, Renderer.Instance.RenderSize.Height / 2f), 0f, new Vector2(scale, scale), Vector2.Zero, 0f, Vector2.Zero) * Matrix.Translation(Vector3.Multiply(new Vector3(cam.X, cam.Y, 0f), scale)) * Matrix.OrthoOffCenterRH(0f, (float)Renderer.Instance.RenderSize.Width, 0f, (float)Renderer.Instance.RenderSize.Height, 0.0f, 10.0f);
            effect.SetValue("WorldViewProj", worldViewProj);
            effect.Technique = "WorldAlpha";
        }

        // Prepare the renderer for Skybox (stars) rendercalls
        public void SetupStarsRender(Device device, Effect effect)
        {
            effect.Technique = "WorldAlpha";
            Matrix worldViewProj = Matrix.Identity;
            effect.SetValue("WorldViewProj", worldViewProj);
        }

        public HagsTexture GetRandomPlanetTexture()
        {
            int i = Common.Rand.Next(PlanetTextures.Count);
            return PlanetTextures[i];
        }

        void LoadRessources()
        {
            SoundManager.Instance.LoadSound("sound/cannon.mp3", "cannon");
            SoundManager.Instance.LoadSound("sound/dead.mp3", "dead");
            SoundManager.Instance.LoadSound("sound/explosion.mp3", "explosion");
            PlanetTextures.Add(new HagsTexture("gfx/planets/1.png"));
            PlanetTextures.Add(new HagsTexture("gfx/planets/2.png"));
            PlanetTextures.Add(new HagsTexture("gfx/planets/3.png"));
            smallStars = new HagsTexture("gfx/starssmall.png");
            bigStars = new HagsTexture("gfx/starsbig.png");
            arenaBorder = new HagsAtlas("gfx/arenaborder.png");
            //arenaBorder["topleft"] = new Rectangle(0, 0, 8, 8);
            //arenaBorder["left"] = new Rectangle(0, 8, 8, 112);
            //arenaBorder["bottomleft"] = new Rectangle(0, 120, 8, 8);
            //arenaBorder["top"] = new Rectangle(8, 0, 8, 8);
            //arenaBorder["bottom"] = new Rectangle(8, 120, 8, 8);
            //arenaBorder["topright"] = new Rectangle(120, 0, 8, 8);
            //arenaBorder["right"] = new Rectangle(120, 8, 8, 8);
            //arenaBorder["bottomright"] = new Rectangle(120, 120, 8, 8);
        }

        // Setup the initial world
        void InitWorld()
        {
            // Spawn planets
            for (int i = 0; i < START_PLANETS; i++)
            {
                Planet planet = new Planet(Common.Rand.Next(MIN_RADIUS, MAX_RADIUS), Vector2.Zero);
                if (Spawn(planet))
                {
                    planet.Texture = GetRandomPlanetTexture();
                    Planets.Add(planet);
                }
                else
                    break; // No more room in the arena
            }
            Player.Texture = new HagsTexture("gfx/earth.png");
        }
    }
}
