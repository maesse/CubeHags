using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX.Direct3D9;
using System.IO;
using CubeHags.client.common;
using SlimDX;

namespace CubeHags.client.map.Source
{
    public class SourceMaterial
    {
        public string Name = null;
        public ushort MaterialID = TextureManager.Instance.NextMaterialID; // Unique identifier

        public Texture baseTexture = null;
        public string baseTextureName = null;

        public bool Alpha = false;

        public Texture detailTexture = null;
        public bool Detail = false;

        public Texture lightmapTexture = null;
        public bool Lightmap = false;

        public Texture bumpmapTexture = null;
        public bool Bumpmap = false;

        public SourceMaterial(Texture tex)
        {
            baseTexture = tex;
        }

        public SourceMaterial()
        {
        }

        public void ApplyMaterial(Device device)
        {
            if (baseTexture == null)
            {
                if (Name != "\"water\"")
                    baseTexture = TextureManager.Instance.NoTexture;
                else
                    return;
            }

            device.SetTexture(0, baseTexture);


            // Set Lightmap texture
            if (Lightmap)
            {
                if (lightmapTexture == null)
                    lightmapTexture = TextureManager.Instance.NoTexture;

                device.SetTexture(1, lightmapTexture);
            }

            //if (material.Detail)
            //{
            //    device.SetTexture(2, material.detailTexture);
            //}
        }

        private static string ReadVarString(string input)
        {
            string[] splitted = input.Split('"');
            if (splitted.Length == 1)
            {
                // No "
                splitted = splitted[0].Split(' ');
                return splitted[1];
            }
            else
            {
                // with "
                int progress = 0;
                int token = 0;
                string value = null;
                foreach (string str in splitted)
                {
                    switch (progress)
                    {
                        case 0:
                            if (str.Equals(""))
                                progress++;
                            break;
                        case 1:
                            progress++;
                            token++;
                            if(token == 2)
                                value = str;
                            break;
                        case 2:
                            if (str.Equals(""))
                                progress = 0;
                            string trimmed = str.Trim();
                            if (trimmed.Equals(""))
                                progress = 1;
                            //else if (str.Equals("\t"))
                            //    progress = 1;
                            break;
                    }

                }
                return value;
            }
        }

        unsafe static public Texture LoadVTF(Device device, string filename)
        {
            if (filename == null)
                return null;

            // Get full path
            FCFile file = null;
            if (!filename.Contains("."))
            {
                file = FileCache.Instance.GetFile(filename + ".vtf");
            }

            if (file != null)
            {
                filename = file.FullName;
            }

            if (!File.Exists(filename))
            {
                return null;
            }

            // Init vtflib
            uint vlImage;
            VtfLib.vlInitialize();
            VtfLib.vlCreateImage(&vlImage);
            VtfLib.vlBindImage(vlImage);

            // Try to load image
            if (!VtfLib.vlImageLoad(filename, false))
            {
                System.Console.WriteLine("[SourceMaterial] Could not load load texture: " + filename);
                VtfLib.vlDeleteImage(vlImage);
                VtfLib.vlShutdown();
                return null;
            }
            
            // Get image description
            uint w, h;
            VtfLib.ImageFormat f;
            w = VtfLib.vlImageGetWidth();
            h= VtfLib.vlImageGetHeight();
            f = VtfLib.vlImageGetFormat();
            bool alpha = false;
            VtfLib.ImageFormat imgFormat = VtfLib.ImageFormat.ImageFormatRGB888;
            if (f == VtfLib.ImageFormat.ImageFormatDXT5)
            {
                alpha = true;
                imgFormat = VtfLib.ImageFormat.ImageFormatARGB8888;
                
            }

            // Convert image
            byte[] lpImageData = null;
            lpImageData = new byte[VtfLib.vlImageComputeImageSize(w, h, 1, 1, imgFormat)];
            fixed (byte* lpOutput = lpImageData)
            {
                if (!VtfLib.vlImageConvert(VtfLib.vlImageGetData(0, 0, 0, 0), lpOutput, w, h, f, imgFormat))
                {
                    System.Console.WriteLine("[SourceMaterial] VTFLib: Could not convert texture");
                }
            }

            //TextureRequirements reqs;
            //Texture.CheckTextureRequirements(device, Usage.AutoGenerateMipMap, Pool.Default, out reqs);

            // Create texture
            Format format = Format.A8R8G8B8;
            if (!alpha) format = Format.X8R8G8B8;
            Texture tex = new Texture(device, (int)w, (int)h, 1, Usage.AutoGenerateMipMap | Usage.Dynamic, format, (Renderer.Instance.Is3D9Ex ? Pool.Default : Pool.Managed));
            DataStream pData = tex.LockRectangle(0, LockFlags.None).Data;
            MemoryStream ms = new MemoryStream(lpImageData);
            // Set pixels
            for (int i = 0; i < h; i++)
            {
                for (int j = 0; j < w; j++)
                {
                    if (alpha)
                    {
                        int[] bytes = new int[] { ms.ReadByte(), ms.ReadByte(), ms.ReadByte(), ms.ReadByte() };
                        pData.Write<uint>( (uint)System.Drawing.Color.FromArgb(bytes[2], bytes[3], bytes[0], bytes[1]).ToArgb()); // wtf?
                        
                    }
                    else
                        pData.Write<uint>((uint)System.Drawing.Color.FromArgb(ms.ReadByte(), ms.ReadByte(), ms.ReadByte()).ToArgb());
                    //pData++;
                }
            }
            tex.UnlockRectangle(0);

            // (debug) Save as bitmap
            if(false)
            {
                string prettyname = filename.Replace('/', '_');
                prettyname = prettyname.Replace('\\', '_');
                Texture.ToFile(tex, prettyname + ".png", ImageFileFormat.Png);
            }

            // Release vtflib
            VtfLib.vlDeleteImage(vlImage);
            VtfLib.vlShutdown();
            return tex;
        }

        public static SourceMaterial LoadFromStream(Stream stream)
        {
            SourceMaterial mat = new SourceMaterial();
            StreamReader reader = new StreamReader(stream);
            string line;
            int progress = 0;
            while ((line = reader.ReadLine()) != null && !line.Trim().Equals(""))
            {
                //System.Console.WriteLine(line);
                line = line.Trim().ToLower();
                switch (progress)
                {
                    case 0:
                        // Read shadername
                        mat.Name = line;
                        progress++;

                        break;
                    case 1:
                        // Read arguments..
                        if (line.Contains("$basetexture\""))
                        {
                            mat.baseTextureName = ReadVarString(line);
                            mat.baseTexture = LoadVTF(Renderer.Instance.device, mat.baseTextureName);
                            if (mat.baseTexture != null && mat.baseTexture.GetLevelDescription(0).Format == Format.A8R8G8B8)
                                mat.Alpha = true;
                        }
                        else if (line.Contains("\"include\""))
                        {
                            string name = ReadVarString(line);
                            SourceMaterial material = TextureManager.Instance.LoadMaterial(name);
                            if (material != null)
                                mat = material;
                        }
                        else if(line.Contains("\"$detail\""))
                        {
                            string detailname = ReadVarString(line);
                            mat.detailTexture = LoadVTF(Renderer.Instance.device, detailname);
                            if (mat.detailTexture != null)
                                mat.Detail = true;

                        }
                        else if (line.Contains("\"$bumpmap\""))
                        {
                            string bumpname = ReadVarString(line);
                            mat.bumpmapTexture = LoadVTF(Renderer.Instance.device, bumpname);
                            if (mat.bumpmapTexture != null)
                                mat.Bumpmap = true;
                        }
                        //System.Console.WriteLine(line);
                        break;
                }
            }
            if (mat.baseTexture == null)
            {
                int test = 2;
            }
            return mat;
        }

        public static SourceMaterial LoadFromFile(string path)
        {
            FileStream file = File.OpenRead(path);
            return LoadFromStream(file);
        }

        public void Dispose()
        {
            // Dispose textures
            if (baseTexture != null)
                baseTexture.Dispose();
            if (lightmapTexture != null)
                lightmapTexture.Dispose();
            if (bumpmapTexture != null)
                bumpmapTexture.Dispose();
        }
    }
}
