using System;
using System.Collections.Generic;
using System.Text;
using SlimDX;
using CubeHags.client.map.Source;
using CubeHags.client;

namespace CubeHags.common
{
    public class Displacement
    {
        public DispSurface Surface;

        public int Power;
        float Elevation;

        public CoreDispVert[] Verts;
        public CoreDispTri[] Tris;
        public CoreDispNode[] Nodes;
        int[] RenderIndices;
        int RenderIndexCount = 0;
        //bool touched;

        public Displacement()
        {
            Surface = new DispSurface();
        }

        public int GetSize()
        {
            return (((1 << Power) + 1) * ((1 << Power) + 1));
        }

        public int GetTris()
        {
            return ((Height - 1) * (Width - 1) * 2);
        }

        int Height { get { return ( ( 1 << Power ) + 1 ); } }
        int Width { get { return ( ( 1 << Power ) + 1 ); } }
        int PostSpacing { get { return ((1 << Power) + 1); } }


        public void Create()
        {
            // generate the displacement surface
            GenerateDispSurf();

            GenerateDispNormals();

            GenerateDispSurfTangentSpaces();

            CalcDispSurfCoords(false, 0);

            for (int i = 0; i < 4; i++)
            {
                CalcDispSurfCoords(true, i);
            }

            GenerateCollisionSurface();

            CreateTris();
        }

        void CreateTris()
        {
            int triCount = GetTris();
            for (int iTri = 0, iRender = 0; iTri < triCount; ++iTri, iRender += 3)
            {
                Tris[iTri].m_iIndex[0] = (ushort)RenderIndices[iRender];
                Tris[iTri].m_iIndex[1] = (ushort)RenderIndices[iRender+1];
                Tris[iTri].m_iIndex[2] = (ushort)RenderIndices[iRender+2];
            }
        }

        void GenerateCollisionSurface()
        {
            // get width and height of displacement maps
            int width = Width;
            int height = Height;

            //
            // generate a fan tesselated (at quadtree node) rendering index list
            //
            RenderIndexCount = 0;
            for (int iV = 0; iV < (height - 1); iV++)
            {
                for (int iU = 0; iU < width - 1; iU++)
                {
                    int ndx = (iV * width) + iU;

                    // test whether or not the index is odd
                    bool odd = ((ndx % 2) == 1);

                    // Top Left to Bottom Right
                    if (odd)
                    {
                        BuildTriTLtoBR(ndx);
                    }
                    // Bottom Left to Top Right
                    else
                    {
                        BuildTriBLtoTR(ndx);
                    }
                }
            }
        }

        void BuildTriTLtoBR(int ndx)
        {
            // get width and height of displacement maps
            int width = Width;

            RenderIndices[RenderIndexCount] = ndx;
            RenderIndices[RenderIndexCount + 1] = ndx + width;
            RenderIndices[RenderIndexCount + 2] = ndx + 1;
            RenderIndexCount += 3;

            RenderIndices[RenderIndexCount] = ndx + 1;
            RenderIndices[RenderIndexCount + 1] = ndx + width;
            RenderIndices[RenderIndexCount + 2] = ndx + width + 1;
            RenderIndexCount += 3;
        }

        void BuildTriBLtoTR( int ndx )
        {
            // get width and height of displacement maps
            int width = Width;

            RenderIndices[RenderIndexCount] = ndx;
            RenderIndices[RenderIndexCount + 1] = ndx + width;
            RenderIndices[RenderIndexCount + 2] = ndx + width + 1;
            RenderIndexCount += 3;

            RenderIndices[RenderIndexCount] = ndx;
            RenderIndices[RenderIndexCount + 1] = ndx + width + 1;
            RenderIndices[RenderIndexCount + 2] = ndx + 1;
            RenderIndexCount += 3;
        }

        void CalcDispSurfCoords(bool lightMap, int lightmapId)
        {
            //
            // get base surface texture coords
            //
            Vector2[] texCoords = new Vector2[4];
            Vector2[] luxelCoords = new Vector2[4];
            DispSurface Surf = Surface;

            for (int i = 0; i < 4; i++)
            {
                texCoords[i] = Surf.m_TexCoords[i];
                luxelCoords[i] = Surf.m_LuxelCoords[lightmapId, i];
            }

            //
            // get images width and intervals along the edge
            //
            int postSpacing = PostSpacing;
            float ooInt = 1.0f / (postSpacing - 1.0f);

            //
            // calculate the parallel edge intervals
            //
            Vector2[] edgeInt = new Vector2[2];
            if (!lightMap)
            {
                edgeInt[0] = texCoords[1] - texCoords[0];
                edgeInt[1] = texCoords[2] - texCoords[3];
            }
            else
            {
                edgeInt[0] = luxelCoords[1] - luxelCoords[0];
                edgeInt[1] = luxelCoords[2] - luxelCoords[3];
            }
            edgeInt[0] *= ooInt;
            edgeInt[1] *= ooInt;

            //
            // calculate the displacement points
            //    
            for (int i = 0; i < postSpacing; i++)
            {
                //
                // position along parallel edges (start and end for a perpendicular segment)
                //
                Vector2[] endPts = new Vector2[2];
                endPts[0] = edgeInt[0] * (float)i;
                endPts[1] = edgeInt[1] * (float)i;
                if (!lightMap)
                {
                    endPts[0] += texCoords[0];
                    endPts[1] += texCoords[3];
                }
                else
                {
                    endPts[0] += luxelCoords[0];
                    endPts[1] += luxelCoords[3];
                }

                //
                // interval length for perpendicular edge
                //
                Vector2 seg = endPts[1] - endPts[0];
                Vector2 segInt = seg * ooInt;

                //
                // calculate the material (texture or light) coordinate at each point
                //
                for (int j = 0; j < postSpacing; j++)
                {
                    seg = segInt * (float)j;

                    if (!lightMap)
                        Verts[i * postSpacing + j].m_TexCoord = endPts[0] + seg;
                    else
                        Verts[i * postSpacing + j].m_LuxelCoords[lightmapId] = endPts[0] + seg;
                }
            }


        }

        public void GenerateDispSurfTangentSpaces()
        {

        }

        //-----------------------------------------------------------------------------
        // Purpose: This function determines if edges exist in each of the directions
        //          off of the given point (given in component form).  We know ahead of
        //          time that there are only 4 possibilities.
        //
        //            1     "directions"
        //          0 + 2
        //            3
        //
        //   Input: indexRow - row position
        //          indexCol - col position
        //          direction - the direction (edge) currently being evaluated
        //          postSpacing - the number of intervals in the row and col directions
        //  Output: the edge existed? (true/false)
        //-----------------------------------------------------------------------------
        bool DoesEdgeExist( int indexRow, int indexCol, int direction, int postSpacing )
        {
            switch( direction )
            {
                case 0:
                    // left edge
                    if( ( indexRow - 1 ) < 0 )
                        return false;
                    return true;
                case 1:
                    // top edge
                    if( ( indexCol + 1 ) > ( postSpacing - 1 ) )
                        return false;
                    return true;
                case 2:
                    // right edge
                    if( ( indexRow + 1 ) > ( postSpacing - 1 ) )
                        return false;
                    return true;
                case 3:
                    // bottom edge
                    if( ( indexCol - 1 ) < 0 )
                        return false;
                    return true;
                default:
                    return false;
            }
        }

        public Vector3 CalcNormalFromEdges(int indexRow, int indexCol, bool[] isEdge)
        {
            // get the post spacing (size/interval of displacement surface)
            int postSpacing = PostSpacing;

            // initialize the normal accumulator - counter
            Vector3 accumNormal = Vector3.Zero;
            int normalCount = 0;

            Vector3[] tmpVect = new Vector3[2];
            Vector3 tmpNormal = Vector3.Zero;

            //
            // check quadrant I (posX, posY)
            //
            if (isEdge[1] && isEdge[2])
            {
                // tri i
                tmpVect[0] = (Verts[(indexCol + 1) * postSpacing + indexRow].m_Vert) - (Verts[indexCol * postSpacing + indexRow].m_Vert);
                tmpVect[1] = (Verts[indexCol * postSpacing + (indexRow + 1)].m_Vert) - (Verts[indexCol * postSpacing + indexRow].m_Vert);
                tmpNormal = Vector3.Cross(tmpVect[1], tmpVect[0]);
                tmpNormal.Normalize();
                accumNormal += tmpNormal;
                normalCount++;

                // tri 2
                tmpVect[0] = (Verts[(indexCol + 1) * postSpacing + indexRow].m_Vert) - (Verts[indexCol * postSpacing + (indexRow + 1)].m_Vert);
                tmpVect[1] = (Verts[(indexCol + 1) * postSpacing + (indexRow + 1)].m_Vert) - (Verts[indexCol * postSpacing + (indexRow + 1)].m_Vert);
                tmpNormal = Vector3.Cross(tmpVect[1], tmpVect[0]);
                tmpNormal.Normalize();
                accumNormal += tmpNormal;
                normalCount++;
            }

            //
            // check quadrant II (negX, posY)
            //
            if (isEdge[0] && isEdge[1])
            {
                // tri i
                tmpVect[0] = (Verts[(indexCol + 1) * postSpacing + (indexRow - 1)].m_Vert) - (Verts[indexCol * postSpacing + (indexRow - 1)].m_Vert);
                tmpVect[1] = (Verts[indexCol * postSpacing + indexRow].m_Vert) - (Verts[indexCol * postSpacing + (indexRow - 1)].m_Vert);

                tmpNormal = Vector3.Cross(tmpVect[1], tmpVect[0]);
                tmpNormal.Normalize();
                accumNormal += tmpNormal;
                normalCount++;

                // tri 2
                tmpVect[0] = (Verts[(indexCol + 1) * postSpacing + (indexRow - 1)].m_Vert) - (Verts[indexCol * postSpacing + indexRow].m_Vert);
                tmpVect[1] = (Verts[(indexCol + 1) * postSpacing + indexRow].m_Vert) - (Verts[indexCol * postSpacing + indexRow].m_Vert);
                tmpNormal = Vector3.Cross(tmpVect[1], tmpVect[0]);
                tmpNormal.Normalize();
                accumNormal += tmpNormal;
                normalCount++;
            }

            //
            // check quadrant III (negX, negY)
            //
            if (isEdge[0] && isEdge[3])
            {
                // tri i
                tmpVect[0] = (Verts[indexCol * postSpacing + (indexRow - 1)].m_Vert) - (Verts[(indexCol - 1) * postSpacing + (indexRow - 1)].m_Vert);
                tmpVect[1] = (Verts[(indexCol - 1) * postSpacing + indexRow].m_Vert) - (Verts[(indexCol - 1) * postSpacing + (indexRow - 1)].m_Vert);
                tmpNormal = Vector3.Cross(tmpVect[1], tmpVect[0]);
                tmpNormal.Normalize();
                accumNormal += tmpNormal;
                normalCount++;

                // tri 2
                tmpVect[0] = (Verts[indexCol * postSpacing + (indexRow - 1)].m_Vert) - (Verts[(indexCol - 1) * postSpacing + indexRow].m_Vert);
                tmpVect[1] = (Verts[indexCol * postSpacing + indexRow].m_Vert) - (Verts[(indexCol - 1) * postSpacing + indexRow].m_Vert);                
                tmpNormal = Vector3.Cross(tmpVect[1], tmpVect[0]);
                tmpNormal.Normalize();
                accumNormal += tmpNormal;
                normalCount++;
            }

            //
            // check quadrant IV (posX, negY)
            //
            if (isEdge[2] && isEdge[3])
            {
                // tri i
                tmpVect[0] = (Verts[indexCol * postSpacing + indexRow].m_Vert) - (Verts[(indexCol - 1) * postSpacing + indexRow].m_Vert);
                tmpVect[1] = (Verts[(indexCol - 1) * postSpacing + (indexRow + 1)].m_Vert) - (Verts[(indexCol - 1) * postSpacing + indexRow].m_Vert);
                tmpNormal = Vector3.Cross(tmpVect[1], tmpVect[0]);
                tmpNormal.Normalize();
                accumNormal += tmpNormal;
                normalCount++;

                // tri 2
                tmpVect[0] = (Verts[indexCol * postSpacing + indexRow].m_Vert) - (Verts[(indexCol - 1) * postSpacing + (indexRow + 1)].m_Vert);
                tmpVect[1] = (Verts[indexCol * postSpacing + (indexRow + 1)].m_Vert) - (Verts[(indexCol - 1) * postSpacing + (indexRow + 1)].m_Vert);
                tmpNormal = Vector3.Cross(tmpVect[1], tmpVect[0]);
                tmpNormal.Normalize();
                accumNormal += tmpNormal;
                normalCount++;
            }

            Vector3 normal = accumNormal * (1.0f / (float)normalCount);
            return normal;
        }

        public void GenerateDispNormals()
        {
            int postSpacing = PostSpacing;

            //
            // generate the normals at each displacement surface vertex
            //
            for (int i = 0; i < postSpacing; i++)
            {
                for (int j = 0; j < postSpacing; j++)
                {
                    bool[] isEdge = new bool[4];

                    // edges
                    for (int k = 0; k < 4; k++)
                    {
                        isEdge[k] = DoesEdgeExist(j, i, k, postSpacing);
                    }

                    Vector3 normal = CalcNormalFromEdges(j, i, isEdge);

                    // save generated normal
                    Verts[i * postSpacing + j].m_Normal = normal;
                }
            }
        }

        public void GenerateDispSurf()
        {
            Vector3[] points = Surface.m_Points;

            //
            // get the spacing (interval = width/height, are equal because it is uniform) along the edge
            //
            int postSpacing = PostSpacing;
            float ooInt = 1.0f / (float)(postSpacing - 1f);

            //
            // calculate the opposite edge intervals
            //
            Vector3[] edgeInt = new Vector3[2];
            edgeInt[0] = (points[1] - points[0])*ooInt;
            edgeInt[1] = (points[2] - points[3]) * ooInt;

            Vector3 elevNormal = Vector3.Zero;
            if (Elevation != 0.0f)
            {
                elevNormal = Surface.GetNormal();
                elevNormal = Vector3.Multiply(elevNormal, Elevation);
            }

            //
            // calculate the displaced vertices
            //
            for (int i = 0; i < postSpacing; i++)
            {
                //
                // calculate segment interval between opposite edges
                //
                Vector3[] endPts = new Vector3[2];
                endPts[0] = (edgeInt[0] * (float)i) + points[0];
                endPts[1] = (edgeInt[1] * (float)i) + points[3];

                Vector3 seg = (endPts[1] - endPts[0]);
                Vector3 segInt = seg *  ooInt;
                //
                // calculate the surface vertices
                //
                for (int j = 0; j < postSpacing; j++)
                {
                    int ndx = i * postSpacing + j;

                    CoreDispVert pVert = Verts[ndx];

                    // calculate the flat surface position -- saved separately
                    pVert.m_FlatVert = endPts[0] + (segInt * (float)j);

                    // start with the base surface position
                    pVert.m_Vert = pVert.m_FlatVert;

                    // add the elevation vector -- if it exists
                    if (Elevation != 0.0f)
                    {
                        pVert.m_Vert += elevNormal;
                    }

                    // add the subdivision surface position
                    pVert.m_Vert += pVert.m_SubdivPos;

                    // add the displacement field direction(normalized) and distance
                    pVert.m_Vert += pVert.m_FieldVector * pVert.m_FieldDistance;
                }
            }
        }

        

        public void InitDispInfo(int power, int minTess, float smoothingAngle, List<dDispVert> verts, List<dDispTri> tris) 
        {
            int MAX_DISPVERTS = NumDispPowerVerts(4);
            Vector3[] vecs = new Vector3[MAX_DISPVERTS];
            float[] dists = new float[MAX_DISPVERTS];
            float[] alphas = new float[MAX_DISPVERTS];

            int nVerts = NumDispPowerVerts(power);
            for (int i = 0; i < nVerts; i++)
            {
                vecs[i] = verts[i].vec;
                dists[i] = verts[i].dist;
                alphas[i] = verts[i].alpha;
            }

            InitDispInfo(power, minTess, smoothingAngle, alphas, vecs, dists);

            int nTris = GetTris();
            for (int i = 0; i < nTris; i++)
            {
                Tris[i].m_uiTags = tris[i].Tags;
            }
        }

        void InitDispInfo(int power, int minTess, float smoothingAngle, float[] alphas, Vector3[] vecs, float[] dists)
        {
            this.Power = power;

            int size = GetSize();
            this.Verts = new CoreDispVert[size];
            int indexCount = size * 2 * 3;

            RenderIndices = new int[indexCount];

            int nNodeCount = GetNodeCount(power);
            Nodes = new CoreDispNode[nNodeCount];

            for (int i = 0; i < size; i++)
            {
                Verts[i] = new CoreDispVert();
            }

            for (int i = 0; i < indexCount; i++)
            {
                RenderIndices[i] = 0;
            }

            for (int i = 0; i < nNodeCount; i++)
            {
                Nodes[i] = new CoreDispNode();
            }

            //
            // save the displacement vector field and distances within the field
            // offset have been combined with fieldvectors at this point!!!
            //
            if (alphas != null && vecs != null && dists != null)
            {
                for (int i = 0; i < size; i++)
                {
                    Verts[i].m_FieldVector = vecs[i];
                    Verts[i].m_FieldDistance = dists[i];
                    Verts[i].m_Alpha = alphas[i];
                }
            }

            // Init triangle information.
            int nTriCount = GetTris();
            if (nTriCount != 0)
            {
                Tris = new CoreDispTri[nTriCount];
                for (int i = 0; i < nTriCount; i++)
                {
                    Tris[i] = new CoreDispTri();
                }
            }
        }

        public static int NumDispPowerVerts(int power)
        {
            return (((1 << (power)) + 1) * ((1 << (power)) + 1));
        }

        public static int NumDispPowerTris(int power)
        {
            return ((1 << (power)) * (1 << (power)) * 2);
        }

        public static int GetNodeCount(int power)
        {
            return ((1 << (power << 1)) / 3);
        }
    }

    public class DispSurface
    {
        public int m_Index;																// parent face (CMapFace, dface_t, msurface_t) index "handle"
	
	    public int			m_PointCount;															// number of points in the face (should be 4!)
        public Vector3[] m_Points = new Vector3[4];												// points
        public Vector3[] m_Normals = new Vector3[4];													// normals at points
        public Vector2[] m_TexCoords = new Vector2[4];													// texture coordinates at points
        public Vector2[,] m_LuxelCoords = new Vector2[4,4];								// lightmap coordinates at points
        public float[] m_Alphas = new float[4];										// alpha at points

	    // Straight from the BSP file.	
        public DisplaceNeighbor[] m_EdgeNeighbors = new DisplaceNeighbor[4];
        public DisplaceCornerNeighbor[] m_CornerNeighbors = new DisplaceCornerNeighbor[4];

        public int m_Flags;																// surface flags - inherited from the "parent" face
        public int m_Contents;																// contents flags - inherited from the "parent" face

        public Vector3 sAxis = Vector3.Zero;																	// used to generate start disp orientation (old method)
        public Vector3 tAxis = Vector3.Zero;																	// used to generate start disp orientation (old method)
        public int m_PointStartIndex = -1;														// index to the starting point -- for saving starting point
        public Vector3 m_PointStart;															// starting point used to determine the orientation of the displacement map on the surface

        public Vector3 GetNormal()
        {
            //
            // calculate the displacement surface normal
            //
            Vector3[] tmp = new Vector3[2];
            tmp[0] = m_Points[1] - m_Points[0];
            tmp[1] = m_Points[3] - m_Points[0];

            Vector3 normal = Vector3.Cross(tmp[1], tmp[0]);
            normal.Normalize();

            return normal;
        }

        // Find corner closest to the start point
        public int FindSurfPointStartIndex()
        {
            if (m_PointStartIndex != -1)
                return m_PointStartIndex;

            int minIndex = -1;
            float minDist = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 segment = m_PointStart - m_Points[i];
                float distSq = segment.LengthSquared();
                if (distSq < minDist)
                {
                    minDist = distSq;
                    minIndex = i;
                }
            }

            m_PointStartIndex = minIndex;
            return minIndex;
        }

        public void AdjustSurfPointData()
        {
            Vector3[] tmpPoints = new Vector3[4];
	        Vector3[] tmpNormals= new Vector3[4];
	        Vector2[] tmpTexCoords= new Vector2[4];
	        Vector2[,] tmpLuxelCoords= new Vector2[4,4];
	        float[]  tmpAlphas = new float[4];

            int i;
            for (i = 0; i < 4; i++)
            {
                tmpPoints[i] = m_Points[i];
                tmpNormals[i] = m_Normals[i];
                tmpTexCoords[i] = m_TexCoords[i];

                for (int j = 0; j < 4; j++)
                {
                    tmpLuxelCoords[j, i] = m_LuxelCoords[j, i];
                }

                tmpAlphas[i] = m_Alphas[i];
            }

            for (i = 0; i < 4; i++)
            {
                m_Points[i] = tmpPoints[(i + m_PointStartIndex) % 4];
                m_Normals[i] = tmpNormals[(i + m_PointStartIndex) % 4];
                m_TexCoords[i] = tmpTexCoords[(i + m_PointStartIndex) % 4];
                m_Alphas[i] = tmpAlphas[i];
            }
        }
    }

    public class DispCollTree
    {
        public const int DISPCOLL_TRILIST_SIZE = 256;
        public const int NEVER_UPDATED = -999999;
        public int Power;
        public Vector3[] m_SurfPoints = new Vector3[4];		// Base surface points.
        public int m_Contents;				// the displacement surface "contents" (solid, etc...)
        public ushort m_iLatestSurfProp;		//
        public short[] m_SurfaceProps = new short[2];		// surface properties (save off from texdata for impact responses)

        public Vector3 m_StabDir;				// the direction to stab for this displacement surface (is the base face normal)
        public Vector3[] m_BBoxWithFace = new Vector3[2];		// the bounding box of the displacement surface and base face

        public int[] m_CheckCount = new int[2];			// per frame collision flag (so we check only once)

        public short m_VertCount;			// number of vertices on displacement collision surface
        public Vector3[] m_pVerts;				// list of displacement vertices
        public Vector3[] m_pOriginalVerts;		// Original vertex positions, used for limiting terrain mods.
        public Vector3[] m_pVertNormals;		// list of displacement vertex normals

        public ushort m_nTriCount;			// number of triangles on displacement collision surface
        public Tri_t[] m_pTris;				// displacement surface triangles

        public short m_NodeCount;			// number of nodes in displacement collision tree
        public Node_t[] m_pNodes;				// list of nodes

	    public DispLeafLink		m_pLeafLinkHead;		// List that links it into the leaves.

        class AABB_t
	    {
		    public List<Vector3>	Normals = new List<Vector3>();
            public float[] Dists = new float[6];//[DISPCOLL_AABB_SIDE_COUNT];
	    }

        // Displacement collision triangle data.
	    public class Tri_t
	    {	
		    public Vector3			m_vecNormal;				// triangle face plane normal
            public float m_flDist;					// traingle face plane distance
            public ushort[] m_uiVerts = new ushort[3];				// triangle vert/vert normal indices
            public ushort m_nFlags;					// triangle surface flags
            public ushort m_iSurfProp;				// 0 or 1
	    };

        public class TriList_t
	    {
		    public short	Count = 0;
            public Tri_t[] ppTriList = new Tri_t[DISPCOLL_TRILIST_SIZE];//[DISPCOLL_TRILIST_SIZE];
	    }

	    public class Node_t
	    {
		    public Vector3[]			m_BBox = new Vector3[2];//[2];
            public short[] m_iTris = new short[2];//[2];
            public int m_fFlags;
	    }

        public bool AABBSweep(Vector3 start, Vector3 end, Vector3 extends,
                              float startf, float endf, ref trace_t trace)
        {
            //
            // create and initialize the triangle list
            //
            TriList_t trilist = new TriList_t();

            //
            // create and initialize the primary AABB
            //
            AABB_t AABBox = new AABB_t();

            //
            // sweep box against the axial-aligned bboxed quad-tree and generate an initial
            // list of collision tris
            //
            SweptAABB_BuildTriList(start, end, extends, 0, AABBox, trilist);

            // save the starting fraction
            float preIntersectionFrac = trace.fraction;

            //
            // sweep axis-aligned bounding box against the triangles in the list
            //
            if (trilist.Count > 0)
            {
                SweptAABB_IntersectTriList(start, end, extends, startf, endf, ref trace, trilist);
            }

            // collision
            if (preIntersectionFrac > trace.fraction)
                return true;

            // no collision
            return false;
        }

        void SweptAABB_IntersectTriList(Vector3 start, Vector3 end, Vector3 extends, float startf, float endf, ref trace_t trace, TriList_t trilist)
        {
            Vector3 impactNormal = new Vector3();
            float impactDist = 0f;
            Tri_t tri;

            //
            // intersect against all the flagged triangles in trilist
            //
            for (int iTri = 0; iTri < trilist.Count; iTri++)
            {
                // get the current displacement
                tri = trilist.ppTriList[iTri];

                // intersection test
                IntersectAABoxSweptTriangle(start, end, extends,
                                     m_pVerts[tri.m_uiVerts[0]],
                                     m_pVerts[tri.m_uiVerts[2]],
                                     m_pVerts[tri.m_uiVerts[1]],
                                     tri.m_vecNormal, tri.m_flDist,
                                     tri.m_nFlags, tri.m_iSurfProp,
                    /*fraction,*/ref trace, true);

            }

        }

        void IntersectAABoxSweptTriangle(Vector3 start, Vector3 end, Vector3 extends,
                                         Vector3 v1, Vector3 v2, Vector3 v3, Vector3 triNormal,
                                         float triDist, ushort triFlags, ushort triSurfProp, ref trace_t trace, bool startsOutside)
        {
            //
	        // make sure the box and triangle are not initially intersecting!!
	        // NOTE: if bStartOutside is set -- this is assumed
	        //
	        if( !startsOutside )
	        {
		        // check for interection -- if not intersecting continue, otherwise
		        // return and let the "in solid" functions handle it
	        }

	        //
	        // initialize the axial-aligned box sweep triangle test
	        //
	        bool  bStartOutSolid = false;
	        bool  bEndOutSolid = false;
	        float fracStart = NEVER_UPDATED;
	        float fracEnd = 1.0f;

	        // calculate the box direction - the sweep direction
	        Vector3 boxDir = end - start;

	        //
	        // OPTIMIZATION: make sure objects are traveling toward one another
	        //
            float angle = Vector3.Dot(triNormal, boxDir);// triNormal.Dot(boxDir);
	        if( angle/*triNormal.Dot( boxDir )*/ > ClipMap.EPSILON )
	        {
		        return;
	        }
            // test against the triangle face plane
            if (!FacePlane(triNormal, triDist, start, end, extends, ref fracStart, ref fracEnd, v1, v2, v3,
                            bStartOutSolid, bEndOutSolid)) { return; }

            // test against axial planes (of the aabb)
            if (!AxialPlanesXYZ(v1, v2, v3, start, end, extends, boxDir, triNormal, ref fracStart, ref fracEnd,
                                 bStartOutSolid, bEndOutSolid)) { return; }

            //
            // There are 9 edge tests - edges 1, 2, 3 cross with the box edges (symmetry) 1, 2, 3.  However, the box
            // is axial-aligned resulting in axially directional edges -- thus each test is edges 1, 2, and 3 vs. 
            // axial planes x, y, and z
            //
            // There are potentially 9 more tests with edges, the edge's edges and the direction of motion!
            // NOTE: I don't think these tests are necessary for a manifold surface -- but they still remain if
            // it ever becomes a problem in the future!
            //
            Vector3 edge;

            // edge 1 - axial tests are 2d tests, swept direction is a 3d test
            edge = v2 - v1;
            if (!EdgeCrossAxialX(edge, v1, v3, extends, start, end, triNormal, triDist, ref fracStart, ref fracEnd, bStartOutSolid, bEndOutSolid, 4)) { return; }
            if (!EdgeCrossAxialY(edge, v1, v3, extends, start, end, triNormal, triDist, ref fracStart, ref fracEnd, bStartOutSolid, bEndOutSolid, 5)) { return; }
            if (!EdgeCrossAxialZ(edge, v1, v3, extends, start, end, triNormal, triDist, ref fracStart, ref fracEnd, bStartOutSolid, bEndOutSolid, 6)) { return; }
            //	if( !EdgeCrossSweptDir( edge, v1, v3, boxDir, boxExtents, boxStart, boxEnd, fracStart, fracEnd ) ) { fraction = fracStart; return; }

            // edge 2 - axial tests are 2d tests, swept direction is a 3d test
            edge = v3 - v2;
            if (!EdgeCrossAxialX(edge, v2, v1, extends, start, end, triNormal, triDist, ref  fracStart, ref fracEnd, bStartOutSolid, bEndOutSolid, 7)) { return; }
            if (!EdgeCrossAxialY(edge, v2, v1, extends, start, end, triNormal, triDist, ref fracStart, ref fracEnd, bStartOutSolid, bEndOutSolid, 8)) { return; }
            if (!EdgeCrossAxialZ(edge, v2, v1, extends, start, end, triNormal, triDist, ref fracStart, ref fracEnd, bStartOutSolid, bEndOutSolid, 9)) { return; }
            //	if( !EdgeCrossSweptDir( edge, v2, v1, boxDir, boxExtents, boxStart, boxEnd, fracStart, fracEnd ) ) { fraction = fracStart; return; }

            // edge 3 - axial tests are 2d tests, swept direction is a 3d test
            edge = v1 - v3;
            if (!EdgeCrossAxialX(edge, v3, v2, extends, start, end, triNormal, triDist, ref fracStart, ref fracEnd, bStartOutSolid, bEndOutSolid, 10)) { return; }
            if (!EdgeCrossAxialY(edge, v3, v2, extends, start, end, triNormal, triDist, ref fracStart, ref fracEnd, bStartOutSolid, bEndOutSolid, 11)) { return; }
            if (!EdgeCrossAxialZ(edge, v3, v2, extends, start, end, triNormal, triDist, ref fracStart, ref fracEnd, bStartOutSolid, bEndOutSolid, 12)) { return; }
            //	if( !EdgeCrossSweptDir( edge, v2, v1, boxDir, boxExtents, boxStart, boxEnd, fracStart, fracEnd ) ) { fraction = fracStart; return; }

            //
            // the direction of motion crossed with the axial planes is equivolent
            // to cross the box (axial planes) and the 
            //
            //	if( !DirectionOfMotionCrossAxialPlanes( boxStart, boxEnd, boxDir, boxExtents, fracStart, fracEnd, v1, v2, v3 ) ) { fraction = fracStart; return; }

            //
            // didn't have a separating axis -- update trace data -- should I handle a fraction left solid here!????
            //
            if (fracStart < fracEnd)
            {
                if ((fracStart > NEVER_UPDATED) && (fracStart < trace.fraction))
                {
                    // clamp -- shouldn't really ever be here!???
                    if (fracStart < 0.0f)
                    {
                        fracStart = 0.0f;
                    }

                    trace.fraction = fracStart;
                    trace.plane.normal = triNormal;
                    trace.plane.dist = triDist;
                    //trace.dispFlags = triFlags;
                    m_iLatestSurfProp = triSurfProp;
                }
            }
        }

        bool EdgeCrossAxialX(Vector3 edge, Vector3 ptOnEdge, Vector3 ptOffEdge, Vector3 extents,
                            Vector3 boxStart, Vector3 boxEnd, Vector3 triNormal, float triDist,
                            ref float fracStart, ref float fracEnd, bool startOutSolid, bool endOutSolid, int index)
        {
            // calculate the normal - edge x axialX = ( 0.0, edgeZ, -edgeY )
            Vector3 normal = new Vector3(0f, edge.Z, -edge.Y);
            normal.Normalize();

            // check for zero length normals
            if (normal.Equals(Vector3.Zero))
                return true;

            // finish the plane definition - get distance
            float dist = (normal.Y * ptOnEdge.Y) + (normal.Z * ptOnEdge.Z);

            // special case the point off edge in plane
            float ptOffDist = (normal.Y * ptOffEdge.Y) + (normal.Z * ptOffEdge.Z);
            if (Math.Abs(ptOffDist - dist) < ClipMap.EPSILON)
            {
                normal = triNormal;
                dist = triDist;
            }
            // adjust plane facing if necessay - triangle should be behind the plane
            else if (ptOffDist > dist)
            {
                normal.Y = -normal.Y;
                normal.Z = -normal.Z;
                dist = -dist;
            }

            // calculate the closest point on box to plane (get extents in that direction)
            Vector3 ptExtents = Vector3.Zero;
            CalcClosestExtents(normal, extents, ref ptExtents);

            //
            // expand the plane by the extents of the box to reduce the swept box/triangle
            // test to a ray/extruded triangle test (one of the triangles extruded planes
            // was just calculated above
            //
            float expandDist = dist - ((normal.Y * ptExtents.Y) + (normal.Z * ptExtents.Z));
            float distStart = (normal.Y * boxStart.Y) + (normal.Z * boxStart.Z) - expandDist;
            float distEnd = (normal.Y * boxEnd.Y) + (normal.Z * boxEnd.Z) - expandDist;

            Vector3 boxDir = boxEnd - boxStart;
            boxDir.Normalize();

            // resolve the ray/plane collision
            if (!ResolveRayPlaneIntersect(distStart, distEnd, ref fracStart, ref fracEnd, boxDir, normal, expandDist, startOutSolid, endOutSolid, index))
                return false;

            return true;

        }

        bool EdgeCrossAxialY(Vector3 edge, Vector3 ptOnEdge, Vector3 ptOffEdge, Vector3 extents,
                            Vector3 boxStart, Vector3 boxEnd, Vector3 triNormal, float triDist,
                            ref float fracStart, ref float fracEnd, bool startOutSolid, bool endOutSolid, int index)
        {
            // calculate the normal - edge x axialX = ( 0.0, edgeZ, -edgeY )
            Vector3 normal = new Vector3(-edge.Z, 0.0f, edge.X);
            normal.Normalize();

            // check for zero length normals
            if (normal.Equals(Vector3.Zero))
                return true;

            // finish the plane definition - get distance
            float dist = (normal.X * ptOnEdge.X) + (normal.Z * ptOnEdge.Z);
            float ptOffDist = (normal.X * ptOffEdge.X) + (normal.Z * ptOffEdge.Z);

            // special case the point off edge in plane
            if (Math.Abs(ptOffDist - dist) < ClipMap.EPSILON)
            {
                normal = triNormal;
                dist = triDist;
            }
            // adjust plane facing if necessay - triangle should be behind the plane
            else if (ptOffDist > dist)
            {
                normal.X = -normal.X;
                normal.Z = -normal.Z;
                dist = -dist;
            }

            // calculate the closest point on box to plane (get extents in that direction)
            Vector3 ptExtents = Vector3.Zero;
            CalcClosestExtents(normal, extents, ref ptExtents);

            //
            // expand the plane by the extents of the box to reduce the swept box/triangle
            // test to a ray/extruded triangle test (one of the triangles extruded planes
            // was just calculated above
            //
            float expandDist = dist - ((normal.X * ptExtents.X) + (normal.Z * ptExtents.Z));
            float distStart = (normal.X * boxStart.X) + (normal.Z * boxStart.Z) - expandDist;
            float distEnd = (normal.X * boxEnd.X) + (normal.Z * boxEnd.Z) - expandDist;

            Vector3 boxDir = boxEnd - boxStart;
            boxDir.Normalize();

            // resolve the ray/plane collision
            if (!ResolveRayPlaneIntersect(distStart, distEnd, ref fracStart, ref fracEnd, boxDir, normal, expandDist, startOutSolid, endOutSolid, index))
                return false;

            return true;
        }

        bool EdgeCrossAxialZ(Vector3 edge, Vector3 ptOnEdge, Vector3 ptOffEdge, Vector3 extents,
                            Vector3 boxStart, Vector3 boxEnd, Vector3 triNormal, float triDist,
                            ref float fracStart, ref float fracEnd, bool startOutSolid, bool endOutSolid, int index)
        {
            // calculate the normal - edge x axialX = ( 0.0, edgeZ, -edgeY )
            Vector3 normal = new Vector3(edge.Y, -edge.X, 0f);
            normal.Normalize();

            // check for zero length normals
            if (normal.Equals(Vector3.Zero))
                return true;

            // finish the plane definition - get distance
            float dist = (normal.X * ptOnEdge.X) + (normal.Y * ptOnEdge.Y);

            // special case the point off edge in plane
            float ptOffDist = (normal.X * ptOffEdge.X) + (normal.Y * ptOffEdge.Y);
            if (Math.Abs(ptOffDist - dist) < ClipMap.EPSILON)
            {
                normal = triNormal;
                dist = triDist;
            }
            // adjust plane facing if necessay - triangle should be behind the plane
            else if (ptOffDist > dist)
            {
                normal.Y = -normal.Y;
                normal.X = -normal.X;
                dist = -dist;
            }

            // calculate the closest point on box to plane (get extents in that direction)
            Vector3 ptExtents = Vector3.Zero;
            CalcClosestExtents(normal, extents, ref ptExtents);

            //
            // expand the plane by the extents of the box to reduce the swept box/triangle
            // test to a ray/extruded triangle test (one of the triangles extruded planes
            // was just calculated above
            //
            float expandDist = dist - ((normal.X * ptExtents.X) + (normal.Y * ptExtents.Y));
            float distStart = (normal.X * boxStart.X) + (normal.Y * boxStart.Y) - expandDist;
            float distEnd = (normal.X * boxEnd.X) + (normal.Y * boxEnd.Y) - expandDist;

            Vector3 boxDir = boxEnd - boxStart;
            boxDir.Normalize();

            // resolve the ray/plane collision
            if (!ResolveRayPlaneIntersect(distStart, distEnd, ref fracStart, ref fracEnd, boxDir, normal, expandDist, startOutSolid, endOutSolid, index))
                return false;

            return true;
        }

        bool AxialPlanesXYZ(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 boxStart, Vector3 boxEnd,
                            Vector3 extents, Vector3 sweptdir, Vector3 triNormal, ref float fracstart, ref float fracend, bool startOutSolid, bool endOutSolid)
        {
            // verify
            Vector3 boxDir = sweptdir;
            boxDir.Normalize();

            //
            // test axial planes (x, y, z)
            //
            Vector3 boxPt = new Vector3();
            CalcClosestBoxPoint(triNormal, boxStart, extents, ref boxPt);

            float dist, distStart, distEnd, closeValue = 0f;

            for (int axis = 0; axis < 3; axis++)
            {
                if (triNormal[axis] > 0.0f)
                {
                    Vector3 normal = Vector3.Zero;
                    normal[axis] = 1.0f;

                    FindMax(v1[axis], v2[axis], v3[axis], ref closeValue);
                    dist = closeValue + extents[axis];

                    distStart = boxPt[axis] - closeValue;
                    distEnd = (boxPt[axis] + sweptdir[axis]) - closeValue;

                    if (!ResolveRayPlaneIntersect(distStart, distEnd, ref fracstart, ref fracend, boxDir, normal, dist, startOutSolid, endOutSolid, axis + 1))
                        return false;
                }
                else
                {
                    Vector3 normal = Vector3.Zero;
                    normal[axis] = -1.0f;

                    FindMin(v1[axis], v2[axis], v3[axis], ref closeValue);
                    dist = closeValue - extents[axis];

                    distStart = closeValue - boxPt[axis];
                    distEnd = closeValue - (boxPt[axis] + sweptdir[axis]);

                    if (!ResolveRayPlaneIntersect(distStart, distEnd, ref fracstart, ref fracend, boxDir, normal, dist, startOutSolid, endOutSolid, axis + 1))
                        return false;
                }
            }

            return true;
        }

        static void FindMin(float v1, float v2, float v3, ref float min)
        {
            min = v1;
            if (v2 < min) min = v2;
            if (v3 < min) min = v3;
        }

        static void FindMax(float v1, float v2, float v3, ref float max)
        {
            max = v1;
            if (v2 > max) max = v2;
            if (v3 > max) max = v3;
        }

        bool FacePlane(Vector3 triNormal, float triDist, Vector3 start, Vector3 end, Vector3 extends, ref float startf, ref float endf, Vector3 v1, Vector3 v2, Vector3 v3, bool startoutSolid, bool endOutsolid)
        {
            // calculate the closest point on box to plane (get extents in that direction)
            Vector3 ptExtent = Vector3.Zero;
            CalcClosestExtents(triNormal, extends, ref ptExtent);

            //
            // expand the plane by the extents of the box to reduce the swept box/triangle
            // test to a ray/extruded triangle test (one of the triangles extruded planes
            // was just calculated above
            //
            float expandDist = triDist - Vector3.Dot(triNormal, ptExtent);
            float distStart = Vector3.Dot(triNormal, start) - expandDist;
            float distEnd = Vector3.Dot(triNormal, end) - expandDist;

            Vector3 boxDir = end - start;
            boxDir.Normalize();

            // resolve the ray/plane collision
            if (!ResolveRayPlaneIntersect(distStart, distEnd, ref  startf, ref endf, boxDir, triNormal, expandDist, startoutSolid, endOutsolid, 0))
                return false;

            return true;

        }

        bool ResolveRayPlaneIntersect(float distStart, float distEnd,ref  float fracStart, ref float fracEnd, Vector3 boxDir, Vector3 normal, float planeDist, bool bStartOutSolid, bool bEndOutSolid, int index)
        {
            if ((distStart > 0.0f) && (distEnd > 0.0f))
                return false;

            //	if( ( distStart <= 0.0f ) && ( distEnd <= 0.0f ) ) { return true; }
            if ((distStart < 0.0f) && (distEnd < 0.0f))
                return true;

            if ((distStart >= 0.0f) && (distEnd <= 0.0f))
            {
                // find t - the parametric distance along the trace line
                float t = (distStart - ClipMap.EPSILON) / (distStart - distEnd);
                if (t > fracStart)
                {
                    fracStart = t;
                }
            }
            else
            //	else if( ( distStart <= 0.0f ) && ( distEnd > 0.0f ) )
            {
                // find t - the parametric distance along the trace line
                float t = (distStart + ClipMap.EPSILON) / (distStart - distEnd);
                if (t < fracEnd)
                {
                    fracEnd = t;
                }
            }

            return true;
        }

        void CalcClosestExtents(Vector3 planeNormal, Vector3 boxExtents, ref Vector3 boxPt)
        {
            boxPt[0] = (planeNormal[0] < 0.0f) ? boxExtents[0] : -boxExtents[0];
            boxPt[1] = (planeNormal[1] < 0.0f) ?  boxExtents[1] : -boxExtents[1];
            boxPt[2] = (planeNormal[2] < 0.0f) ? boxExtents[2] : -boxExtents[2];
        }

        void CalcClosestBoxPoint(Vector3 planeNormal, Vector3 boxStart, Vector3 boxExtents, ref Vector3 boxPt)
        {
            boxPt = boxStart;
            boxPt[0] += (planeNormal[0] < 0.0f) ? boxExtents[0] : -boxExtents[0];
            boxPt[1] += (planeNormal[1] < 0.0f) ? boxExtents[1] : -boxExtents[1];
            boxPt[2] += (planeNormal[2] < 0.0f) ? boxExtents[2] : -boxExtents[2];
        }

        void SweptAABB_BuildTriList(Vector3 start, Vector3 end, Vector3 extends, int nodeid, AABB_t AABBox, TriList_t triList)
        {
            // get the current node
            Node_t node = m_pNodes[nodeid];

            //
            // fill in AABBox plane distances
            //
            AABBox.Dists[0] = -(node.m_BBox[0].X - ClipMap.EPSILON);
            AABBox.Dists[1] = (node.m_BBox[1].X + ClipMap.EPSILON);
            AABBox.Dists[2] = -(node.m_BBox[0].Y - ClipMap.EPSILON);
            AABBox.Dists[3] = (node.m_BBox[1].Y + ClipMap.EPSILON);
            AABBox.Dists[4] = -(node.m_BBox[0].Z - ClipMap.EPSILON);
            AABBox.Dists[5] = (node.m_BBox[1].Z + ClipMap.EPSILON);

            // test the swept box against the given node
            if (SweptAABB_NodeTest(start, end, extends, AABBox))
            {
                //
                // if leaf add tris to list
                //
                if ((node.m_fFlags & 0x01) > 0)
                {
                    if (triList.Count < DISPCOLL_TRILIST_SIZE)
                    {
                        triList.ppTriList[triList.Count] = m_pTris[node.m_iTris[0]];
                        triList.ppTriList[triList.Count + 1] = m_pTris[node.m_iTris[1]];
                        triList.Count += 2;
                    }

                    return;
                }
                // continue testing with children nodes
                else
                {
                    SweptAABB_BuildTriList(start, end, extends, Nodes_GetChild(nodeid, 0), AABBox, triList);
                    SweptAABB_BuildTriList(start, end, extends, Nodes_GetChild(nodeid, 1), AABBox, triList);
                    SweptAABB_BuildTriList(start, end, extends, Nodes_GetChild(nodeid, 2), AABBox, triList);
                    SweptAABB_BuildTriList(start, end, extends, Nodes_GetChild(nodeid, 3), AABBox, triList);
                }
            }
        }

        bool SweptAABB_NodeTest(Vector3 start, Vector3 end, Vector3 extends, AABB_t AABBox)
        {
            //
            // create and initialize the enter and exit fractions
            //
            float enterFraction = 0.0f;
            float exitFraction = 1.0f;

            //
            // test the ray against the AABB (reduced to 1d tests)
            //
            float distStart, distEnd, fraction;

            for (int ndxAxis = 0; ndxAxis < 3; ndxAxis++)
            {
                //
                // test negative axial direction
                //
                distStart = -start[ndxAxis] - (AABBox.Dists[(ndxAxis << 1)] + extends[ndxAxis] /*+ DIST_EPSILON*/ );
                distEnd = -end[ndxAxis] - (AABBox.Dists[(ndxAxis << 1)] + extends[ndxAxis] /*+ DIST_EPSILON*/ );

                if ((distStart > 0.0f) && (distEnd < 0.0f))
                {
                    fraction = (distStart - ClipMap.EPSILON) / (distStart - distEnd);
                    //			fraction = distStart * scalar[ndxAxis];
                    if (fraction > enterFraction)
                    {
                        enterFraction = fraction;
                    }
                }
                else if ((distStart < 0.0f) && (distEnd > 0.0f))
                {
                    fraction = (distStart + ClipMap.EPSILON) / (distStart - distEnd);
                    //			fraction = distStart * scalar[ndxAxis];
                    if (fraction < exitFraction)
                    {
                        exitFraction = fraction;
                    }
                }
                else if ((distStart > 0.0f) && (distEnd > 0.0f))
                {
                    return false;
                }

                //
                // test positive axial direction
                //
                distStart = start[ndxAxis] - (AABBox.Dists[(ndxAxis << 1) + 1] + extends[ndxAxis] /*+ DIST_EPSILON*/ );
                distEnd = end[ndxAxis] - (AABBox.Dists[(ndxAxis << 1) + 1] + extends[ndxAxis] /*+ DIST_EPSILON*/ );

                if ((distStart > 0.0f) && (distEnd < 0.0f))
                {
                    fraction = (distStart - ClipMap.EPSILON) / (distStart - distEnd);
                    //			fraction = distStart * scalar[ndxAxis];
                    if (fraction > enterFraction)
                    {
                        enterFraction = fraction;
                    }
                }
                else if ((distStart < 0.0f) && (distEnd > 0.0f))
                {
                    fraction = (distStart + ClipMap.EPSILON) / (distStart - distEnd);
                    //			fraction = distStart * scalar[ndxAxis];
                    if (fraction < exitFraction)
                    {
                        exitFraction = fraction;
                    }
                }
                else if ((distStart > 0.0f) && (distEnd > 0.0f))
                {
                    return false;
                }
            }

            // test results
            if (exitFraction < enterFraction)
                return false;

            return true;
        
        }

        //-----------------------------------------------------------------------------
        // Purpose: allocate and initialize the displacement collision tree data
        //   Input: pDisp - displacement surface data
        //  Output: success? (true/false)
        //-----------------------------------------------------------------------------
        public bool Create(Displacement pDisp)
        {
            // Displacement size.
            Power = pDisp.Power;

            // Displacement contents.
            DispSurface pSurf = pDisp.Surface;
            m_Contents = pSurf.m_Contents;

            // Displacement stab direction = base face normal.
            m_StabDir = pSurf.GetNormal();

            // Copy the base surface points.
            for (int iPt = 0; iPt < 4; iPt++)
            {
                m_SurfPoints[iPt] = pSurf.m_Points[iPt];
            }

            // Allocate collision tree data.
            bool ResultNodes = Nodes_Alloc();
            bool ResultVert = AllocVertData();
            bool ResultTris = Tris_Alloc();
            if (!ResultTris || !ResultVert || !ResultNodes)
            {
                // Clean up
                m_pNodes = null;
                m_NodeCount = 0;
                m_pVerts = null;
                m_pOriginalVerts = null;
                m_pVertNormals = null;
                m_VertCount = 0;
                m_pTris = null;
                m_nTriCount = 0;
                return false;
            }

            // Copy the vertices and vertex normals.
            for (int iVert = 0; iVert < m_VertCount; iVert++)
            {
                m_pVerts[iVert] = pDisp.Verts[iVert].m_Vert;
                m_pOriginalVerts[iVert] = m_pVerts[iVert];
                m_pVertNormals[iVert] = pDisp.Verts[iVert].m_Normal;
            }

            // Copy the triangle flags data.
            for (int iTri = 0; iTri < m_nTriCount; ++iTri )
            {
                ushort[] ind = pDisp.Tris[iTri].m_iIndex;
                m_pTris[iTri].m_uiVerts = ind;
                m_pTris[iTri].m_nFlags = pDisp.Tris[iTri].m_uiTags;

                // Calculate the surface props.
                float totalAlpha = 0.0f;
                for (int iVert = 0; iVert < 3; ++iVert)
                {
                    totalAlpha += pDisp.Verts[m_pTris[iTri].m_uiVerts[iVert]].m_Alpha;
                }
                m_pTris[iTri].m_iSurfProp = 0;
                if (totalAlpha > 382.5f)
                {
                    m_pTris[iTri].m_iSurfProp = 1;
                }

                // Add the displacement surface flag!
                m_pTris[iTri].m_nFlags |= (1 << 0);

                Tri_CalcPlane(iTri);
            }

            // create leaf nodes
            Leafs_Create();

            // create tree nodes
            Nodes_Create();

            // create the bounding box of the displacement surface + the base face
            CalcFullBBox();

            // tree successfully created!
            return true;
        }

        void CalcFullBBox()
        {
            //
            // initialize the full bounding box with the displacement bounding box
            //
            m_BBoxWithFace[0] = m_pNodes[0].m_BBox[0];
            m_BBoxWithFace[1] = m_pNodes[0].m_BBox[1];

            //
            // expand to contain the base face if necessary
            //
            for (int ndxPt = 0; ndxPt < 4; ndxPt++)
            {
                Vector3 v = m_SurfPoints[ndxPt];

                m_BBoxWithFace[0] = Vector3.Minimize(m_BBoxWithFace[0], v);
                m_BBoxWithFace[1] = Vector3.Maximize(m_BBoxWithFace[1], v);
            }
        }

        //-----------------------------------------------------------------------------
        // Purpose: calculate the number of tree nodes given the size of the 
        //          displacement surface
        //   Input: power - size of the displacement surface
        //  Output: int - the number of tree nodes
        //-----------------------------------------------------------------------------
        int Nodes_CalcCount( int power )
        {
	        return ( ( 1 << ( ( power + 1 ) << 1 ) ) / 3 );
        }

        bool Nodes_Alloc()
        {
            m_NodeCount = (short)Nodes_CalcCount(Power);
            if (m_NodeCount == 0)
                return false;

            m_pNodes = new Node_t[m_NodeCount];
            if (m_pNodes.Length == 0)
            {
                m_NodeCount = 0;
                return false;
            }

            //
            // initialize the nodes
            //
            for (int i = 0; i < m_NodeCount; i++)
            {
                m_pNodes[i] = new Node_t();
                Node_Init(m_pNodes[i]);
            }

            // tree successfully allocated!
            return true;
        }

        void Node_Init(Node_t node)
        {
            node.m_BBox[0] = new Vector3(99999.0f, 99999.0f, 99999.0f);
            node.m_BBox[1] = new Vector3(-99999.0f, -99999.0f, -99999.0f);

            node.m_iTris[0] = -1;
            node.m_iTris[1] = -1;

            node.m_fFlags = 0;
        }

        int Nodes_GetChild(int ndxNode, int direction)
        {
            // ( node index * 4 ) + ( direction + 1 )
            return ((ndxNode << 2) + (direction + 1));
        }

        int Nodes_GetIndexFromComponents( int x, int y )
        {
	        int index = 0;

	        //
	        // interleave bits from the x and y values to create the index
	        //
	        int shift;
	        for( shift = 0; x != 0; shift += 2, x >>= 1 )
	        {
		        index |= ( x & 1 ) << shift;
	        }

	        for( shift = 1; y != 0; shift += 2, y >>= 1 )
	        {
		        index |= ( y & 1 ) << shift;
	        }

	        return index;
        }

        void Nodes_Create()
        {
            //
            // create all nodes in tree
            //
            int power = Power + 1;
            for (int level = power; level > 0; level--)
            {
                Nodes_CreateRecur(0, level);
            }
        }

        int Nodes_GetLevel( int ndxNode )
        {
	        // level = 2^n + 1
	        if( ndxNode == 0 )  { return 1; }
	        if( ndxNode < 5 )   { return 2; }
	        if( ndxNode < 21 )  { return 3; }
	        if( ndxNode < 85 )  { return 4; }
	        if( ndxNode < 341 ) { return 5; }

	        return -1;
        }

        void Nodes_CreateRecur(int ndxNode, int termLevel)
        {
            int nodeLevel = Nodes_GetLevel(ndxNode);

            //
            // terminating condition -- set node info (leaf or otherwise)
            //
            Node_t pNode = m_pNodes[ndxNode];
            if (nodeLevel == termLevel)
            {
                Nodes_CalcBounds(pNode, ndxNode);
                return;
            }

            //
            // recurse into children
            //
            pNode.m_BBox[0] = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            pNode.m_BBox[1] = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int ndxChildren = 0; ndxChildren < 4; ndxChildren++)
            {
                int iChildNode = Nodes_GetChild(ndxNode, ndxChildren);
                Nodes_CreateRecur(iChildNode, termLevel);

                Node_t childNode = m_pNodes[iChildNode];
                pNode.m_BBox[0] = Vector3.Minimize(childNode.m_BBox[0], pNode.m_BBox[0]);
                pNode.m_BBox[1] = Vector3.Maximize(childNode.m_BBox[1], pNode.m_BBox[1]);
            }

        }

        bool Node_IsLeaf(Node_t node)
        {
            return (node.m_fFlags & 0x1) == 0x1;
        }

        void Nodes_CalcBounds(Node_t pNode, int ndxNode)
        {
            //
            // leaf nodes have special cases (caps, etc.)
            //
            if (Node_IsLeaf(pNode))
            {
                Nodes_CalcLeafBounds(pNode, ndxNode);
                return;
            }

            //
            // get the maximum and minimum bounds of all leaf nodes -- that is the
            // bounding box for this node
            //
            pNode.m_BBox[0] = new Vector3(99999.0f, 99999.0f, 99999.0f);
            pNode.m_BBox[1] = new Vector3(-99999.0f, -99999.0f, -99999.0f);

            for (int ndxChildren = 0; ndxChildren < 4; ndxChildren++)
            {
                //
                // get the current child node
                //
                int ndxChildNode = Nodes_GetChild(ndxNode, ndxChildren);
                Node_t childNode = m_pNodes[ndxChildNode];

                // update the bounds
                pNode.m_BBox[0] = Vector3.Minimize(pNode.m_BBox[0], childNode.m_BBox[0]);
                pNode.m_BBox[1] = Vector3.Maximize(pNode.m_BBox[1], childNode.m_BBox[1]);

            }
        }

        void Nodes_CalcLeafBounds(Node_t pNode, int ndxNode)
        {
            // Find the minimum and maximum component values for all triangles in 
            // the leaf node (including caps tris)
            for (int iTri = 0; iTri < 2; iTri++)
            {
                for (int iVert = 0; iVert < 3; iVert++)
                {
                    // Minimum checks
                    Tri_t pTri = m_pTris[pNode.m_iTris[iTri]];
                    Vector3 vecTemp = m_pVerts[pTri.m_uiVerts[iVert]];

                    pNode.m_BBox[0] = Vector3.Minimize(pNode.m_BBox[0], vecTemp);
                    pNode.m_BBox[1] = Vector3.Maximize(pNode.m_BBox[1], vecTemp);
                }
            }
        }

        void Leafs_Create()
        {
            // Find the bottom leftmost node.
            int iMinNode = 0;
            for (int iPower = 0; iPower < Power; ++iPower)
            {
                iMinNode = Nodes_GetChild(iMinNode, 0);
            }

            int nWidth = ((1 << Power) + 1) -1;
            int nHeight = nWidth;

            for (int iHgt = 0; iHgt < nHeight; ++iHgt)
            {
                for (int iWid = 0; iWid < nWidth; ++iWid)
                {
                    int iNode = Nodes_GetIndexFromComponents(iWid, iHgt);
                    iNode += iMinNode;
                    Node_t pNode = m_pNodes[iNode];
                    if (pNode == null)
                        continue;

                    Node_SetLeaf(pNode, true);

                    int nIndex = iHgt * nWidth + iWid;
                    int iTri = nIndex * 2;

                    pNode.m_iTris[0] = (short)iTri;
                    pNode.m_iTris[1] = (short)(iTri + 1);
                }
            }
        }

        void Node_SetLeaf(Node_t node, bool leaf)
        {
            if (leaf)
                node.m_fFlags |= 0x1;
            else
                node.m_fFlags &= ~0x1;
        }

        void Tri_CalcPlane(int iTri)
        {
            Tri_t pTri = m_pTris[iTri];
            if (pTri == null)
                return;

            Vector3[] vecTmp = new Vector3[3];
            Vector3 vecEdge1, vecEdge2;
            vecTmp[0] = m_pVerts[pTri.m_uiVerts[0]];
            vecTmp[1] = m_pVerts[pTri.m_uiVerts[1]];
            vecTmp[2] = m_pVerts[pTri.m_uiVerts[2]];
            vecEdge1 = vecTmp[1] - vecTmp[0];
            vecEdge2 = vecTmp[2] - vecTmp[0];

            pTri.m_vecNormal = Vector3.Cross(vecEdge2, vecEdge1);
            pTri.m_vecNormal.Normalize();
            pTri.m_flDist = Vector3.Dot(pTri.m_vecNormal, vecTmp[0]);
        }

        bool Tris_Alloc()
        {
            // Calculate the number of triangles.
            m_nTriCount = (ushort)((1 << (Power)) * (1 << (Power)) * 2);

            // Allocate triangle memory.
            m_pTris = new Tri_t[m_nTriCount];
            if (m_pTris.Length == 0)
            {
                m_nTriCount = 0;
                return false;
            }

            // Initialize the triangles.
            Tris_Init();

            return true;
        }

        void Tris_Init()
        {
            for (int iTri = 0; iTri < m_nTriCount; ++iTri)
            {
                m_pTris[iTri] = new Tri_t();
                m_pTris[iTri].m_vecNormal = Vector3.Zero;
                m_pTris[iTri].m_flDist = 0.0f;

                m_pTris[iTri].m_nFlags = 0;

                // Triangle vertex data.
                for (int iVert = 0; iVert < 3; ++iVert)
                {
                    m_pTris[iTri].m_uiVerts[iVert] = 0;
                }
            }
        }

        bool AllocVertData()
        {
            m_VertCount = (short)(((1 << Power) + 1) * ((1 << Power) + 1));
            if (m_VertCount == 0)
                return false;

            m_pVerts = new Vector3[m_VertCount];
            m_pOriginalVerts = new Vector3[m_VertCount];
            if (m_pVerts.Length == 0 || m_pOriginalVerts.Length == 0)
            {
                m_VertCount = 0;
                return false;
            }

            m_pVertNormals = new Vector3[m_VertCount];
            if (m_pVertNormals.Length == 0)
            {
                m_VertCount = 0;
                m_pVerts = null;
                return false;
            }

            return true;
        }
    }

    public class CoreDispVert
    {
        public Vector3			m_FieldVector;						// displacement vector field
	    public float			m_FieldDistance;					// the distances along the displacement vector normal

	    public Vector3			m_SubdivNormal;
	    public Vector3			m_SubdivPos;						// used the create curvature of displacements

	    // generated displacement surface data
	    public Vector3			m_Vert;								// displacement surface vertices
	    public Vector3			m_FlatVert;
	    public Vector3			m_Normal;							// displacement surface normals
	    public Vector3			m_TangentS;							// use in calculating the tangent space axes
	    public Vector3			m_TangentT;							// use in calculating the tangent space axes
	    public Vector2		m_TexCoord;							// displacement surface texture coordinates
	    public Vector2[]		m_LuxelCoords = new Vector2[4];	// displacement surface lightmap coordinates

	    // additional per-vertex data
	    public float			m_Alpha;							// displacement alpha values (per displacement vertex)
    }

    public class CoreDispNode
    {
        public Vector3[]    m_BBox = new Vector3[2];											// displacement node bounding box (take into account size of children)
        public float        m_ErrorTerm;										// LOD error term (the "precision" of the representation of the surface at this node's level)
        public int          m_VertIndex;										// the node's vertex index (center vertex of node)
        public int[]        m_NeighborVertIndices = new int[8];		// all other vertex indices in node (maximally creates 8 trianglar surfaces)
        public Vector3[,]   m_SurfBBoxes = new Vector3[8, 2];			// surface bounding boxes - old method
        public cplane_t[]   m_SurfPlanes = new cplane_t[8];				// surface plane info - old method
        public Vector3[,]   m_RayBBoxes = new Vector3[4, 2];									// bounding boxes for ray traces

        public CoreDispNode()
        {
            m_VertIndex = -1;
            for (int i = 0; i < 4; i++)
            {
                m_NeighborVertIndices[i] = -1;
            }
        }
    }

    public class CoreDispTri
    {
	    public ushort[]  m_iIndex = new ushort[3];						// the three indices that make up a triangle 
	    public ushort	m_uiTags;							// walkable, buildable, etc.
    }

    public class DispLeafLink
    {
        public object m_pDispInfo;
        public object m_pLeaf;
        
		const int LIST_LEAF=0;		// the list that's chained into leaves.
		const int LIST_DISP=1;		// the list that's chained into displacements

        public DispLeafLink[] Prev = new DispLeafLink[2];
        public DispLeafLink[] Next = new DispLeafLink[2];

        public void Add(object pDispInfo, ref DispLeafLink pDispHead, object pLeaf, ref DispLeafLink pLeafHead)
        {
            // Store off pointers.
            m_pDispInfo = pDispInfo;
            m_pLeaf = pLeaf;

            // Link into the displacement's list of leaves.
            if (pDispHead != null)
            {
                Prev[LIST_DISP] = pDispHead;
                Next[LIST_DISP] = pDispHead.Next[LIST_DISP];
            }
            else
            {
                pDispHead = Prev[LIST_DISP] = Next[LIST_DISP] = this;
            }
            Prev[LIST_DISP].Next[LIST_DISP] = Next[LIST_DISP].Prev[LIST_DISP] = this;

            // Link into the leaf's list of displacements.
            if (pLeafHead != null)
            {
                Prev[LIST_LEAF] = pLeafHead;
                Next[LIST_LEAF] = pLeafHead.Next[LIST_LEAF];
            }
            else
            {
                pLeafHead = Prev[LIST_LEAF] = Next[LIST_LEAF] = this;
            }
            Prev[LIST_LEAF].Next[LIST_LEAF] = Next[LIST_LEAF].Prev[LIST_LEAF] = this;

        }

        public void Remove(ref DispLeafLink pDispHead, ref DispLeafLink pLeafHead)
        {
            // Remove from the displacement.
            Prev[LIST_DISP].Next[LIST_DISP] = Next[LIST_DISP];
            Next[LIST_DISP].Prev[LIST_DISP] = Prev[LIST_DISP];

            if (this == pDispHead)
            {
                if (Next[LIST_DISP] == this)
                    pDispHead = null;
                else
                    pDispHead = Next[LIST_DISP];
            }

            // Remove from the leaf.
            Prev[LIST_LEAF].Next[LIST_LEAF] = Next[LIST_LEAF];
            Next[LIST_LEAF].Prev[LIST_LEAF] = Prev[LIST_LEAF];
            if (this == pLeafHead)
            {
                if (Next[LIST_LEAF] == this)
                    pLeafHead = null;
                else
                    pLeafHead = Next[LIST_LEAF];
            }
        }
    }
}
