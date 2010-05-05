using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;
using System.Drawing;
using SlimDX.Direct3D9;

namespace CubeHags.client.render
{
    class ToneMap : IResettable
    {
        // Returns current average 1x1 pixel texture
        public Texture AverageLum {get {return CurrentAvgToneMap == -1? null:avgToneMap[CurrentAvgToneMap]; }}
        private Texture[] avgToneMap = new Texture[2]; // Used for keeping average lum
        private int CurrentAvgToneMap = -1;
        
        private int nToneMapTex = 5;
        Texture[] ToneMapTex = null; // Used for downscaling
        Texture LockableAverage;

        public float MaxLogLum = 7;
        public float MinLogLum = 2;
        public bool ForceAvgLum = true;
        public float ForcedAvgLum = 150f;

        public ToneMap(Renderer renderer)
        {
            OnResetDevice();
        }

        public void Dispose()
        {
            for (int i = 0; i < avgToneMap.Length; i++)
            {
                avgToneMap[i].Dispose();
            }
            for (int i = 0; i < ToneMapTex.Length; i++)
            {
                ToneMapTex[i].Dispose();
            }
        }

        public Result OnLostDevice()
        {
            foreach (Texture tex in ToneMapTex)
            {
                tex.Dispose();
            }
            avgToneMap[0].Dispose();
            avgToneMap[1].Dispose();
            LockableAverage.Dispose();
            return new Result();
        }

        public Result OnResetDevice()
        {
            Renderer renderer = Renderer.Instance;
            // Create tonemap textures starting from the smallest scale
            ToneMapTex = new Texture[nToneMapTex];
            int sampleLen = 1;
            for (int i = 0; i < ToneMapTex.Length; i++)
            {
                ToneMapTex[i] = new Texture(renderer.device, sampleLen, sampleLen, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
                sampleLen *= 3;
            }

            avgToneMap[0] = new Texture(renderer.device, 1, 1, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
            avgToneMap[1] = new Texture(renderer.device, 1, 1, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
            LockableAverage = new Texture(renderer.device, 1, 1, 1, Usage.Dynamic, Format.A8R8G8B8, Pool.SystemMemory);

            return new Result();
        }

        public void MeasureLuminance(Effect effect, Device device)
        {
            Surface OriginalRenderTarget = device.GetRenderTarget(0);
            Vector2[] sampleOffsets = new Vector2[16];
            Texture dest = ToneMapTex[nToneMapTex - 1];

            SurfaceDescription destDescr = dest.GetLevelDescription(0);
            SurfaceDescription sourceDescr = Renderer.Instance.RenderSurface.Description;

            GetSampleOffsets_DownScale2x2_Lum(sourceDescr.Width, sourceDescr.Height, destDescr.Width, destDescr.Height, ref sampleOffsets);
            effect.SetValue<Vector2>(new EffectHandle("g_avSampleOffsets"), sampleOffsets);
            effect.Technique = "DownScale2x2_Lum_RGBE8";

            Surface destSurface = dest.GetSurfaceLevel(0);
            device.SetRenderTarget(0, destSurface);
            Surface OriginalDS = device.DepthStencilSurface;
            device.DepthStencilSurface = Renderer.Instance.RenderDepthSurface;
            device.SetSamplerState(0, SamplerState.MagFilter, (int)TextureFilter.Linear);
            device.SetSamplerState(0, SamplerState.MinFilter, (int)TextureFilter.Linear);
            device.SetTexture(0, Renderer.Instance.RenderTexture);

            Renderer.Instance.DrawFullScreenQuad();
            device.SetTexture(0, null);

            effect.Technique = "DownScale3x3_RGBE8";
            for (int i = nToneMapTex - 1; i > 0; i--)
            {
                Surface source = ToneMapTex[i].GetSurfaceLevel(0);
                dest = ToneMapTex[i - 1];
                destSurface = dest.GetSurfaceLevel(0);
                GetSampleOffsetsets_DownScale3x3(destSurface.Description.Width, destSurface.Description.Height, ref sampleOffsets);
                effect.SetValue<Vector2>(new EffectHandle("g_avSampleOffsets"), sampleOffsets);

                device.SetRenderTarget(0, destSurface);
                device.SetTexture(0, ToneMapTex[i]);
                //device.SetSamplerState(0, SamplerStageStates.MagFilter, (int)TextureFilter.Point);
                //device.SetSamplerState(0, SamplerStageStates.MinFilter, (int)TextureFilter.Point);

                Renderer.Instance.DrawFullScreenQuad();

                device.SetTexture(0, null);
            }

            // Add calculated luminance to average
            if (CurrentAvgToneMap == -1)
            {
                Rectangle avgrect = new Rectangle(new Point(), new Size(1, 1));
                device.StretchRectangle(ToneMapTex[0].GetSurfaceLevel(0), avgrect, avgToneMap[0].GetSurfaceLevel(0), avgrect, TextureFilter.Point);

                CurrentAvgToneMap = 0;
            }
            else
            {
                effect.Technique = "CalcAvgLum_RGBE8";
                device.SetTexture(0, avgToneMap[CurrentAvgToneMap]); // "main" average
                int other = (CurrentAvgToneMap == 0 ? 1 : 0);
                device.SetTexture(1, ToneMapTex[0]); // new average to be added
                Surface surf = avgToneMap[other].GetSurfaceLevel(0);
                device.SetRenderTarget(0, surf); // unused "main" average texture used as rendertarget

                Renderer.Instance.DrawFullScreenQuad();

                device.SetTexture(0, null);
                device.SetTexture(1, null);
                CurrentAvgToneMap = other; // swap avgTonemap usage next frame
            }

            device.SetRenderTarget(0, OriginalRenderTarget);
            device.DepthStencilSurface = OriginalDS;
            
            // Read result
            device.GetRenderTargetData(AverageLum.GetSurfaceLevel(0), LockableAverage.GetSurfaceLevel(0));
            DataRectangle rect = LockableAverage.LockRectangle(0, LockFlags.ReadOnly);
            Color4 color = new Color4(rect.Data.Read<int>());
            LockableAverage.UnlockRectangle(0);

            // Revert LogLum encoding
            float invLogLumRange = 1.0f / (MaxLogLum + MinLogLum);
            float logLumOffset = MinLogLum * invLogLumRange;
            double avgLum = Math.Exp((color.Alpha) / (invLogLumRange + logLumOffset));
            effect.SetValue("avgLogLum", avgLum);
            //System.Console.WriteLine(avgLum);
        }

        private void GetSampleOffsets_DownScale2x2_Lum(int srcWidth, int srcHeight, int destWidth, int destHeight, ref Vector2[] sampleOffsets)
        {
            if (sampleOffsets == null)
                return;

            float tU = 1f / srcWidth;
            float tV = 1f / srcHeight;

            float deltaU = (float)srcWidth / destWidth / 2f;
            float deltaV = (float)srcHeight / destHeight / 2f;

            // sample from 4 surrounding points
            int i = 0;
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x <= 2; x++)
                {
                    sampleOffsets[i].X = (x - 0.5f) * deltaU * tU;
                    sampleOffsets[i].Y = (y - 0.5f) * deltaV * tV;

                    i++;
                }
            }
        }

        private void GetSampleOffsetsets_DownScale3x3(int width, int height, ref Vector2[] avSampleOffsets)
        {
            float tU = 1.0f / width;
            float tV = 1.0f / height;

            // Sample from the 9 surrounding points. 
            int index = 0;
            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    avSampleOffsets[index].X = x * tU;
                    avSampleOffsets[index].Y = y * tV;

                    index++;
                }
            }
        }

        
    }
}
