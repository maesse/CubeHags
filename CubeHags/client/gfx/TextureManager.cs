using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX.Direct3D9;
using System.IO;
using CubeHags.client.map.Source;
using Ionic.Zip;
using CubeHags.client.common;
using System.Drawing;
using System.Drawing.Imaging;
using SlimDX;
using CubeHags.client.gfx;

namespace CubeHags.client
{
    public sealed class TextureManager
    {
        private static readonly TextureManager _instance = new TextureManager();
        private Dictionary<string, Texture> textures;
        private Dictionary<string, SourceMaterial> materials;
        public List<HagsTexture> hagsTextures { get; private set; }
        public Texture NoTexture = null;
        private ushort _NextMaterialID = 1;
        public ushort NextMaterialID { get { return _NextMaterialID++; } }

        TextureManager() 
        {
            hagsTextures = new List<HagsTexture>();
            textures = new Dictionary<string, Texture>();
            materials = new Dictionary<string, SourceMaterial>();
            //NoTexture = LoadTexture("gfx/checkerboard.dds");
            FileCache.Instance.ToString();
        }

        public void CacheMaterial(string name, SourceMaterial mat)
        {
            // Add if it doesnt excist
            if (!materials.ContainsKey(name))
                materials.Add(name, mat);
            else
                System.Console.WriteLine("Material already cached: " + name);
        }

        public SourceMaterial LoadMaterial(string name)
        {
            // Clean up name
            name = name.Replace('\\', '/').ToLower();
            if (name.Contains("" + '\0'))
                name = name.Substring(0, name.IndexOf('\0'));
            if (name.StartsWith("materials/"))
                name = name.Substring("materials/".Length);

            // Look in cache
            if (name.Contains("" + '.'))
            {
                if (materials.ContainsKey(name))
                    return materials[name];
            }
            else
            {
                if (materials.ContainsKey(name+".vmt"))
                    return materials[name + ".vmt"];
                else if (materials.ContainsKey(name + ".vtf"))
                    return materials[name + ".vtf"];
            }

            // Look in FileCache
            FCFile file;
            if (name.Contains("" + '.'))
                file = FileCache.Instance.GetFile(name);
            else {
                // No extension.. try vmt first, then vtf
                if ((file = FileCache.Instance.GetFile(name + ".vmt")) == null)
                    file = FileCache.Instance.GetFile(name + ".vtf");
            }

            if (file != null)
            {
                SourceMaterial material = null;

                if (file.FullName.EndsWith(".vmt"))
                    material = SourceMaterial.LoadFromFile(file.FullName);
                else if (file.FullName.EndsWith(".vtf"))
                {
                    material = new SourceMaterial();
                    material.baseTexture = SourceMaterial.LoadVTF(Renderer.Instance.device, file.FullName);
                }

                if (material != null)
                {
                    CacheMaterial(name, material);
                    return material;
                }
            }


            // Give up
            System.Console.WriteLine("Could not find Material: " + name);
            return null;

        }

        public Texture LoadTexture(string name)
        {
            // Clean up name
            name.Replace('\\', '/');
            if (name.Contains("" + '\0'))
                name = name.Substring(0, name.IndexOf('\0'));
            string cleanName = name;

            // return texture if its already loaded
            if (textures.ContainsKey(cleanName))
            {
                return textures[cleanName];
            }

            FCFile file = FileCache.Instance.GetFile(name);
            if (Renderer.Instance.device == null)
            {
                throw new Exception("device == null");
            }
            if (file != null)
            {
                Texture texture = Texture.FromFile(Renderer.Instance.device, file.FullName, Usage.None, (Renderer.Instance.Is3D9Ex?Pool.Default:Pool.Managed));
                if (texture != null)
                    textures.Add(cleanName, texture);
                return texture;
            }

            System.Console.WriteLine("Could not find texture: " + name);
            throw new FileNotFoundException("Could not find texture: " + name, name);
        }

        // Caches a zip files content in the texturemanager
        public void CacheZipFile(ZipFile zip)
        {
            MemoryStream ms = new MemoryStream();
            SourceMaterial material;
            string name;
            System.Console.WriteLine("Caching PAK file.. nFiles: " + zip.Count);
            foreach (ZipEntry entry in zip)
            {
                // read it
                ms.Position = 0;
                ms.SetLength(entry.UncompressedSize);
                entry.Extract(ms);
                ms.Seek(0L, SeekOrigin.Begin);

                // Is it a material?
                if (entry.FileName.EndsWith(".vmt"))
                {
                    name = entry.FileName;
                    if (!materials.ContainsKey(name))
                    {
                        // parse it
                        material = SourceMaterial.LoadFromStream(ms);

                        if (name.StartsWith("materials"))
                        {
                            name = name.Substring("materials/".Length);
                        }

                        // cache it
                        CacheMaterial(name, material);
                    }
                    else
                        System.Console.WriteLine("Not caching PAK file, name already exists in cache: " + name);
                }
            }
            System.Console.WriteLine("Finished caching PAK file..");
        }

        public static Texture CreateTexture(int w, int h, Format format, Color4 color)
        {
            Texture texture = new Texture(Renderer.Instance.device, w, h, 1, Usage.None, format, (Renderer.Instance.Is3D9Ex ? Pool.Default : Pool.Managed));
            DataRectangle drect = texture.LockRectangle(0, LockFlags.None);
            DataStream ds = drect.Data;

            // pixelsize in bytes
            int fieldsize = 8;
            switch (format)
            {
                case Format.A8R8G8B8:
                    fieldsize = 4;
                    break;
                case Format.A16B16G16R16F:
                    fieldsize = 8;
                    break;
            }
            
            // Fill texture with color
            for (int j = 0; j < (w * h); j++)
            {
                int x = ((j) % (w));
                int y = ((j) / (w));

                ds.Seek((long)(y * fieldsize) + (long)(x * fieldsize), System.IO.SeekOrigin.Begin);

                switch (format)
                {
                    case Format.A8R8G8B8:
                        ds.Write<Color4>(color);
                        break;
                    case Format.A16B16G16R16F:
                        Half[] half = Half.ConvertToHalf(new float[] { color.Red, color.Green, color.Blue, color.Alpha });
                        ds.Write<Half4>(new Half4(half[0], half[1], half[2], half[3]));
                        break;
                }
                
            }
            texture.UnlockRectangle(0);
            return texture;
        }

        public void Dispose()
        {
            if(NoTexture != null)
                NoTexture.Dispose();
            foreach (SourceMaterial material in materials.Values)
            {
                material.Dispose();
            }
            foreach (Texture tex in textures.Values)
            {
                if (tex != null)
                {
                    tex.Dispose();
                }
            }
            
        }


        public static TextureManager Instance
        {
            get { return _instance; }
        }
    }
}
