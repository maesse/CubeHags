using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;

namespace CubeHags.client.map.Source
{
    public class Trace
    {
        public static float EPSILON = (1f / 32f);
        public TraceOutput TraceS(Vector3 inputStart, Vector3 inputEnd)
        {
            TraceOutput result = new TraceOutput();

            //if (inputStart.Equals(Vector3.Zero) && inputEnd.Equals(Vector3.Zero))
            //{
            //    result.IsPoint = true;
            //}
            //else
            //{
            //    result.extends = new Vector3(
            //}

            // walk through the BSP tree
            //CheckNode(0, 0f, 1f, inputStart, inputEnd, result);

            if (result.outputFraction == 1f)
            {
                // nothing blocked the trace
                result.outputEnd = inputEnd;
            }
            else
            {
                result.outputEnd.X = inputStart.X + result.outputFraction * (inputEnd.X - inputStart.X);
                result.outputEnd.Y = inputStart.Y + result.outputFraction * (inputEnd.Y - inputStart.Y);
                result.outputEnd.Z = inputStart.Z + result.outputFraction * (inputEnd.Z - inputStart.Z);
            }

            return result;
        }

        //void CheckBrush(dbrush_t brush, Vector3 start, Vector3 end, TraceOutput result)
        //{
        //    float startFraction = -1.0f;
        //    float endFraction = 1.0f;
        //    bool startsOut = false;
        //    bool endsOut = false;

        //    for (int i = 0; i < brush.numsides; i++)
        //    {
        //        dbrushside_t brushSide = brushsides[brush.firstside + i];
        //        plane_t plane = planes_t[brushSide.planenum];

        //        float startDistance = Vector3.Dot(start, plane.normal) - plane.dist;
        //        float endDistance = Vector3.Dot(end, plane.normal) - plane.dist;

        //        if (startDistance > 0)
        //            startsOut = true;
        //        if (endDistance > 0)
        //            endsOut = true;

        //        // make sure the trace isn't completely on one side of the brush
        //        if (startDistance > 0 && endDistance > 0)
        //        {   // both are in front of the plane, its outside of this brush
        //            return;
        //        }
        //        if (startDistance <= 0 && endDistance <= 0)
        //        {   // both are behind this plane, it will get clipped by another one
        //            continue;
        //        }

        //        if (startDistance > endDistance)
        //        {   // line is entering into the brush
        //            float fraction = (startDistance - EPSILON) / (startDistance - endDistance);  // *
        //            if (fraction > startFraction)
        //                startFraction = fraction;
        //        }
        //        else
        //        {   // line is leaving the brush
        //            float fraction = (startDistance + EPSILON) / (startDistance - endDistance);  // *
        //            if (fraction < endFraction)
        //                endFraction = fraction;
        //        }
        //        break;
        //    }

        //    if (startsOut == false)
        //    {
        //        result.outputStartsOut = false;
        //        if (endsOut == false)
        //            result.outputAllSolid = true;
        //        return;
        //    }

        //    if (startFraction < endFraction)
        //    {
        //        if (startFraction > -1 && startFraction < result.outputFraction)
        //        {
        //            if (startFraction < 0)
        //                startFraction = 0;
        //            result.outputFraction = startFraction;
        //        }
        //    }

        //}

        //void CheckNode(int nodeIndex, float startFraction, float endFraction, Vector3 start, Vector3 end, TraceOutput result)
        //{
        //    if (nodeIndex < 0)
        //    {
        //        dleaf_t leaf = leafs[-(nodeIndex + 1)];
        //        for (int i = 0; i < leaf.numleafbrushes; i++)
        //        {
        //            short brushindex = leafBrushes[leaf.firstleafface + i];
        //            if (brushindex <= brushes.Length && brushindex >= 0)
        //            {
        //                dbrush_t brush = brushes[brushindex];
        //                if (brush.numsides > 0 &&
        //                    (brush.contents & brushflags.CONTENTS_SOLID) == brushflags.CONTENTS_SOLID)
        //                {
        //                    CheckBrush(brush, start, end, result);
        //                }
        //            }
        //        }

        //        return;
        //    }

        //    dnode_t node = nodes[nodeIndex];
        //    plane_t plane = planes_t[node.planenum];

        //    float startDistance = Vector3.Dot(start, plane.normal) - plane.dist;
        //    float endDistance = Vector3.Dot(end, plane.normal) - plane.dist;

        //    if (startDistance >= 0 && endDistance >= 0) // A
        //    {
        //        // Both points in front of plane
        //        CheckNode(node.children[0], startDistance, endFraction, start, end, result);
        //    }
        //    else if (startDistance < 0 && endDistance < 0) // B
        //    {
        //        // Both points in back of plane
        //        CheckNode(node.children[1], startDistance, endFraction, start, end, result);
        //    }
        //    else // C
        //    {
        //        // Points spans the splitting plane
        //        // Split segment in two
        //        int side;
        //        float fraction1, fraction2;
        //        if (startDistance < endDistance)
        //        {
        //            side = 1; // back
        //            float inverseDistance = 1.0f / (startDistance - endDistance);
        //            fraction1 = (startDistance + EPSILON) * inverseDistance;
        //            fraction2 = (startDistance + EPSILON) * inverseDistance;
        //        }
        //        else if (endDistance < startDistance)
        //        {
        //            side = 0; // front
        //            float inverseDistance = 1.0f / (startDistance - endDistance);
        //            fraction1 = (startDistance + EPSILON) * inverseDistance;
        //            fraction2 = (startDistance - EPSILON) * inverseDistance;
        //        }
        //        else
        //        {
        //            side = 0; // front
        //            fraction1 = 1f;
        //            fraction2 = 0f;
        //        }

        //        // Check numbers
        //        if (fraction1 < 0f) fraction1 = 0f;
        //        else if (fraction1 > 1f) fraction1 = 1f;
        //        if (fraction2 < 0f) fraction2 = 0f;
        //        else if (fraction2 > 1f) fraction2 = 1f;

        //        // Calculate middle point for first side
        //        float middleFraction = startFraction + (endFraction - startFraction) * fraction1;
        //        Vector3 middle = Vector3.Zero;
        //        middle.X = start.X + fraction1 * (end.X - start.X);
        //        middle.Y = start.Y + fraction1 * (end.Y - start.Y);
        //        middle.Z = start.Z + fraction1 * (end.Z - start.Z);

        //        // Check first side
        //        CheckNode(node.children[side], startFraction, middleFraction, start, middle, result);

        //        // Calculate middle point for second side
        //        middleFraction = startFraction + (endFraction - startFraction) * fraction2;
        //        middle.X = start.X + fraction2 * (end.X - start.X);
        //        middle.Y = start.Y + fraction2 * (end.Y - start.Y);
        //        middle.Z = start.Z + fraction2 * (end.Z - start.Z);

        //        // Check second side
        //        CheckNode(node.children[(side == 0 ? 1 : 0)], middleFraction, endFraction, middle, end, result);
        //    }
        //}


        // Pack up collision tracing output in a neat little package :)
        public class TraceOutput
        {
            public bool IsCollision { get { return (!outputFraction.Equals(1f)) ? true : false; } }

            public bool outputStartsOut = true;
            public bool outputAllSolid = false;
            public float outputFraction = 1f;
            public Vector3 outputEnd = Vector3.Zero;
            public Vector3 extends = Vector3.Zero;
            public bool IsPoint = true;

            public TraceOutput() { }
            public TraceOutput(bool outputStartsOut, bool outputAllSolid, float outputFraction, Vector3 outputEnd)
            {
                this.outputAllSolid = outputAllSolid;
                this.outputEnd = outputEnd;
                this.outputFraction = outputFraction;
                this.outputStartsOut = outputStartsOut;
            }
        }
    }
}
