using System;
using System.Collections.Generic;
using System.Text;
using CubeHags.client;
using SlimDX;
using CubeHags.common;
using CubeHags.client.map.Source;

namespace CubeHags.server
{
    public sealed partial class Server
    {

        public trace_t SV_Trace(Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, int passEntityNum, int contentMask)
        {
            trace_t results;
            if (mins == null)
                mins = Vector3.Zero;
            if (maxs == null)
                maxs = Vector3.Zero;

            // clip to world
            MoveClip clip = new MoveClip();
            clip.trace = ClipMap.Instance.Box_Trace(start, end, mins, maxs, 0, contentMask);
            clip.trace.entityNum = (clip.trace.fraction != 1.0f) ? 1022 : 1023;
            if (clip.trace.fraction == 0f)
            {
                return clip.trace; // blocked immediately by the world
            }


            clip.contentmask = contentMask;
            clip.start = start;
            clip.end = end;
            clip.mins = mins;
            clip.maxs = maxs;
            clip.passEntityNum = passEntityNum;
            clip.capsule = 0;

            // create the bounding box of the entire move
            // we can limit it to the part of the move not
            // already clipped off by the world, which can be
            // a significant savings for line of sight and shot traces
            for (int i = 0; i < 3; i++)
            {
                if (end[i] > start[i])
                {
                    clip.boxmins[i] = clip.start[i] + clip.mins[i] - 1;
                    clip.boxmaxs[i] = clip.end[i] + clip.maxs[i] + 1;
                }
                else
                {
                    clip.boxmins[i] = clip.end[i] + clip.mins[i] - 1;
                    clip.boxmaxs[i] = clip.start[i] + clip.maxs[i] + 1;
                }
            }

            // clip to other solid entities
            ClipMoveToEntities(clip);
            results = clip.trace;
            return results;
        }

        void ClipMoveToEntities(MoveClip clip)
        {
            
            AreaParams ap = AreaEntities(clip.boxmins, clip.boxmaxs, 1024);

            int passownernum;
            if (clip.passEntityNum != 1023)
            {
                passownernum = sv.gentities[clip.passEntityNum].r.ownerNum;
                if (passownernum == 1023)
                    passownernum = -1;
            }
            else
                passownernum = -1;

            for (int i = 0; i < ap.count; i++)
            {
                
                if (clip.trace.allsolid)
                    return;
                sharedEntity touch = sv.gentities[ap.list[i]];

                // see if we should ignore this entity
                if (clip.passEntityNum != 1023)
                {
                    if (ap.list[i] == clip.passEntityNum)
                        continue;   // don't clip against the pass entity
                    if (touch.r.ownerNum == clip.passEntityNum)
                        continue;   // don't clip against own missiles
                    if (touch.r.ownerNum == passownernum)
                        continue;   // don't clip against other missiles from our owner
                }

                // if it doesn't have any brushes of a type we
                // are looking for, ignore it
                if ((clip.contentmask & touch.r.contents) == 0)
                    continue;

                // might intersect, so do an exact clip
                int clipHandle = ClipHandleForEntity(touch);

                Vector3 origin = touch.r.currentOrigin;
                Vector3 angles = touch.r.currentAngles;

                if (!touch.r.bmodel)
                    angles = Vector3.Zero;

                trace_t trace = ClipMap.Instance.TransformedBoxTrace(clip.start, clip.end, clip.mins, clip.maxs, clipHandle, clip.contentmask, origin, angles);

                if (trace.allsolid)
                {
                    clip.trace.allsolid = true;
                    trace.entityNum = touch.s.number;
                }
                else if (trace.startsolid)
                {
                    clip.trace.startsolid = true;
                    trace.entityNum = touch.s.number;
                }

                if (trace.fraction < clip.trace.fraction)
                {
                    bool oldstart = clip.trace.startsolid;

                    trace.entityNum = touch.s.number;
                    clip.trace = trace;
                    clip.trace.startsolid |= oldstart;
                }

            }
        }

        int ClipHandleForEntity(sharedEntity ent)
        {
            return ClipMap.Instance.TempBoxModel(ent.r.mins, ent.r.maxs);
        }

        // Area Query
        class AreaParams 
        {
            public Vector3 mins, maxs;
            public int[] list;
            public int count;
        }

        AreaParams AreaEntities(Vector3 mins, Vector3 maxs, int count)
        {
            AreaParams ap = new AreaParams();
            ap.mins = mins;
            ap.maxs = maxs;
            ap.list = new int[count];
            ap.count = 0;

            AreaEntities_r(sv_worldSectors[0], ap);

            return ap;
        }

        void AreaEntities_r(worldSector_t node, AreaParams ap)
        {
            svEntity_t check, next = null;
            sharedEntity gcheck;

            for (check = node.entities; check != null; check = next )
            {
                next = check.nextEntityInWorldSector;

                gcheck = GEntityForSvEntity(check);

                if (gcheck.r.absmin[0] > ap.maxs[0]
                    || gcheck.r.absmin[1] > ap.maxs[1]
                    || gcheck.r.absmin[2] > ap.maxs[2]
                    || gcheck.r.absmax[0] < ap.mins[0]
                    || gcheck.r.absmax[1] < ap.mins[1]
                    || gcheck.r.absmax[2] < ap.mins[2])
                    continue;

                if (ap.list.Length == ap.count)
                {
                    Common.Instance.WriteLine("AreaEntities: MAXCOUNT");
                    return;
                }

                ap.list[ap.count] = check.id;
                ap.count++;
            }

            if (node.axis == -1)
                return; // terminal node

            // recurse down both sides
            if (ap.maxs[node.axis] > node.dist)
                AreaEntities_r(node.children[0], ap);
            if (ap.mins[node.axis] < node.dist)
                AreaEntities_r(node.children[1], ap);
        }
    }
}
