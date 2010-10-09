using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client;
using SlimDX;
using System.Diagnostics;
using CubeHags.client.map.Source;

namespace CubeHags.common
{
    public class MoveClip
    {
        public Vector3 boxmins, boxmaxs;// enclose the test object along entire move
        public Vector3 mins;
        public Vector3 maxs;	// size of the moving object
        public Vector3 start;
        public Vector3 end;
        public trace_t trace;
        public int passEntityNum;
        public int contentmask;
        public int capsule;
    }

    public sealed partial class ClipMap
    {

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

        public class traceWork_t
        {
            public Vector3 start;
            public Vector3 end;
            public Vector3[] size = new Vector3[2];	// size of the box being swept through the model
            public Vector3[] offsets = new Vector3[8];	// [signbits][x] = either size[0][x] or size[1][x]
            public float maxOffset;	// longest corner length from origin
            public Vector3 extents;	// greatest of abs(size[0]) and abs(size[1])
            public Vector3[] bounds = new Vector3[2];	// enclosing box of start and end surrounding by size
            public Vector3 modelOrigin;// origin of the model tracing through
            public int contents;	// ored contents of the model tracing through
            public bool isPoint;	// optimized case
            public trace_t trace = new trace_t();		// returned from trace call
            public sphere_t sphere = new sphere_t();		// sphere for oriendted capsule collision
        }

        // Used for oriented capsule collision detection
        public class sphere_t
        {
            public bool use;
            public float radius;
            public float halfheight;
            public Vector3 offset;
        }

        public const float EPSILON = 0.03125f;

        leafList_t ll = new leafList_t();
        traceWork_t tw = new traceWork_t();
        int[] lleafs = new int[1024];

        public int GetPointContents(Vector3 point)
        {
            int contents = ClipMap.Instance.GetPointContents(point);
            if ((contents & (int)brushflags.MASK_CURRENT) > 0)
            {
                contents = (int)brushflags.CONTENTS_WATER;
            }

            if ((contents & (int)brushflags.CONTENTS_SOLID) == 0)
            {
                // go over entities
            }

            return contents;
        }

        public trace_t Box_Trace( Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, int model, int brushmask)
        {
            return Trace(start, end, mins, maxs, model, Vector3.Zero, brushmask);
        }

        public trace_t TransformedBoxTrace(Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, int model, int brushmask, Vector3 origin, Vector3 angles)
        {
            // adjust so that mins and maxs are always symetric, which
            // avoids some complications with plane expanding of rotated
            // bmodels
            Vector3 offset = (mins + maxs) * 0.5f;
            Vector3[] symetricSize = new Vector3[2];
            symetricSize[0] = mins - offset;
            symetricSize[1] = maxs - offset;
            Vector3 startl = start + offset;
            Vector3 endl = end + offset;

            // subtract origin offset
            startl = startl - origin;
            endl = endl - origin;

            // rotate start and end into the models frame of reference
            bool rotated = false;
            if (model != 255 && (angles[0] != 0f || angles[1] != 0f || angles[2] != 0f))
                rotated = true;

            trace_t trace = Trace(startl, endl, symetricSize[0], symetricSize[1], model, origin, brushmask);
            if (rotated && trace.fraction != 1.0f)
            {

            }

            trace.endpos = start + trace.fraction * (end - start);
            return trace;
        }

        trace_t Trace(Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, int model, Vector3 origin, int brushmask)
        {
            //cmodel_t cmod = ClipHandleToModel(model);
            // for multi-check avoidance
            checkcount++;
            // for statistics, may be zeroed
            c_traces++;
            this.tw = new traceWork_t();
            traceWork_t tw = this.tw;
            tw.trace = new trace_t();
            tw.trace.fraction = 1f; // assume it goes the entire distance until shown otherwise
            tw.modelOrigin = origin;

            if (nodes == null || nodes.Length == 0)
            {
                return tw.trace; // map not loaded, shouldn't happen
            }

            // set basic parms
            tw.contents = brushmask;

            // adjust so that mins and maxs are always symetric, which
            // avoids some complications with plane expanding of rotated
            // bmodels
            Vector3 offset = (mins+maxs) * 0.5f;
            tw.size[0] = mins - offset;
            tw.size[1] = maxs - offset;
            tw.start = start + offset;
            tw.end = end + offset;

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
            

            //
            // check for position test special case
            //
            if (start.Equals(end))
            {
                if (model > 0)
                {
                    int test = 2;
                    //TestInLeaf(tw, cmod.leaf);
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
                    //TraceThroughLeaf(tw, cmod.leaf);
                }
                else
                {
                    RecursiveHullCheck(tw, 0, 0, 1, tw.start, tw.end);
                    //TraceThroughTree(tw, 0, 0, 1, tw.start, tw.end);
                }
            }

            // generate endpos from the original, unmodified start/end
            if (tw.trace.fraction == 1f)
            {
                tw.trace.endpos = end;
            }
            else
            {
                tw.trace.endpos = start + (tw.trace.fraction * (end - start));
            }

            // If allsolid is set (was entirely inside something solid), the plane is not valid.
            // If fraction == 1.0, we never hit anything, and thus the plane is not valid.
            // Otherwise, the normal on the plane should have unit length
            Debug.Assert(tw.trace.allsolid || tw.trace.fraction == 1f || tw.trace.plane.normal.LengthSquared() > 0.9999f);
            return tw.trace;
        }

        void RecursiveHullCheck(traceWork_t tw, int num, float p1f, float p2f, Vector3 p1, Vector3 p2)
        {
            if (tw.trace.fraction <= p1f)
                return; // already hit something nearer

            dnode_t node = null;
            cplane_t plane;
            float t1 = 0f, t2 = 0f, offset = 0f;

            // find the point distances to the seperating plane
            // and the offset for the size of the box

            // NJS: Hoisted loop invariant comparison to trace_ispoint
            if (tw.isPoint)
            {
                while (num >= 0)
                {
                    node = nodes[num];
                    plane = node.plane;

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
                        offset = 0;
                    }

                    // see which sides we need to consider
                    if (t1 > offset && t2 > offset)
                    {
                        num = node.children[0];
                        continue;
                    }
                    if (t1 < -offset && t2 < -offset)
                    {
                        num = node.children[1];
                        continue;
                    }
                    break;

                }
            }
            else
            {
                while (num >= 0)
                {
                    node = nodes[num];
                    plane = node.plane;

                    //Vector3 pl = new Vector3(0.5f, 0.5f, 0);
                    //Vector3 pos = new Vector3(100, 200, -45);
                    //float res = Vector3.Dot(pos,pl);
                    //float sside = res +368;

                    if (plane.type < 3)
                    {
                        t1 = p1[plane.type] - plane.dist;
                        t2 = p2[plane.type] - plane.dist;
                        offset = tw.extents[plane.type];
                    } else {
                        t1 = Vector3.Dot(plane.normal, p1) - plane.dist;
                        t2 = Vector3.Dot(plane.normal, p2) - plane.dist;
                        offset = (float)(Math.Abs(tw.extents[0]*plane.normal[0]) +
                                       Math.Abs(tw.extents[1]*plane.normal[1]) +
                                       Math.Abs(tw.extents[2]*plane.normal[2]));
                    }

                    // see which sides we need to consider
                    if (t1 > offset && t2 > offset)
                    {
                        num = node.children[0];
                        continue;
                    }
                    if (t1 < -offset && t2 < -offset)
                    {
                        num = node.children[1];
                        continue;
                    }
                    break;
                }
            }

            // if < 0, we are in a leaf node
            if (num < 0)
            {
                //TraceToLeaf(tw, -1 - num, p1f, p2f);
                TraceThroughLeaf(tw, -1 - num);
                return;
            }

            // put the crosspoint DIST_EPSILON pixels on the near side
            float idist, frac, frac2;
            int side;
            if (t1 < t2)
            {
                idist = 1.0f / (t1 - t2);
                side = 1;
                frac2 = (t1 + offset + EPSILON) * idist;
                frac = (t1 - offset - EPSILON) * idist;
            }
            else if (t1 > t2)
            {
                idist = 1.0f / (t1 - t2);
                side = 0;
                frac2 = (t1 - offset - EPSILON) * idist;
                frac = (t1 + offset + EPSILON) * idist;
            }
            else
            {
                side = 0;
                frac = 1;
                frac2 = 0;
            }

            // move up to the node
            frac = Clamp(frac, 0, 1);
            float midf = p1f + (p2f - p1f) * frac;
            Vector3 mid = p1 + (p2 - p1) * frac;

            RecursiveHullCheck(tw, node.children[side], p1f, midf, p1, mid);

            // go past the node
            frac2 = Clamp(frac2, 0, 1);
            midf = p1f + (p2f - p1f) * frac2;
            mid = p1 + (p2 - p1) * frac2;

            RecursiveHullCheck(tw, node.children[side ^ 1], midf, p2f, mid, p2);
        }

        //void TraceToLeaf(traceWork_t tw, int ndxLeaf, float startFrac, float endFrac)
        //{
        //    //int nCurrentDepth = CurrentCheckCount();
        //    //int nDepth = CurrentCheckCountDepth();

        //    // get the leaf
        //    dleaf_t pLeaf = leafs[ndxLeaf];

        //    //
        //    // trace ray/box sweep against all brushes in this leaf
        //    //
        //    for (int ndxLeafBrush = 0; ndxLeafBrush < pLeaf.numleafbrushes; ndxLeafBrush++)
        //    {
        //        // get the current brush
        //        int ndxBrush = leafbrushes[pLeaf.firstleafbrush + ndxLeafBrush];
        //        dbrush_t brush = brushes[ndxBrush];

        //        //// make sure we only check this brush once per trace/stab
        //        //if (brush.checkcount[nDepth] == nCurrentCheckCount)
        //        //    continue;
                
        //        //// mark the brush as checked
        //        //brush.checkcount[nDepth] = nCurrentCheckCount;

        //        // only collide with objects you are interested in
        //        if (((int)brush.contents & tw.contents) == 0)
        //            continue;

        //        // trace against the brush and find impact point -- if any?
        //        // NOTE: trace_trace.fraction == 0.0f only when trace starts inside of a brush!
        //        ClipBoxToBrush(tw, brush);
        //        if (tw.trace.fraction == 0.0f)
        //            return;
        //    }

        //    // TODO: this may be redundant
        //    if (tw.trace.startsolid)
        //        return;
        //}

        

        float Clamp(float value, float min, float max)
        {
            if (value > max)
                value = max;
            if (value < min)
                value = min;
            return value;
        }

        ///*
        //==================
        //CM_TraceThroughTree

        //Traverse all the contacted leafs from the start to the end position.
        //If the trace is a point, they will be exactly in order, but for larger
        //trace volumes it is possible to hit something in a later leaf with
        //a smaller intercept fraction.
        //==================
        //*/
        //void TraceThroughTree(traceWork_t tw, int num, float p1f, float p2f, Vector3 p1, Vector3 p2)
        //{
        //    //TraceTest(0);
        //    if (tw.trace.fraction <= p1f)
        //        return; // already hit something nearer

        //    // if < 0, we are in a leaf node
        //    if (num < 0)
        //    {
        //        TraceThroughLeaf(tw, leafs[-1-num]);
        //        return;
        //    }
            
        //    //
        //    // find the point distances to the seperating plane
        //    // and the offset for the size of the box
        //    //
        //    dnode_t node = nodes[num];
        //    cplane_t plane = node.plane;

        //    // adjust the plane distance apropriately for mins/maxs
        //    float t1, t2, offset;
        //    if (plane.type < 3)
        //    {
        //        t1 = p1[plane.type] - plane.dist;
        //        t2 = p2[plane.type] - plane.dist;
        //        offset = tw.extents[plane.type];
        //    }
        //    else
        //    {
        //        t1 = Vector3.Dot(plane.normal, p1) - plane.dist;
        //        t2 = Vector3.Dot(plane.normal, p2) - plane.dist;
        //        if (tw.isPoint)
        //            offset = 0;
        //        else
        //        {
        //            offset = (float)(Math.Abs(tw.extents[0] * plane.normal[0]) + Math.Abs(tw.extents[1] * plane.normal[1]) + Math.Abs(tw.extents[2] * plane.normal[2]));
        //            // this is silly
        //            //offset = 2048;
        //        }
        //    }

        //    // see which sides we need to consider
        //    if (t1 > offset + 1 && t2 > offset + 1)
        //    {
        //        TraceThroughTree(tw, node.children[0], p1f, p2f, p1, p2);
        //        return;
        //    }
        //    if (t1 < -offset - 1 && t2 < -offset - 1)
        //    {
        //        TraceThroughTree(tw, node.children[1], p1f, p2f, p1, p2);
        //        return;
        //    }

        //    // put the crosspoint SURFACE_CLIP_EPSILON pixels on the near side
        //    float idist, frac, frac2;
        //    int side;
        //    if (t1 < t2)
        //    {
        //        idist = 1.0f / (t1 - t2);
        //        side = 1;
        //        frac2 = (t1 + offset + EPSILON) * idist;
        //        frac = (t1 - offset + EPSILON) * idist;
        //    }
        //    else if (t1 > t2)
        //    {
        //        idist = 1.0f / (t1 - t2);
        //        side = 0;
        //        frac2 = (t1 - offset - EPSILON) * idist;
        //        frac = (t1 + offset + EPSILON) * idist;
        //    }
        //    else
        //    {
        //        side = 0;
        //        frac = 1;
        //        frac2 = 0;
        //    }

        //    // move up to the node
        //    if (frac < 0f)
        //        frac = 0;
        //    if (frac > 1f)
        //        frac = 1;

        //    float midf = p1f + ((p2f - p1f) * frac);
        //    Vector3 mid = p1 + (p2 - p1) * frac;

        //    TraceThroughTree(tw, node.children[side], p1f, midf, p1, mid);

        //    // go past the node
        //    if (frac2 < 0f)
        //        frac2 = 0;
        //    if (frac2 > 1f)
        //        frac2 = 1f;

        //    midf = p1f + ((p2f - p1f) * frac2);
        //    mid = p1 + (frac2 * (p2 - p1));

        //    TraceThroughTree(tw, node.children[side ^ 1], midf, p2f, mid, p2);
        //}

        void TraceThroughLeaf(traceWork_t tw, int num)
        {
            dleaf_t leaf = leafs[num];
            dbrush_t b;
            // trace line against all brushes in the leaf
            int brushnum = leaf.numleafbrushes;
            if (brushnum == 0)
                brushnum++;
            for (int k = 0; k < brushnum; k++)
            {
                b = brushes[leafbrushes[leaf.firstleafbrush + k]];

                if (b.checkcount == checkcount)
                    continue;   // already checked this brush in another leaf
                b.checkcount = checkcount;

                // Assert that the brush contains the needed contents
                if (((int)b.contents & tw.contents) == 0)
                    continue;

                // Check bounds of brush
                if (!BoundsIntersect(tw.bounds[0], tw.bounds[1], b.boundsmin, b.boundsmax))
                    continue;

                //ClipBoxToBrush(tw, b);
                TraceThroughBrush(tw, b);
                // Return now if we were blocked 100%
                if (tw.trace.fraction == 0.0f)
                    return;
            }
            if (tw.trace.fraction == 0.0f)
                return;
            //// Check displacements
            //if (Renderer.Instance.SourceMap != null)
            //{
            //    World world = Renderer.Instance.SourceMap.world;
            //    leaf = world.leafs[num];
            //    if (leaf.DisplacementIndexes != null)
            //    {
            //        KeyValuePair<int,int>[] indx = leaf.DisplacementIndexes;
            //        for (int i = 0; i < indx.Length; i++)
            //        {

            //            DispCollTree dispTree = world.dispCollTrees[indx[i].Value];

            //            if ((dispTree.m_Contents & tw.contents) == 0)
            //                continue;

            //            TraceToDispTree(dispTree, tw.start, tw.end, tw.size[0], tw.size[1], 0f, 1f, tw.trace, false);

            //            if (tw.trace.fraction == 0f)
            //                break;
            //            //if (face2.lastVisCount != VisCount)
            //            //{
            //            //    face2.lastVisCount = VisCount;
            //            //    visibleRenderItems[face2.item.material.MaterialID].Add(face2.item);
            //            //}
            //        }
            //    }
            //}

            if (tw.trace.fraction != 1.0f)
            {
                //
                // determine whether or not we are in solid
                //	
                Vector3 traceDir = tw.end - tw.start;
                if (Vector3.Dot(tw.trace.plane.normal, traceDir) > 0.0f)
                {
                    tw.trace.allsolid = true;
                    tw.trace.startsolid = true;
                }
            }
        }

        void TraceThroughBrush(traceWork_t tw, dbrush_t brush)
        {
            float enterFrac = -9999f;
            float leaveFrac = 1f;
            cplane_t clipplane = null;

            if (brush.numsides <= 0)
                return;

            c_brush_traces++;
            bool getout = false;
            bool getoutTemp = false;
            bool startout = false;
            bool startoutTemp = false;
            dbrushside_t leadside = null;
            cplane_t plane = null;
            //
            // compare the trace against all planes of the brush
            // find the latest time the trace crosses a plane towards the interior
            // and the earliest time the trace crosses a plane towards the exterior
            //
            for (int i = 0; i < brush.numsides; ++i)
            {
                dbrushside_t side = brushsides[brush.firstside + i];
                plane = side.plane;

                // sanity check
                if (side.plane.normal.X == 0f && side.plane.normal.Y == 0f && side.plane.normal.Z == 0f)
                    continue;

                float dist;
                if (!tw.isPoint)
                    dist = plane.dist - Vector3.Dot(tw.offsets[plane.signbits], plane.normal);
                else // ray
                {
                    dist = plane.dist;
                    // dont trace rays against bevel planes
                    if (side.bevel > 0)
                        continue;
                }

                float d1 = Vector3.Dot(tw.start, plane.normal) - dist;
                float d2 = Vector3.Dot(tw.end, plane.normal) - dist;

                // if completely in front of face, no intersection
                if (d1 > 0.0f)
                {

                    startout = true;
                    // d1 > 0.f && d2 > 0.f
                    if (d2 > 0.0f)
                        return;

                    
                }
                else
                {
                    // d1 <= 0.f && d2 <= 0.f
                    if (d2 <= 0.0f)
                        continue;

                    // d2 > 0.f
                    getout = true;  // endpoint is not in solid
                }

                //startout = startoutTemp;
                //getout = getoutTemp;

                // crosses face
                if (d1 > d2)    // enter
                {
                    // enter
                    // JAY: This could be negative if d1 is less than the epsilon.
                    // If the trace is short (d1-d2 is small) then it could produce a large
                    // negative fraction.  I can't believe this didn't break Q2!
                    float f = (d1 - EPSILON);
                    if (f < 0.0f)
                        f = 0.0f;
                    f = f / (d1 - d2);
                    if (f > enterFrac)
                    {
                        enterFrac = f;
                        
                        clipplane = plane;
                        leadside = side;

                    }
                }
                else // leave
                {
                    float f = (d1 + EPSILON) / (d1 - d2);
                    //if (f > 1)
                    //    f = 1;
                    if (f < leaveFrac)
                    {
                        leaveFrac = f;
                        
                    }
                }
            }

                ////when this happens, we entered the brush *after* leaving the previous brush.
                //// Therefore, we're still outside!

                //// NOTE: We only do this test against points because fractionleftsolid is
                //// not possible to compute for brush sweeps without a *lot* more computation
                //// So, client code will never get fractionleftsolid for box sweeps
                //if (tw.isPoint && startout)
                //{
                //    // Add a little sludge.  The sludge should already be in the fractionleftsolid
                //    // (for all intents and purposes is a leavefrac value) and enterfrac values.  
                //    // Both of these values have +/- DIST_EPSILON values calculated in.  Thus, I 
                //    // think the test should be against "0.0."  If we experience new "left solid"
                //    // problems you may want to take a closer look here!
                //    //		if ((trace->fractionleftsolid - enterfrac) > -1e-6)
                //    if (tw.trace.fractionleftsolid - enterFrac > 0.0f)
                //        startout = false;
                //}

            //
            // all planes have been checked, and the trace was not
            // completely outside the brush
            //
            if (!startout)
            {
                // original point was inside brush
                tw.trace.startsolid = true;
                tw.trace.contents = (int)brush.contents;

                if (!getout)
                {
                    tw.trace.allsolid = true;
                    //tw.trace.plane = plane;
                    tw.trace.fraction = 0f;
                    //tw.trace.fractionleftsolid = 1.0f;
                }
                else
                {
                    // if leavefrac == 1, this means it's never been updated or we're in allsolid
                    // the allsolid case was handled above
                    //if ((leaveFrac != 1.0f) && (leaveFrac > tw.trace.fractionleftsolid))
                    //{
                    //    tw.trace.fractionleftsolid = leaveFrac;

                    //    // This could occur if a previous trace didn't start us in solid
                    //    if (tw.trace.fraction <= leaveFrac)
                    //    {
                    //        tw.trace.fraction = 1.0f;
                    //    }
                    //}
                }
                return;
            }

            // We haven't hit anything at all until we've left...
            if (enterFrac < leaveFrac)
            {
                if (enterFrac > -9999.0f && enterFrac < tw.trace.fraction)
                {
                    if (enterFrac < 0)
                        enterFrac = 0;
                    tw.trace.fraction = enterFrac;
                    tw.trace.plane = clipplane;
                    if(leadside.texinfo != -1)
                        tw.trace.surfaceFlags = (int)texinfos[leadside.texinfo].flags;
                    //tw.trace.surfaceFlags = leadside.bevel;
                    tw.trace.contents = (int)brush.contents;

                }
            }
        }

        void TraceToDispTree(DispCollTree dispTree, Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, float startf, float endf, trace_t trace, bool raycast)
        {
            if (raycast)
            {

            }
            else
            {
                Vector3 boxExtends = ((mins + maxs) * 0.5f) - mins;
                if(dispTree.AABBSweep(start, end, boxExtends, startf, endf, ref trace)) 
                {

                }
            }
        }

        //void ClipBoxToBrush(traceWork_t tw, dbrush_t brush)
        //{
        //    if (brush.numsides <= 0)
        //        return;

        //    float enterFrac = -9999f;
        //    float leaveFrac = 1.0f;
        //    cplane_t clipPlane = null;

        //    bool getout = false;
        //    bool getout2 = false;
        //    bool startout = false;
        //    bool startout2 = false;
        //    dbrushside_t leadside = null;

        //    float dist;
        //    cplane_t plane = new cplane_t();

        //    dbrushside_t side;
        //    for (int i = 0; i < brush.numsides; i++)
        //    {
        //        side = brushsides[brush.firstside+i];
        //        plane = side.plane;

        //        if (!tw.isPoint)
        //        {
        //            // general box case

        //            // push the plane out apropriately for mins/maxs

        //            // FIXME: special case for axial - NOTE: These axial planes are not always positive!!!
        //            // FIXME: use signbits into 8 way lookup for each mins/maxs

        //            // Compute the sign bits
        //            //unsigned nSigns  =  ( *(int*)(&plane->normal[0])) & 0x80000000;
        //            //		   nSigns |= (( *(int*)(&plane->normal[1]) & 0x80000000 ) >> 1);
        //            //         nSigns |= (( *(int*)(&plane->normal[2]) & 0x80000000 ) >> 2);

        //            Vector3 ofs = Vector3.Zero;
        //            ofs[0] = (plane.normal[0] < 0.0f) ? tw.extents[0] : tw.extents[0] * -1f;
        //            ofs[1] = (plane.normal[1] < 0.0f) ? tw.extents[1] : tw.extents[1] * -1f;
        //            ofs[2] = (plane.normal[2] < 0.0f) ? tw.extents[2] : tw.extents[2] * -1f;

        //            dist = plane.dist - Vector3.Dot(ofs, plane.normal);
        //        }
        //        else
        //        {
        //            // special point case
        //            dist = plane.dist;
        //            // don't trace rays against bevel planes 
        //            if (side.bevel > 0)
        //                continue;
        //        }

        //        //for (int j = 0; j < planes.Count; j++)
        //        //{
        //        //    if ( planes[j].dist == -512f)
        //        //    {
        //        //        int test = 2;
        //        //    }
        //        //}

        //        //if ( plane.normal.Y == -1.0f && plane.dist == -544.0f)
        //        //{
        //        //    int test = 2;
        //        //}

        //        float d1 = Vector3.Dot(tw.start, plane.normal) - dist;
        //        float d2 = Vector3.Dot(tw.end, plane.normal) - dist;

        //        // if completely in front of face, no intersection
        //        if (d1 > 0.0f)
        //        {
        //            startout2 = true;

        //            //if (d2 > 0.0f)
        //            //    return;
        //        }
        //        else
        //        {
        //            if (d2 <= 0.0f)
        //                continue;

        //            getout2 = true;
        //        }

        //        // crosses face
        //        if (d1 > d2)
        //        {
        //            // enter
        //            // JAY: This could be negative if d1 is less than the epsilon.
        //            // If the trace is short (d1-d2 is small) then it could produce a large
        //            // negative fraction.  I can't believe this didn't break Q2!
        //            float f = (d1 - EPSILON);
        //            if (f < 0.0f)
        //                f = 0.0f;
        //            f = f / (d1 - d2);
        //            if (f > enterFrac)
        //            {
        //                enterFrac = f;
        //                clipPlane = plane;
        //                leadside = side;
        //                getout = getout2;
        //                startout = startout2;
        //            }

        //        }
        //        else
        //        {
        //            // leave
        //            float f = (d1 + EPSILON) / (d1 - d2);
        //            if (f < leaveFrac)
        //            {
        //                leaveFrac = f;
        //                getout = getout2;
        //                startout = startout2;
        //            }
        //        }
        //    }

        //    // when this happens, we entered the brush *after* leaving the previous brush.
        //    // Therefore, we're still outside!

        //    // NOTE: We only do this test against points because fractionleftsolid is
        //    // not possible to compute for brush sweeps without a *lot* more computation
        //    // So, client code will never get fractionleftsolid for box sweeps
        //    if (tw.isPoint && startout)
        //    {
        //        // Add a little sludge.  The sludge should already be in the fractionleftsolid
        //        // (for all intents and purposes is a leavefrac value) and enterfrac values.  
        //        // Both of these values have +/- DIST_EPSILON values calculated in.  Thus, I 
        //        // think the test should be against "0.0."  If we experience new "left solid"
        //        // problems you may want to take a closer look here!
        //        //		if ((trace->fractionleftsolid - enterfrac) > -1e-6)
        //        if (tw.trace.fractionleftsolid - enterFrac > 0.0f)
        //            startout = false;
        //    }

        //    if (!startout)
        //    {
        //        // original point was inside brush
        //        tw.trace.startsolid = true;
        //        // return starting contents
        //        tw.trace.contents = (int)brush.contents;

        //        if (!getout)
        //        {
        //            tw.trace.allsolid = true;
        //            tw.trace.plane = plane;
        //            tw.trace.fraction = 0.0f;
        //            tw.trace.fractionleftsolid = 1.0f;
        //        }
        //        else
        //        {
        //            // if leavefrac == 1, this means it's never been updated or we're in allsolid
        //            // the allsolid case was handled above
        //            if ((leaveFrac != 1.0f) && (leaveFrac > tw.trace.fractionleftsolid))
        //            {
        //                tw.trace.fractionleftsolid = leaveFrac;

        //                // This could occur if a previous trace didn't start us in solid
        //                if (tw.trace.fraction <= leaveFrac)
        //                {
        //                    tw.trace.fraction = 1.0f;
        //                }
        //            }
        //        }
        //        return;

        //    }

        //    // We haven't hit anything at all until we've left...
        //    if (enterFrac < leaveFrac)
        //    {
        //        if (enterFrac > -9999 && enterFrac < tw.trace.fraction)
        //        {
        //            if (enterFrac < 0)
        //                enterFrac = 0;
        //            tw.trace.fraction = enterFrac;
        //            tw.trace.plane = clipPlane;
        //            tw.trace.contents = (int)brush.contents;
        //        }
        //    }
        //}

        bool BoundsIntersect(Vector3 mins, Vector3 maxs, Vector3 mins2, Vector3 maxs2)
        {
            if (maxs[0] < mins2[0] - EPSILON ||
                maxs[1] < mins2[1] - EPSILON ||
                maxs[2] < mins2[2] - EPSILON ||
                mins[0] > maxs2[0] + EPSILON ||
                mins[1] > maxs2[1] + EPSILON ||
                mins[2] > maxs2[2] + EPSILON)
            {
                return false;
            }

            return true;
        }

        //int FindParentNode(int nodeid)
        //{
        //    //System.Console.WriteLine("Parent for " + nodeid);
        //    if (nodeid == 0)
        //        return 0;
        //    for (int i = 0; i < nodes.Length; i++)
        //    {
        //        if (nodes[i].children[0] == nodeid)
        //            return FindParentNode(i);
        //        else if (nodes[i].children[1] == nodeid)
        //            return FindParentNode(i);
        //    }
        //    return -1;
        //}

        void PositionTest(traceWork_t tw)
        {
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
                TraceThroughLeaf(tw, lleafs[i]);
                if (tw.trace.allsolid)
                    break;
            }

        }

        public void BoxLeafnums(leafList_t ll, int nodenum)
        {
            cplane_t plane;
            dnode_t node;
            int s;
            while (true)
            {
                if (nodenum < 0)
                {
                    StoreLeafs(ll, nodenum);
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

        //cmodel_t ClipHandleToModel(int handle)
        //{
        //    if (handle < 0)
        //        Common.Instance.Error("ClipHandleToModel: bad handle " + handle);

        //    //if(handle < numSubModels)
        //    return new cmodel_t();
        //}

        //void TestInLeaf(traceWork_t tw, dleaf_t leaf)
        //{
        //    int brushnum;
        //    dbrush_t b;
        //    // test box position against all brushes in the leaf
        //    for (int k = 0; k < leaf.numleafbrushes; k++)
        //    {
        //        brushnum = leafbrushes[leaf.firstleafbrush + k];
        //        b = brushes[brushnum];
        //        //if (b.checkcount == checkcount)
        //        //    continue;   // already checked this brush in another leaf
        //        //b.checkcount = checkcount;

        //        if (((int)b.contents & tw.contents) != tw.contents)
        //            continue;

        //        TestBoxInBrush(tw, b);
        //        if (tw.trace.allsolid)
        //            return;
        //    }

        //}

        //void TestBoxInBrush(traceWork_t tw, dbrush_t brush)
        //{
        //    if (brush.numsides <= 0)
        //        return;
            
            
        //    // special test for axial
        //    if (tw.bounds[0][0] > brush.boundsmax[0]
        //        || tw.bounds[0][1] > brush.boundsmax[1]
        //        || tw.bounds[0][2] > brush.boundsmax[2]
        //        || tw.bounds[1][0] < brush.boundsmin[0]
        //        || tw.bounds[1][1] < brush.boundsmin[1]
        //        || tw.bounds[1][2] < brush.boundsmin[2])
        //        return;

        //    Vector3 startp;
        //    if (tw.sphere.use)
        //    {
        //        // the first six planes are the axial planes, so we only
        //        // need to test the remainder
        //        for (int i = 0; i < 6; i++)
        //        {
        //            dbrushside_t side = brush.sides[i];
        //            cplane_t plane = side.plane;

        //            // adjust the plane distance apropriately for radius
        //            float dist = plane.dist + tw.sphere.radius;

        //            // find the closest point on the capsule to the plane
        //            float t = Vector3.Dot(plane.normal, tw.sphere.offset);
        //            if (t > 0f)
        //                startp = tw.start - tw.sphere.offset;
        //            else
        //                startp = tw.start + tw.sphere.offset;

        //            float d1 = Vector3.Dot(startp, plane.normal) - dist;
        //            // if completely in front of face, no intersection
        //            if (d1 > 0f)
        //                return;
        //        }
        //    }
        //    else
        //    {
        //        // the first six planes are the axial planes, so we only
        //        // need to test the remainder
        //        for (int i = 0; i < 6; i++)
        //        {
        //            dbrushside_t side = brush.sides[i];
        //            cplane_t plane = side.plane;

        //            // adjust the plane distance apropriately for mins/maxs
        //            float dist = plane.dist - Vector3.Dot(tw.offsets[plane.signbits], plane.normal);
        //            float d1 = Vector3.Dot(tw.start, plane.normal) - dist;

        //            // if completely in front of face, no intersection
        //            if (d1 > 0f)
        //                return;
        //        }
        //    }

        //    // inside this brush
        //    tw.trace.startsolid = tw.trace.allsolid = true;
        //    tw.trace.fraction = 0f;
        //    tw.trace.contents = (int)brush.contents;
        //}

        
    }
}
