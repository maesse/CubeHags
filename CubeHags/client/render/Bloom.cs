using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;
using SlimDX.Direct3D9;

namespace CubeHags.client.render
{
    class Bloom : IResettable
    {
        
        private int nBloomTex = 2;
        Texture[] BloomTex = null;
        public Texture BloomTexture { get { return BloomTex[0]; } }
        Texture BrightPassTex = null;

        public Bloom(Renderer renderer)
        {
            OnResetDevice();
        }

        public Result OnLostDevice()
        {
            foreach (Texture tex in BloomTex)
            {
                tex.Dispose();
            }
            BrightPassTex.Dispose();

            return new Result();
        }

        public Result OnResetDevice()
        {
            Renderer renderer = Renderer.Instance;
            // Set up bloom textures
            BloomTex = new Texture[nBloomTex];
            for (int i = 0; i < nBloomTex; i++)
            {
                BloomTex[i] = new Texture(renderer.device, renderer.RenderSize.Width / 2, renderer.RenderSize.Height / 2, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
            }
            BrightPassTex = new Texture(renderer.device, renderer.RenderSize.Width / 2, renderer.RenderSize.Height / 2, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
            return new Result();
        }

        private void BrightPassFilter(Device device, Effect effect)
        {
            Surface OriginalDS = device.DepthStencilSurface;
            device.DepthStencilSurface = Renderer.Instance.RenderDepthSurface;
            Vector2[] sampleOffsets = new Vector2[16];
            Surface brightPassSurface = BrightPassTex.GetSurfaceLevel(0);
            SurfaceDescription backbufDescr = Renderer.Instance.RenderSurface.Description;
            GetSampleOffsetsets_DownScale3x3(backbufDescr.Width / 2, backbufDescr.Height / 2, ref sampleOffsets);
            effect.SetValue<Vector2>(new EffectHandle("g_avSampleOffsets"), sampleOffsets);

            effect.Technique = "DownScale3x3_BrightPass_RGBE8";
            Surface orgTarget = device.GetRenderTarget(0);
            device.SetRenderTarget(0, brightPassSurface);
            device.SetTexture(0, Renderer.Instance.RenderTexture);
            device.SetTexture(1, Renderer.Instance.AvgLum);
            device.SetSamplerState(0, SamplerState.MagFilter, (int)TextureFilter.Linear);
            device.SetSamplerState(0, SamplerState.MinFilter, (int)TextureFilter.Linear);

            Renderer.Instance.DrawFullScreenQuad();

            device.SetTexture(0, null);
            device.SetTexture(1, null);
            device.SetRenderTarget(0, orgTarget);
            device.DepthStencilSurface = OriginalDS;
        }

        public void RenderBloom(Device device, Effect effect)
        {
            BrightPassFilter(device, effect);
            Surface orgTarget = device.GetRenderTarget(0);
            Vector2[] sampleOffsets = new Vector2[16];
            Vector4[] sampleWeights = new Vector4[16];
            float[] afSampleOffsets = new float[16];

            Surface surfDest = BloomTex[1].GetSurfaceLevel(0);
            SurfaceDescription desc = BrightPassTex.GetSurfaceLevel(0).Description;
            Surface OriginalDS = device.DepthStencilSurface;
            device.DepthStencilSurface = Renderer.Instance.RenderDepthSurface;

            GetSampleOffsets_Bloom(desc.Width, afSampleOffsets, ref sampleWeights, 3.0f, 1.25f);

            for (int i = 0; i < 16; i++)
            {
                sampleOffsets[i] = new Vector2(afSampleOffsets[i], 0f);
            }

            effect.Technique = "Bloom";
            effect.SetValue("g_avSampleWeights", sampleWeights);
            effect.SetValue<Vector2>(new EffectHandle("g_avSampleOffsets"), sampleOffsets);

            device.SetRenderTarget(0, surfDest);
            device.SetTexture(0, BrightPassTex);
            device.SetSamplerState(0, SamplerState.MinFilter, (int)TextureFilter.Point);
            device.SetSamplerState(0, SamplerState.MagFilter, (int)TextureFilter.Point);

            Renderer.Instance.DrawFullScreenQuad();

            device.SetTexture(0, null);
            surfDest.Dispose();
            surfDest = BloomTex[0].GetSurfaceLevel(0);
            GetSampleOffsets_Bloom(desc.Height, afSampleOffsets, ref sampleWeights, 3.0f, 1.25f);
            for (int i = 0; i < 16; i++)
            {
                sampleOffsets[i] = new Vector2(0f, afSampleOffsets[i]);
            }

            effect.Technique = "Bloom";
            effect.SetValue("g_avSampleWeights", sampleWeights);
            effect.SetValue<Vector2>(new EffectHandle("g_avSampleOffsets"), sampleOffsets);

            device.SetRenderTarget(0, surfDest);
            device.SetTexture(0, BloomTex[1]);

            Renderer.Instance.DrawFullScreenQuad();

            device.SetTexture(0, null);
            device.SetRenderTarget(0, orgTarget);
            device.DepthStencilSurface = OriginalDS;
        }

        private void GetSampleOffsets_Bloom(int texSize, float[] afTexCoordOffset, ref Vector4[] colorWeight, float deviation, float multiplier)
        {
            float tu = 1f / texSize;

            // Get center texel
            float weight = 1f * GaussianDistribution(0, 0, deviation);
            colorWeight[0] = new Vector4(weight, weight, weight, 1f);
            afTexCoordOffset[0] = 0f;

            // Fill right side
            for (int i = 1; i < 8; i++)
            {
                weight = multiplier * GaussianDistribution((float)i, 0, deviation);
                afTexCoordOffset[i] = i * tu;

                colorWeight[i] = new Vector4(weight, weight, weight, 1f);
            }

            // Fill left side
            for (int i = 8; i < 15; i++)
            {
                colorWeight[i] = colorWeight[i - 7];
                afTexCoordOffset[i] = -afTexCoordOffset[i - 7];
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

        private float GaussianDistribution(float x, float y, float rho)
        {
            float g = (float)(1.0f / Math.Sqrt(2.0f * Math.PI * rho * rho));
            g *= (float)Math.Exp(-(x * x + y * y) / (2 * rho * rho));

            return g;
        }

        public void Dispose()
        {
            for (int i = 0; i < BloomTex.Length; i++)
            {
                BloomTex[i].Dispose();
            }
            BrightPassTex.Dispose();
        }

        
    }
}
