using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX.Direct3D9;
using System.Drawing;
using CubeHags.client.map.Source;
using SlimDX;
using CubeHags.client.render.Formats;

namespace CubeHags.client.render
{
    class MiscRender
    {
        static Rectangle DefRect =  new Rectangle() { Width = 1, Height = 1 };
        static Size DefSize = new Size(1, 1);
        private void TakeScreenshot()
        {
            // Init surfaces
            //Rectangle rect = new Rectangle(new Point(), RenderSize);
            //Surface InputSurface = RenderSurface;
            //Surface CaptureSurface = device.CreateOffscreenPlainSurface(RenderSize.Width, RenderSize.Height, Format.A8R8G8B8, Pool.SystemMemory);

            //// If multisampling, copy RenderSurface to another RenderTarget without multisampling
            //if (RenderSurface.Description.MultiSampleType != MultiSampleType.None)
            //{
            //    InputSurface = device.CreateRenderTarget(RenderSize.Width, RenderSize.Height, Format.A8R8G8B8, MultiSampleType.None, 0, true);
            //    device.StretchRectangle(RenderSurface, rect, InputSurface, rect, TextureFilter.Linear);
            //}

            //// From RenderTarget to Sysmem Texture
            //device.GetRenderTargetData(InputSurface, CaptureSurface);

            //// From Texture to Bitmap
            //GraphicsStream renderStream = CaptureSurface.LockRectangle(LockFlags.None);
            //Bitmap renderBitmap = new Bitmap(RenderSize.Width, RenderSize.Height, 4 * RenderSize.Width, System.Drawing.Imaging.PixelFormat.Format32bppArgb, renderStream.InternalData);

            //// Save bitmap
            //renderBitmap.Save("Screenshot-" + new Random().Next(123123) + ".bmp");

            //// Clean up
            //if (RenderSurface.Description.MultiSampleType != MultiSampleType.None)
            //    InputSurface.Dispose();
            //CaptureSurface.UnlockRectangle();
            //CaptureSurface.Dispose();
            //renderBitmap.Dispose();

            //DumpTonemaps();
        }

        // Save as images
        private void DumpTonemaps()
        {
            //Surface source;
            //SurfaceDescription sourceDescr;
            //Surface CaptureSurface;
            //Bitmap renderBitmap;
            //GraphicsStream gs;

            ////for (int i = 0; i < ToneMapTex.Length; i++)
            //for (int i = nToneMapTex-1; i < ToneMapTex.Length; i++)
            //{
            //    // Get surface from tonemap texture
            //    source = ToneMapTex[i].GetSurfaceLevel(0);
            //    sourceDescr = source.Description;

            //    // From RenderTarget to Sysmem Texture
            //    CaptureSurface = device.CreateOffscreenPlainSurface(sourceDescr.Width, sourceDescr.Height, Format.A8R8G8B8, Pool.SystemMemory);
            //    device.GetRenderTargetData(source, CaptureSurface);

            //    // From Texture to Bitmap
            //    gs = CaptureSurface.LockRectangle(LockFlags.None);
            //    renderBitmap = new Bitmap(sourceDescr.Width, sourceDescr.Height, 4 * sourceDescr.Width, System.Drawing.Imaging.PixelFormat.Format32bppArgb, gs.InternalData);

            //    // Save bitmap
            //    renderBitmap.Save("Tonemap"+i+"-" + new Random().Next(123123) + ".bmp");

            //    // Release ressources
            //    renderBitmap.Dispose();
            //    CaptureSurface.UnlockRectangle();
            //    CaptureSurface.Dispose();
            //}
        }

        public static VertexPositionColorTex[] GetQuadPoints(RectangleF destination, Color4 color)
        {
            return GetQuadPoints(destination, DefRect, DefSize, color);
        }

        public static VertexPositionColorTex[] GetQuadPoints(RectangleF destination)
        {
            return GetQuadPoints(destination, DefRect, DefSize, new Color4(Color.White));
        }

        public static VertexPositionColorTex[] GetQuadPoints2(RectangleF destination)
        {
            return GetQuadPoints2(destination, DefRect, DefSize);
        }

        // Used for sprites.. generates 6 vertex points
        public static VertexPositionColorTex[] GetQuadPoints2(RectangleF destination, RectangleF texture, SizeF texureSize)
        {
            return GetQuadPoints2(destination, texture, texureSize, new Color4(Color.White));
        }

        public static VertexPositionColorTex[] GetQuadPoints2(RectangleF destination, RectangleF texture, SizeF texureSize, Color4 color)
        {
            VertexPositionColorTex[] result = new VertexPositionColorTex[6];
            result[0] = new VertexPositionColorTex(new Vector3(destination.X, destination.Y, 0f), color, new Vector2((texture.X) / texureSize.Width, (texture.Y) / texureSize.Height));
            result[1] = new VertexPositionColorTex(new Vector3(destination.X, destination.Y - destination.Height, 0f), color, new Vector2((texture.X) / texureSize.Width, (texture.Y + texture.Height) / texureSize.Height));
            result[2] = new VertexPositionColorTex(new Vector3(destination.X + destination.Width, destination.Y, 0f), color, new Vector2((texture.X + texture.Width) / texureSize.Width, (texture.Y) / texureSize.Height));
            result[3] = result[2];
            result[4] = result[1];
            result[5] = new VertexPositionColorTex(new Vector3(destination.X + destination.Width, destination.Y - destination.Height, 0f), color, new Vector2((texture.X + texture.Width) / texureSize.Width, (texture.Y + texture.Height) / texureSize.Height));
            return result;
        }

        public static VertexPositionColorTex[] GetQuadPoints(RectangleF destination, RectangleF texture, SizeF texureSize)
        {
            return GetQuadPoints(destination, texture, texureSize, new Color4(Color.White));
        }
        // Used for sprites.. generates 6 vertex points
        public static VertexPositionColorTex[] GetQuadPoints(RectangleF destination, RectangleF texture, SizeF texureSize, Color4 color)
        {
            VertexPositionColorTex[] result = new VertexPositionColorTex[6];
            Size renderSize = Renderer.Instance.RenderSize;
            float fWidth5 = (0.5f / (float)renderSize.Width);// (float)rtDescr.Width - 1f;
            float fHeight5 = (-0.5f / (float)renderSize.Height);//(float)rtDescr.Height -1f;
            result[0] = new VertexPositionColorTex(GetTransformed2DPoint(destination.X, renderSize.Height - destination.Y, renderSize), color, new Vector2((texture.X) / texureSize.Width, (texture.Y) / texureSize.Height));
            result[1] = new VertexPositionColorTex(GetTransformed2DPoint(destination.X, renderSize.Height - destination.Y - destination.Height, renderSize), color, new Vector2((texture.X) / texureSize.Width, (texture.Y + texture.Height) / texureSize.Height));
            result[2] = new VertexPositionColorTex(GetTransformed2DPoint(destination.X + destination.Width, renderSize.Height - destination.Y, renderSize), color, new Vector2((texture.X + texture.Width) / texureSize.Width, (texture.Y) / texureSize.Height));
            result[3] = result[2];
            result[4] = result[1];
            result[5] = new VertexPositionColorTex(GetTransformed2DPoint(destination.X + destination.Width, renderSize.Height - destination.Y - destination.Height, renderSize), color, new Vector2((texture.X + texture.Width) / texureSize.Width, (texture.Y + texture.Height) / texureSize.Height));
            return result;
        }

        public static VertexPositionColor[] CreateBox(Vector3 mins, Vector3 maxs, Color4 color)
        {

            VertexPositionColor[] verts = new VertexPositionColor[36];

            // Front face
            verts[0] = new VertexPositionColor(new Vector3(mins.X, maxs.Y, maxs.Z), color);
            verts[1] = new VertexPositionColor(new Vector3(mins.X, mins.Y, maxs.Z), color);
            verts[2] = new VertexPositionColor(new Vector3(maxs.X, maxs.Y, maxs.Z), color);
            verts[3] = new VertexPositionColor(new Vector3(mins.X, mins.Y, maxs.Z), color);
            verts[4] = new VertexPositionColor(new Vector3(maxs.X, mins.Y, maxs.Z), color);
            verts[5] = new VertexPositionColor(new Vector3(maxs.X, maxs.Y, maxs.Z), color);

            // Back face (remember this is facing *away* from the camera, so vertices should be
            //    clockwise order)
            verts[6] = new VertexPositionColor(new Vector3(mins.X, maxs.Y, mins.Z), color);
            verts[7] = new VertexPositionColor(new Vector3(maxs.X, maxs.Y, mins.Z), color);
            verts[8] = new VertexPositionColor(new Vector3(mins.X, mins.Y, mins.Z), color);
            verts[9] = new VertexPositionColor(new Vector3(mins.X, mins.Y, mins.Z), color);
            verts[10] = new VertexPositionColor(new Vector3(maxs.X, maxs.Y, mins.Z), color);
            verts[11] = new VertexPositionColor(new Vector3(maxs.X, mins.Y, mins.Z), color);

            // Top face
            verts[12] = new VertexPositionColor(new Vector3(mins.X, maxs.Y, maxs.Z), color);
            verts[13] = new VertexPositionColor(new Vector3(maxs.X, maxs.Y, mins.Z), color);
            verts[14] = new VertexPositionColor(new Vector3(mins.X, maxs.Y, mins.Z), color);
            verts[15] = new VertexPositionColor(new Vector3(mins.X, maxs.Y, maxs.Z), color);
            verts[16] = new VertexPositionColor(new Vector3(maxs.X, maxs.Y, maxs.Z), color);
            verts[17] = new VertexPositionColor(new Vector3(maxs.X, maxs.Y, mins.Z), color);

            // Bottom face (remember this is facing *away* from the camera, so vertices should be
            //    clockwise order)
            verts[18] = new VertexPositionColor(new Vector3(mins.X, mins.Y, maxs.Z), color);
            verts[19] = new VertexPositionColor(new Vector3(mins.X, mins.Y, mins.Z), color);
            verts[20] = new VertexPositionColor(new Vector3(maxs.X, mins.Y, mins.Z), color);
            verts[21] = new VertexPositionColor(new Vector3(mins.X, mins.Y, maxs.Z), color);
            verts[22] = new VertexPositionColor(new Vector3(maxs.X, mins.Y, mins.Z), color);
            verts[23] = new VertexPositionColor(new Vector3(maxs.X, mins.Y, maxs.Z), color);

            // Left face
            verts[24] = new VertexPositionColor(new Vector3(mins.X, maxs.Y, maxs.Z), color);
            verts[25] = new VertexPositionColor(new Vector3(mins.X, mins.Y, mins.Z), color);
            verts[26] = new VertexPositionColor(new Vector3(mins.X, mins.Y, maxs.Z), color);

            verts[27] = new VertexPositionColor(new Vector3(mins.X, maxs.Y, mins.Z), color);
            verts[28] = new VertexPositionColor(new Vector3(mins.X, mins.Y, mins.Z), color);
            verts[29] = new VertexPositionColor(new Vector3(mins.X, maxs.Y, maxs.Z), color);

            // Right face (remember this is facing *away* from the camera, so vertices should be
            //    clockwise order)
            verts[30] = new VertexPositionColor(new Vector3(maxs.X, maxs.Y, maxs.Z), color);
            verts[31] = new VertexPositionColor(new Vector3(maxs.X, mins.Y, maxs.Z), color);
            verts[32] = new VertexPositionColor(new Vector3(maxs.X, mins.Y, mins.Z), color);
            verts[33] = new VertexPositionColor(new Vector3(maxs.X, maxs.Y, mins.Z), color);
            verts[34] = new VertexPositionColor(new Vector3(maxs.X, maxs.Y, maxs.Z), color);
            verts[35] = new VertexPositionColor(new Vector3(maxs.X, mins.Y, mins.Z), color);

            return verts;
        }

        public static Vector3 GetTransformed2DPoint(float x, float y, SizeF renderSize)
        {
            return new Vector3(((x * 2) / (renderSize.Width)) - 1f - (1f / renderSize.Width), ((y * 2) / (renderSize.Height)) - 1f - (-1f / renderSize.Height), 0f);
        }

        public static Mesh LoadMesh(SlimDX.Direct3D9.Device device, string file, ref Material[] meshMaterials, ref Texture[] meshTextures)
        {
            ExtendedMaterial[] mtrl = null;

            int lastIndex = file.LastIndexOfAny(new char[] { '/', '\\' });
            string path = "";
            if (lastIndex != -1)
                path = file.Substring(0, lastIndex);
            path += "\\";

            System.Console.WriteLine("[LoadMesh] Loading: " + file);

            Mesh mesh = Mesh.FromFile(device, file, MeshFlags.Managed);

            if ((mtrl != null) && (mtrl.Length > 0))
            {
                meshMaterials = new Material[mtrl.Length];
                meshTextures = new Texture[mtrl.Length];

                for (int i = 0; i < mtrl.Length; i++)
                {
                    meshMaterials[i] = mtrl[i].MaterialD3D;
                    if (mtrl[i].TextureFileName != null && mtrl[i].TextureFileName != string.Empty)
                    {
                        System.Console.WriteLine("Loading tex: " + path + mtrl[i].TextureFileName);
                        meshTextures[i] = Texture.FromFile(device, path + mtrl[i].TextureFileName, Usage.None, Pool.Default);
                    }
                }
            }
            return mesh;
        }

        // Draws useful information
        public static void DrawRenderStats(Renderer renderer)
        {
            // Draw FPS
            string str = "FPS: " + HighResolutionTimer.Instance.FramesPerSecond;
            System.Drawing.Rectangle rect = new Rectangle();
            renderer.Fonts["diag"].MeasureString(renderer.sprite, str, DrawTextFormat.Left, ref rect);
            WriteText(renderer, str, renderer.device.Viewport.Width - rect.Width, 0, System.Drawing.Color.White, true);

            // Draws camera position
            //renderer.Camera.DrawPosition();
        }

        public static void WriteText(Renderer renderer, string text, int x, int y, Color c, bool shadow)
        {
            if (shadow)
                renderer.Fonts["diag"].DrawString(null, text, new Rectangle(x + 1, y + 1, renderer.device.Viewport.Width, renderer.device.Viewport.Height), DrawTextFormat.ExpandTabs | DrawTextFormat.NoClip | DrawTextFormat.WordBreak, Color.Black);

            renderer.Fonts["diag"].DrawString(null, text, new Rectangle(x, y, renderer.device.Viewport.Width, renderer.device.Viewport.Height), DrawTextFormat.ExpandTabs | DrawTextFormat.NoClip | DrawTextFormat.WordBreak, c);

        }
    }
}
