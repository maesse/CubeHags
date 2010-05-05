using System;
using System.Collections.Generic;
 
using System.Text;
using System.Collections;
using System.IO;
using SlimDX.Direct3D9;
using SlimDX;
using CubeHags.client.render.Formats;


namespace CubeHags.client
{
    public class HeightMap
    {
        private int m, n;
        private float scale, offset;
        private string filename = null;
        private System.Drawing.Color[,] loadedHeightMap = null;
        private int inputImageWidth, inputImageHeight;
        //private Mesh mesh; // holds the heightmap
        private List<Mesh> meshes = new List<Mesh>();
//        

        //public static VertexElement[] elems = new VertexElement[] {
        //        new VertexElement(0, 0, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Position, 0),
        //        new VertexElement(0, sizeof(float)*3, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Normal, 0),
        //        new VertexElement(0, sizeof(float)*6, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 0),
        //        VertexElement.VertexDeclarationEnd
        //    };
        
        private VertexDeclaration vd = null;

        public int NumRows { get { return m; } }
        public int NumCols { get { return n; } }
        public HeightMap()
        {
        }
        public HeightMap(int m, int n)
        {
        }
        public HeightMap(int m, int n, string filename, float heightScale, float heightOffset)
        {
            this.m = m;
            this.n = n;
            this.filename = filename;
            this.scale = heightScale;
            this.offset = heightOffset;
            
            // Create grid
            if(CreateFromFile())
                BuildGridGeometry();
            vd = new VertexDeclaration(Renderer.Instance.device, VertexPosTexNormalTanBitan.Elements);
            
        }

        public void recreate()
        {
        }

        public void Render()
        {
            foreach (Mesh mesh in meshes)
            //Mesh mesh = meshes[0];
            {
                //RenderGroup group = new RenderGroup(RenderLayer.Mesh);
                //group.vd = vd;
                //group.mesh = mesh;
                ////group.tex = TextureManager.Instance.LoadTexture("client/gfx/white.dds");
                ////group.tex = TextureManager.Instance.LoadTexture("client/gfx/europe4k.dds");
                //group.tex = TextureManager.Instance.LoadTexture("textures/stones.bmp");
                //group.tex2 = TextureManager.Instance.LoadTexture("textures/stones_NM_height.tga");
                

                //Renderer.Instance.AddRenderGroupToLayer(group);
            }
        }

        private float GetHeightFromImage(int j, int i)
        {
            return (loadedHeightMap[j, i].R + loadedHeightMap[j, i].G + loadedHeightMap[j, i].B) / 3;
        }

        float SampleHeightMap3x3(int i, int j)
        {
            float avg = 0f, num = 0f;

            for (int m = i - 1; m <= i + 1; ++m)
            {
                for (int n = j - 1; n <= j + 1; ++n)
                {
                    if (m < inputImageWidth && m >= 0 && n < inputImageHeight && n >= 0)
                    {
                        avg += GetHeightFromImage(m, n);
                        num += 1f;
                    }
                }
            }
            return avg / num;
        }

        int GetGoodAverage(int input)
        {
            int diff = 0, output = 1, lastDiff = -1;
            while (true)
            {
                output *= 2;
                input = input / 2;
                if ((diff = Math.Abs(input - output)) < lastDiff || lastDiff == -1)
                {
                    lastDiff = diff;
                }
                else
                    return output / 2;
            }
        }

        void BuildGridGeometry()
        {

            Vector3[] globalverts, gridverts;
            int[] globalindices, gridindices, gridindices2;

            float dx = 10.0f;
            float dz = 10.0f;

            // Generate global grid
            GenTriGrid(inputImageHeight, inputImageWidth, dx, dz, new Vector3(0.0f, -1000f, 0f), out globalverts, out globalindices);

            // Number of sub-grids
            int nGridsY = GetGoodAverage(inputImageHeight);
            int nGridsX = GetGoodAverage(inputImageWidth);

            // Subgrid size
            int GridW = inputImageWidth / nGridsX;
            int GridD = inputImageHeight / nGridsY;

            int gridNumVerts = (GridW+1) * (GridD+1);
            int gridNumTris = (GridD ) * (GridW ) * 2;

            // Generate subgrid indices
            GenTriGrid(GridD+1, GridW+1, dx, dz, new Vector3(0.0f, -5000f, 0f), out gridverts, out gridindices);
            GenTriGrid(GridD, GridW, dx, dz, new Vector3(0.0f, -5000f, 0f), out gridverts, out gridindices2);

            // Define some often used variables
            bool overflowX = false, overflowY = false;
            float w = (GridW*nGridsX) * dx;
            float d = (GridD * nGridsY) * dz;
            Vector3 normal = new Vector3(0f, 1f, 0f);
            Mesh mesh;
            VertexPositionNormalTextured[] vertexData = new VertexPositionNormalTextured[gridNumVerts];
            int subgridX, subgridY, globalIndex, gridIndex;

            // foreach subgrid
            for (int gridX = 0; gridX < nGridsX; gridX++)
            {
                for (int gridY = 0; gridY < nGridsY; gridY++)
                {
                    overflowY = false;
                    overflowX = false;
                    mesh = new Mesh(Renderer.Instance.device, gridNumTris, gridNumVerts, MeshFlags.Use32Bit, VertexPositionNormalTextured.Format);

                    // Check for overflow
                    if ((gridX+1) * (GridW + 1) > inputImageWidth)
                        overflowX = true;
                    else if ((gridY+1) * (GridD + 1) > inputImageHeight)
                        overflowY = true;
                    if (overflowY || overflowX)
                    {
                    }
                    else
                    {
                        for (int subD = 0; subD < GridD + 1; ++subD)
                        {
                            for (int subW = 0; subW < GridW + 1; ++subW)
                            {
                                subgridX = gridX * GridW + subW;
                                subgridY = gridY * GridD + subD;
                                globalIndex = (subgridY * inputImageHeight) + subgridX;
                                gridIndex = (subD * (GridD + 1)) + subW;

                                vertexData[gridIndex].Position = globalverts[globalIndex];
                                //vertexData[gridIndex].Y += GetHeightFromImage(subgridY, subgridX) * scale;
                                vertexData[gridIndex].Position.Y += SampleHeightMap3x3(subgridY, subgridX) * scale;
                                vertexData[gridIndex].Normal = normal;
                                vertexData[gridIndex].TextureCoordinate = new Vector2((vertexData[gridIndex].Position.X + (0.5f * w)) / w, (vertexData[gridIndex].Position.Z - (0.5f * d)) / -d);
                            }
                        }

                        DataStream gs = mesh.LockVertexBuffer(LockFlags.None);
                        gs.WriteRange<VertexPositionNormalTextured>(vertexData);
                        gs.Seek(0, SeekOrigin.Begin);
                        // Todo: Fix AABB and frustrum culling
                        Vector3 min, max;
                        //Geometry.ComputeBoundingBox(gs, gridNumVerts, VertexPositionNormalTextured.Format, out min, out max);
                        mesh.UnlockVertexBuffer();


                        //int[] meshIndices = new int[gridNumTris * 3];
                        DataStream ds = mesh.LockAttributeBuffer(LockFlags.None);
                        for (int i = 0; i < gridNumTris; ++i)
                        {
                            ds.Write<int>(0);
                        }
                        mesh.UnlockAttributeBuffer();

                        //meshIndices = ;
                        gs = mesh.LockIndexBuffer(LockFlags.None);
                        gs.WriteRange<int>(gridindices);
                        mesh.UnlockIndexBuffer();

                        //gs = mesh.LockAttributeBuffer(LockFlags.None);
                        //gs.Write(meshAttr);
                        

                        mesh.ComputeNormals();
                        int[] adj = new int[mesh.FaceCount * 3];
                        mesh.GenerateAdjacency(float.Epsilon);
                        //mesh.OptimizeInPlace(MeshFlags.OptimizeAttributeSort | MeshFlags.OptimizeVertexCache | MeshFlags.OptimizeCompact, adj);
                        Mesh newmesh = mesh.Clone(Renderer.Instance.device, MeshFlags.Managed, VertexPosTexNormalTanBitan.Elements);
                        newmesh.ComputeTangent(0, 0, -1, false);
                        
                        

                        meshes.Add(newmesh);
                    }
                }
            }
            
        }

        // Loads heightmap from image file
        bool CreateFromFile()
        {
            // Load heightmap image
            Texture heightmap =  TextureManager.Instance.LoadTexture(filename);
            if (heightmap == null)
                return false;
            
            // Get image size
            SurfaceDescription sf = heightmap.GetLevelDescription(0);
            inputImageHeight = sf.Height;
            inputImageWidth = sf.Width;

            // Prepare for loading
            loadedHeightMap = new System.Drawing.Color[sf.Width, sf.Height];

            // Lock the texture
            DataStream gs = heightmap.LockRectangle(0, LockFlags.ReadOnly).Data;
            
            // Load pixel by pixel
            for (int i = 0; i < sf.Width; i++)
            {
                for (int j = 0; j < sf.Height; j++)
                {
                    byte[] buffer = new byte[4];
                    gs.Read(buffer, 0, 4);
                    System.Drawing.Color color = System.Drawing.Color.FromArgb(BitConverter.ToInt32(buffer, 0));
                    loadedHeightMap[i, j] = color;
                    
                }
            }
            // Unload texture
            heightmap.UnlockRectangle(0);
            return true;
        }

        // Generate Triangle-Grid - fra 3D Game Programming: A shader based approach..
        public static void GenTriGrid(int numVertRows, int numVertCols, float dx, float dz, Vector3 center, out Vector3[] verts, out int[] indices)
        {
            int numVerts = numVertCols * numVertRows;
            int numCellRows = numVertRows - 1;
            int numCellCols = numVertCols - 1;

            // Total number of triangles
            int numTri = numCellRows * numCellCols * 2;

            // Size of grid
            float width = (float)numCellCols * dx;
            float depth = (float)numCellRows * dz;

            verts = new Vector3[numVerts];

            // Gen offset so grid is centered around origin
            float xOffset = -width * 0.5f;
            float zOffset = -depth * 0.5f;

            // Build vertices
            int k = 0;
            for (float i = 0; i < numVertRows; ++i)
            {
                for (float j = 0; j < numVertCols; ++j)
                {
                    verts[k].X = j * dx + xOffset;
                    verts[k].Z = i * dz + zOffset;
                    verts[k].Y = 0.0f;
                    //;
                    //Matrix T = new Matrix();
                    //T.Translate(center
                    verts[k] = Vector3.TransformCoordinate(verts[k], Matrix.Translation(center));

                    ++k;
                }
            }

            // Build indices
            indices = new int[numTri * 3];
            k = 0;
            for (int i = 0; i < numCellRows; ++i)
            {
                for (int j = 0; j < numCellCols; ++j)
                {
                    indices[k] = i * numVertCols + j;
                    indices[k + 1] = i * numVertCols + j + 1;
                    indices[k + 2] = (i + 1) * numVertCols + j;

                    indices[k + 3] = (i + 1) * numVertCols + j;
                    indices[k + 4] = i * numVertCols + j + 1;
                    indices[k + 5] = (i + 1) * numVertCols + j + 1;

                    // next quad
                    k += 6;
                }
            }
        }

    }
}
