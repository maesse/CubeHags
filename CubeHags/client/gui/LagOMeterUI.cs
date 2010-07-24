using System;
using System.Collections.Generic;
using System.Text;
using CubeHags.client.gfx;
using CubeHags.client.render;
using SlimDX.Direct3D9;
using System.Drawing;
using SlimDX;

namespace CubeHags.client.gui
{
    public class LagOMeterUI : Window
    {

        HagsTexture tex;
        public LagOMeterUI()
        {
            this.WindowSpawnPosition = Corner.BOTRIGHT;
            this.Title = "Lag-o-Meter";
            this.Size = new System.Drawing.Size(300, 200);
            this.Bound.Size = this.Size;
            tex = new HagsTexture("white.png");
            this.AlwaysVisible = true;
        }

       
        public override void  Render()
        {
               
            base.Render();
            DrawLagometer();
        }

        public void DrawLagometer()
        {

            int vertStartOffset = WindowManager.Instance.VertexList.Count;


            
            int x = middle.Location.X;
            int y = middle.Location.Y;
            int w = middle.Size.Width;
            int h = middle.Size.Height;

            float range = h / 3;
            float mid = y + range;
            float vscale = range / 150;
            float wscale = (float)w / Lagometer.LAGBUFFER;
            for (int a = 0; a < Lagometer.LAGBUFFER; a++)
            {
                int i = (Client.Instance.lagometer.frameCount - 1 - a) & Lagometer.LAGBUFFER-1;
                int v = Client.Instance.lagometer.frameSamples[i];
                v = (int)(v*vscale);
                if (v > 0)
                {
                    // Yellow
                    if (v > range)
                        v = (int)range;
                    // Draw rect
                    WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(x + w - ((a) * wscale), mid - v), new SizeF(1 * wscale, v)), new SlimDX.Color4(Color.Yellow)));
                }
                else if (v < 0)
                {
                    // Blue
                    v = -v;
                    if (v > range)
                        v = (int)range;
                    // Draw rect
                    WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(x + w - ((a) * wscale), mid), new SizeF(1 * wscale, v)), new SlimDX.Color4(Color.Blue)));
                }
            }

            // Draw snapshot latency & drop graph
            for (int a = 0; a < Lagometer.LAGBUFFER; a++)
            {
                int i = (Client.Instance.lagometer.snapshotCount - 1 - a) & Lagometer.LAGBUFFER-1;
                int v = Client.Instance.lagometer.snapshotSamples[i];
                if (v > 0)
                {
                    Color4 color;
                    if ((Client.Instance.lagometer.snapshotFlags[i] & 1) == 1)
                    {
                        // Yellow for rate delay
                        color = new Color4(Color.Yellow);
                    }
                    else
                    {
                        // Green
                        color = new Color4(Color.Green);
                    }
                    v = (int)(v * vscale);
                    if (v > range)
                        v = (int)range;
                    // Draw rect
                    WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(x + w - (a*wscale), y+h-v), new SizeF(1*wscale, v)), color));
                }
                else if (v < 0)
                {
                    // Red for dropped snapshots
                    WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(x + w - (a * wscale), y + h - range), new SizeF(1 * wscale, range)), new SlimDX.Color4(Color.Red)));
                }
            }
            
            ulong key = SortItem.GenerateBits(SortItem.FSLayer.HUD, SortItem.Viewport.DYNAMIC, SortItem.VPLayer.HUD, SortItem.Translucency.NORMAL, tex.MaterialID, 0, 0, WindowManager.Instance.vb.VertexBufferID);
            int nPrimitives = (WindowManager.Instance.VertexList.Count - vertStartOffset) / 3;
            RenderDelegate del = new RenderDelegate((effect, device, setMaterial) =>
            {               
                if (setMaterial)
                    device.SetTexture(0, tex.Texture);

                // Draw UI elements
                device.DrawPrimitives(PrimitiveType.TriangleList, vertStartOffset, nPrimitives);
            });
            WindowManager.Instance.renderCalls.Add(new KeyValuePair<ulong, RenderDelegate>(key, del));
        }
    }
}
