using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client;
using SlimDX;
using System.Diagnostics;
using CubeHags.client.map.Source;

namespace CubeHags.common
{
    public sealed partial class ClipMap
    {
        leafList_t ll = new leafList_t();

        public trace_t Box_Trace(trace_t results, Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, int model, int brushmask, int capsule)
        {
            return Trace(results, start, end, mins, maxs, model, Vector3.Zero, brushmask, capsule, null);
        }

        trace_t Trace(trace_t results, Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, int model, Vector3 origin, int brushmask, int capsule, sphere_t sphere)
        {
            cmodel_t cmod = ClipHandleToModel(model);
            // for multi-check avoidance
            checkcount++;
            // for statistics, may be zeroed
            c_traces++;

            traceWork_t tw = new traceWork_t();
            tw.trace.fraction = 1; // assume it goes the entire distance until shown otherwise
            tw.modelOrigin = origin;

            if (nodes == null || nodes.Length == 0)
            {
                results = tw.trace;
                return results; // map not loaded, shouldn't happen
            }

            // allow NULL to be passed in for 0,0,0
            if (mins == null)
                mins = Vector3.Zero;
            if (maxs == null)
                maxs = Vector3.Zero;

            // set basic parms
            tw.contents = brushmask;

            // adjust so that mins and maxs are always symetric, which
            // avoids some complications with plane expanding of rotated
            // bmodels
            Vector3 offset = (mins-maxs) * 0.5f;
            tw.size[0] = mins - offset;
            tw.size[1] = maxs - offset;
            tw.start = start + offset;
            tw.end = end + offset;

            // if a sphere is already specified
            if (sphere != null)
                tw.sphere = sphere;
            else
            {
                tw.sphere.use = capsule==1?true:false;
                tw.sphere.radius = (tw.size[1][0] > tw.size[1][2]) ? tw.size[1][2] : tw.size[1][0];
                tw.sphere.halfheight = tw.size[1][2];
                tw.sphere.offset = new Vector3(0, 0, tw.size[1][2] - tw.sphere.radius);
            }

            tw.maxOffset = tw.size[1][0] + tw.size[1][1] + tw.size[1][2];

            // tw.offsets[signbits] = vector to apropriate corner from origin
            tw.offsets[0][0] = tw.size[0][0];
            tw.offsets[0][1] = tw.size[0][1];
            tw.offsets[0][2] = tw.size[0][2];

            tw.offsets[1][0] = tw.size[1][0];
            tw.offsets[1][1] = tw.size[0][1];
            tw.offsets[1][2] = tw.size[0][2];

            tw.offsets[2][0] = tw.size[0][0];
            tw.offsets[2][1] = tw.size[1][1];
            tw.offsets[2][2] = tw.size[0][2];

            tw.offsets[3][0] = tw.size[1][0];
            tw.offsets[3][1] = tw.size[1][1];
            tw.offsets[3][2] = tw.size[0][2];

            tw.offsets[4][0] = tw.size[0][0];
            tw.offsets[4][1] = tw.size[0][1];
            tw.offsets[4][2] = tw.size[1][2];

            tw.offsets[5][0] = tw.size[1][0];
            tw.offsets[5][1] = tw.size[0][1];
            tw.offsets[5][2] = tw.size[1][2];

            tw.offsets[6][0] = tw.size[0][0];
            tw.offsets[6][1] = tw.size[1][1];
            tw.offsets[6][2] = tw.size[1][2];

            tw.offsets[7][0] = tw.size[1][0];
            tw.offsets[7][1] = tw.size[1][1];
            tw.offsets[7][2] = tw.size[1][2];

            //
            // calculate bounds
            //
            if (tw.sphere.use)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (tw.start[i] < tw.end[i])
                    {
                        tw.bounds[0][i] = tw.start[i] - (float)Math.Abs(tw.sphere.offset[i]) - tw.sphere.radius;
                        tw.bounds[1][i] = tw.end[i] + (float)Math.Abs(tw.sphere.offset[i]) + tw.sphere.radius;
                    }
                    else
                    {
                        tw.bounds[0][i] = tw.end[i] - (float)Math.Abs(tw.sphere.offset[i]) - tw.sphere.radius;
                        tw.bounds[1][i] = tw.start[i] + (float)Math.Abs(tw.sphere.offset[i]) + tw.sphere.radius;
                    }
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    if (tw.start[i] < tw.end[i])
                    {
                        tw.bounds[0][i] = tw.start[i] + tw.size[0][i];
                        tw.bounds[1][i] = tw.end[i] + tw.size[1][i];
                    }
                    else
                    {
                        tw.bounds[0][i] = tw.end[i] + tw.size[0][i];
                        tw.bounds[1][i] = tw.start[i] + tw.size[1][i];
                    }
                }
            }

            //
            // check for position test special case
            //
            if (start.Equals(end))
            {
                if (model > 0)
                {
                    int test = 2;
                    //if (model == 254) // CAPSULE_MODEL_HANDLE
                    //{
                    //    if (tw.sphere.use)
                    //    {
                    //        TestCapsuleInCapsule(tw, model);
                    //    }
                    //    else
                    //    {
                    //        TestBoundingBoxInCapsule(tw, model);
                    //    }
                    //}
                    //else
                    //{
                    //    TestInLeaf(tw, cmod.leaf);
                    //}
                }
                else
                {
                    PositionTest(tw);
                }
            }
            else
            {
                //
                // check for point special case
                //
                if (tw.size[0].Equals(Vector3.Zero))
                {
                    tw.isPoint = true;
                    tw.extents = Vector3.Zero;
                }
                else
                {
                    tw.isPoint = false;
                    tw.extents = tw.size[1];
                }

                //
                // general sweeping through world
                //
                if (model > 0)
                {
                    int test = 2;
                    //if (model == 254) // CAPSULE_MODEL_HANDLE
                    //{
                    //    if (tw.sphere.use)
                    //    {
                    //        TraceCapsuleThroughCapsule(tw, model);
                    //    }
                    //    else
                    //    {
                    //        TraceBoundingBoxThroughCapsule(tw, model);
                    //    }
                    //}
                    //else
                    //{
                    //    TraceThroughLeaf(tw, cmod.leaf);
                    //}
                }
                else
                {
                    TraceThroughTree(tw, 0, 0, 1, tw.start, tw.end);
                }
            }

            // generate endpos from the original, unmodified start/end
            if (tw.trace.fraction == 1f)
            {
                tw.trace.endpos = end;
            }
            else
            {
                tw.trace.endpos = start + tw.trace.fraction * (end - start);

                //for (int i = 0; i < 3; i++)
                //{
                //    tw.trace.endpos[i] = start[i] + tw.trace.fraction * (end[i] - start[i]);
                //}
            }

            // If allsolid is set (was entirely inside something solid), the plane is not valid.
            // If fraction == 1.0, we never hit anything, and thus the plane is not valid.
            // Otherwise, the normal on the plane should have unit length
            Debug.Assert(tw.trace.allsolid || tw.trace.fraction == 1f || tw.trace.plane.normal.LengthSquared() > 0.9999f);
            return tw.trace;
        }

        void TraceThroughLeaf(traceWork_t tw, dleaf_t leaf)
        {
            // trace line against all brushes in the leaf
            for (int k = 0; k < leaf.numleafbrushes; k++)
            {
                dbrush_t b = brushes[leafbrushes[leaf.firstleafbrush + k]];
                if (b.checkcount == checkcount)
                    continue;   // already checked this brush in another leaf
                b.checkcount = checkcount;

                if (b.contents == brushflags.CONTENTS_EMPTY || ((int)b.contents & tw.contents) != tw.contents)
                    continue;

                if (!BoundsIntersect(tw.bounds[0], tw.bounds[1], b.boundsmin, b.boundsmax))
                    continue;

                TraceThroughBrush(tw, b);
                if (tw.trace.fraction == 0.0f)
                    return;
            }
        }

        void TraceThroughBrush(traceWork_t tw, dbrush_t brush)
        {
            float enterFrac = -1f;
            float leaveFrac = 1f;
            cplane_t clipplane = null;

            if (brush.numsides <= 0)
                return;

            c_brush_traces++;
            bool getout = false;
            bool startout = false;

            dbrushside_t leadside = null;

            if (tw.sphere.use)
            {
                int test = 2;
            }
            else
            {
                //
                // compare the trace against all planes of the brush
                // find the latest time the trace crosses a plane towards the interior
                // and the earliest time the trace crosses a plane towards the exterior
                //
                for (int i = 0; i < brush.numsides; i++)
                {
                    dbrushside_t side = brush.sides[i];
                    cplane_t plane = side.plane;

                    // adjust the plane distance apropriately for mins/maxs
                    float dist = plane.dist - Vector3.Dot(tw.offsets[plane.signbits], plane.normal);

                    float d1 = Vector3.Dot(tw.start, plane.normal) - dist;
                    float d2 = Vector3.Dot(tw.end, plane.normal) - dist;

                    if (d2 > 0f)
                        getout = true;  // endpoint is not in solid
                    if (d1 > 0f)
                        startout = true;

                    // if completely in front of face, no intersection with the entire brush
                    if (d1 > 0f && (d2 >= 0.125f || d2 >= d1))
                        return;

                    // if it doesn't cross the plane, the plane isn't relevent
                    if (d1 <= 0 && d2 <= 0)
                    {
                        continue;
                    }

                    // crosses face
                    if (d1 > d2)    // enter
                    {
                        float f = (d1 - 0.125f) / (d1 - d2);
                        if (f < 0)
                            f = 0;
                        if (f > enterFrac)
                        {
                            enterFrac = f;
                            clipplane = plane;
                            leadside = side;
                        }
                    }
                    else // leave
                    {
                        float f = (d1 + 0.125f) / (d1 - d2);
                        if (f > 1)
                            f = 1;
                        if (f < leaveFrac)
                        {
                            leaveFrac = f;
                        }
                    }
                }
            }

            //
            // all planes have been checked, and the trace was not
            // completely outside the brush
            //
            if (!startout)
            {
                // original point was inside brush
                tw.trace.startsolid = true;
                if (!getout)
                {
                    tw.trace.allsolid = true;
                    tw.trace.fraction = 0f;
                    tw.trace.contents = (int)brush.contents;
                }
                return;
            }

            if (enterFrac < leaveFrac)
            {
                if (enterFrac > -1 && enterFrac < tw.trace.fraction)
                {
                    if (enterFrac < 0)
                        enterFrac = 0;
                    tw.trace.fraction = enterFrac;
                    tw.trace.plane = clipplane;
                    tw.trace.surfaceFlags = leadside.bevel;
                    tw.trace.contents = (int)brush.contents;

                }
            }
        }

        bool BoundsIntersect(Vector3 mins, Vector3 maxs, Vector3 mins2, Vector3 maxs2)
        {
            float SURFACE_CLIP_EPSILON = 0.125f;
            if (maxs[0] < mins2[0] - SURFACE_CLIP_EPSILON ||
                maxs[1] < mins2[1] - SURFACE_CLIP_EPSILON ||
                maxs[2] < mins2[2] - SURFACE_CLIP_EPSILON ||
                mins[0] > maxs2[0] + SURFACE_CLIP_EPSILON ||
                mins[1] > maxs2[1] + SURFACE_CLIP_EPSILON ||
                mins[2] > maxs2[2] + SURFACE_CLIP_EPSILON)
            {
                return false;
            }

            return true;
        }

        /*
        ==================
        CM_TraceThroughTree

        Traverse all the contacted leafs from the start to the end position.
        If the trace is a point, they will be exactly in order, but for larger
        trace volumes it is possible to hit something in a later leaf with
        a smaller intercept fraction.
        ==================
        */
        void TraceThroughTree(traceWork_t tw, int num, float p1f, float p2f, Vector3 p1, Vector3 p2)
        {
            if (tw.trace.fraction <= p1f)
                return; // already hit something nearer

            // if < 0, we are in a leaf node
            if (num < 0)
            {
                TraceThroughLeaf(tw, leafs[-1 - num]);
                return;
            }

            //
            // find the point distances to the seperating plane
            // and the offset for the size of the box
            //
            dnode_t node = nodes[num];
            cplane_t plane = node.plane;

            // adjust the plane distance apropriately for mins/maxs
            float t1, t2, offset;
            if (plane.type < 3)
            {
                t1 = p1[plane.type] - plane.dist;
                t2 = p2[plane.type] - plane.dist;
                offset = tw.extents[plane.type];
            }
            else
            {
                t1 = Vector3.Dot(plane.normal, p1) - plane.dist;
                t2 = Vector3.Dot(plane.normal, p2) - plane.dist;
                if (tw.isPoint)
                    offset = 0;
                else
                {
                    // this is silly
                    offset = 2048;
                }
            }

            // see which sides we need to consider
            if (t1 >= offset + 1 && t1 >= offset + 1)
            {
                TraceThroughTree(tw, node.children[0], p1f, p2f, p1, p2);
                return;
            }
            if (t1 < -offset - 1 && t2 < -offset - 1)
            {
                TraceThroughTree(tw, node.children[1], p1f, p2f, p1, p2);
                return;
            }

            // put the crosspoint SURFACE_CLIP_EPSILON pixels on the near side
            float idist, frac, frac2;
            int side;
            if (t1 < t2)
            {
                idist = 1.0f / (t1 - t2);
                side = 1;
                frac2 = (t1 + offset + 0.125f) * idist;
                frac = (t1 - offset + 0.125f) * idist;
            }
            else if (t1 > t2)
            {
                idist = 1.0f / (t1 - t2);
                side = 0;
                frac2 = (t1 - offset - 0.125f) * idist;
                frac = (t1 + offset + 0.125f) * idist;
            }
            else
            {
                side = 0;
                frac = 1;
                frac2 = 0;
            }

            // move up to the node
            if (frac < 0f)
                frac = 0;
            if (frac > 1f)
                frac = 1;

            float midf = p1f + (p2f - p1f) * frac;
            Vector3 mid = Vector3.Zero;
            mid[0] = p1[0] + frac * (p2[0] - p1[0]);
            mid[1] = p1[1] + frac * (p2[1] - p1[1]);
            mid[2] = p1[2] + frac * (p2[2] - p1[2]);

            TraceThroughTree(tw, node.children[side], p1f, midf, p1, mid);

            // go past the node
            if (frac2 < 0f)
                frac2 = 0;
            if (frac2 > 1f)
                frac2 = 1f;

            midf = p1f + (p2f - p1f) * frac2;

            mid[0] = p1[0] + frac2 * (p2[0] - p1[0]);
            mid[1] = p1[1] + frac2 * (p2[1] - p1[1]);
            mid[2] = p1[2] + frac2 * (p2[2] - p1[2]);

            TraceThroughTree(tw, node.children[side ^ 1], midf, p2f, mid, p2);
        }

        void PositionTest(traceWork_t tw)
        {
            int[] lleafs = new int[1024];
            // identify the leafs we are touching
            
            ll.bounds[0] = tw.start - tw.size[0];
            ll.bounds[1] = tw.end - tw.size[1];

            for (int i = 0; i < 3; i++)
            {
                ll.bounds[0][i] -= 1;
                ll.bounds[1][i] += 1;
            }

            ll.count = 0;
            ll.maxcount = 1024;
            ll.list = lleafs;
            //ll.StoreLeaf += new StoreLeafDelegate(StoreLeafs);
            ll.lastLeaf = 0;
            ll.overflowed = false;

            checkcount++;

            BoxLeafnums(ll, 0);

            checkcount++;

            // test the contents of the leafs
            for (int i = 0; i < ll.count; i++)
            {
                TestInLeaf(tw, leafs[lleafs[i]]);
                if (tw.trace.allsolid)
                    break;
            }

        }

        void BoxLeafnums(leafList_t ll, int nodenum)
        {
            cplane_t plane;
            dnode_t node;
            int s;
            while (true)
            {
                if (nodenum < 0)
                {
                    StoreLeafs(ll, nodenum);
                    //ll.RunStoreLeaf(ll, nodenum);
                    return;
                }

                node = nodes[nodenum];
                plane = node.plane;

                s = Common.Instance.BoxOnPlaneSide(ref ll.bounds[0], ref ll.bounds[1], plane);
                if (s == 1)
                    nodenum = node.children[0];
                else if (s == 2)
                    nodenum = node.children[1];
                else
                {
                    // go down both
                    BoxLeafnums(ll, node.children[0]);
                    nodenum = node.children[1];
                }
            }
        }

        

        void StoreLeafs(leafList_t ll, int nodenum)
        {
            int leafnum = -1 - nodenum;

            // store the lastLeaf even if the list is overflowed
            if (leafs[leafnum].cluster != -1)
                ll.lastLeaf = leafnum;

            if (ll.count >= ll.maxcount)
            {
                ll.overflowed = true;
                return;
            }

            ll.list[ll.count++] = leafnum;
        }

        cmodel_t ClipHandleToModel(int handle)
        {
            if (handle < 0)
                Common.Instance.Error("ClipHandleToModel: bad handle " + handle);

            //if(handle < numSubModels)
            return new cmodel_t();
        }

        void TestInLeaf(traceWork_t tw, dleaf_t leaf)
        {
            int brushnum;
            dbrush_t b;
            // test box position against all brushes in the leaf
            for (int k = 0; k < leaf.numleafbrushes; k++)
            {
                brushnum = leafbrushes[leaf.firstleafbrush + k];
                b = brushes[brushnum];
                if (b.checkcount == checkcount)
                    continue;   // already checked this brush in another leaf
                b.checkcount = checkcount;

                if (((int)b.contents & tw.contents) != tw.contents)
                    continue;

                TestBoxInBrush(tw, b);
                if (tw.trace.allsolid)
                    return;
            }

        }

        void TestBoxInBrush(traceWork_t tw, dbrush_t brush)
        {
            if (brush.numsides <= 0)
                return;
            
            
            // special test for axial
            if (tw.bounds[0][0] > brush.boundsmax[0]
                || tw.bounds[0][1] > brush.boundsmax[1]
                || tw.bounds[0][2] > brush.boundsmax[2]
                || tw.bounds[1][0] < brush.boundsmin[0]
                || tw.bounds[1][1] < brush.boundsmin[1]
                || tw.bounds[1][2] < brush.boundsmin[2])
                return;

            Vector3 startp;
            if (tw.sphere.use)
            {
                // the first six planes are the axial planes, so we only
                // need to test the remainder
                for (int i = 0; i < 6; i++)
                {
                    dbrushside_t side = brush.sides[i];
                    cplane_t plane = side.plane;

                    // adjust the plane distance apropriately for radius
                    float dist = plane.dist + tw.sphere.radius;

                    // find the closest point on the capsule to the plane
                    float t = Vector3.Dot(plane.normal, tw.sphere.offset);
                    if (t > 0f)
                        startp = tw.start - tw.sphere.offset;
                    else
                        startp = tw.start + tw.sphere.offset;

                    float d1 = Vector3.Dot(startp, plane.normal) - dist;
                    // if completely in front of face, no intersection
                    if (d1 > 0f)
                        return;
                }
            }
            else
            {
                // the first six planes are the axial planes, so we only
                // need to test the remainder
                for (int i = 0; i < 6; i++)
                {
                    dbrushside_t side = brush.sides[i];
                    cplane_t plane = side.plane;

                    // adjust the plane distance apropriately for mins/maxs
                    float dist = plane.dist - Vector3.Dot(tw.offsets[plane.signbits], plane.normal);
                    float d1 = Vector3.Dot(tw.start, plane.normal) - dist;

                    // if completely in front of face, no intersection
                    if (d1 > 0f)
                        return;
                }
            }

            // inside this brush
            tw.trace.startsolid = tw.trace.allsolid = true;
            tw.trace.fraction = 0f;
            tw.trace.contents = (int)brush.contents;
        }

        public class leafList_t 
        {
            public int count;
            public int maxcount;
            public bool overflowed;
            public int[] list;
            public Vector3[] bounds = new Vector3[2];
            public int lastLeaf;		// for overflows where each leaf can't be stored individually
        	public event StoreLeafDelegate StoreLeaf;
            public void RunStoreLeaf(leafList_t ll, int nodenum) { StoreLeaf(ll, nodenum); }
        } 

        public delegate void StoreLeafDelegate(leafList_t ll, int nodenum);

        public class traceWork_t {
        	public Vector3		start;
        	public Vector3		end;
        	public Vector3[]		size = new Vector3[2];	// size of the box being swept through the model
        	public Vector3[]		offsets = new Vector3[8];	// [signbits][x] = either size[0][x] or size[1][x]
        	public float		maxOffset;	// longest corner length from origin
        	public Vector3		extents;	// greatest of abs(size[0]) and abs(size[1])
            public Vector3[] bounds = new Vector3[2];	// enclosing box of start and end surrounding by size
        	public Vector3		modelOrigin;// origin of the model tracing through
        	public int			contents;	// ored contents of the model tracing through
        	public bool	isPoint;	// optimized case
        	public trace_t		trace = new trace_t();		// returned from trace call
        	public sphere_t	sphere = new sphere_t();		// sphere for oriendted capsule collision
        }

        // Used for oriented capsule collision detection
        public class sphere_t
        {
        	public bool	        use;
        	public float		radius;
        	public float		halfheight;
        	public Vector3		offset;
        }
    }
}
