using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client;
using SlimDX;
using CubeHags.client.render;
using CubeHags.client.cgame;
using CubeHags.client.map.Source;

namespace CubeHags.common
{
    public sealed partial class Common
    {
        pml_t pml;
        pmove_t pm;
        trace_t lastTrace;

        float pm_maxspeed = 400f;
        float pm_stopspeed = 100.0f;
        float pm_accelerate = 10f;
        float pm_flyaccelerate = 8f;
        float pm_airaccelerate = 1.0f;
        float pm_friction = 6f;
        float pm_spectatorfriction = 5f;
        float pm_flyfriction = 4f;
        float pm_duckScale = 0.25f;
        int c_pmove = 0;

        /*
        ================
        Pmove

        Can be called by either the server or the client
        ================
        */
        public void Pmove(pmove_t pm)
        {
            //this.pm = pm;
            int finalTime = pm.cmd.serverTime;

            if (finalTime < pm.ps.commandTime)
                return; // should not happen

            if (finalTime > pm.ps.commandTime + 1000)
                pm.ps.commandTime = finalTime - 1000;

            pm.ps.pmove_framecount = (pm.ps.pmove_framecount + 1) & ((1 << 6) - 1);
            // chop the move up if it is too long, to prevent framerate
            // dependent behavior
            while (pm.ps.commandTime != finalTime)
            {
                int msec = finalTime - pm.ps.commandTime;

                if (pm.pmove_fixed > 0)
                {
                    if (msec > pm.pmove_msec)
                        msec = pm.pmove_msec;
                }
                else
                {
                    if (msec > 66)
                        msec = 66;
                }

                pm.cmd.serverTime = pm.ps.commandTime + msec;
                PmoveSingle(pm);

                if ((pm.ps.pm_flags & PMFlags.JUMP_HELD) == PMFlags.JUMP_HELD)
                    pm.cmd.upmove = 20;
            }

        }

        void PmoveSingle(pmove_t pmove)
        {
            pm = pmove;
            // this counter lets us debug movement problems with a journal
            // by setting a conditional breakpoint fot the previous frame
            c_pmove++;
            
            // clear results
            pm.numtouch = 0;

            if (pm.ps.stats[0] <= 0)
            {
                pm.tracemask &= ~0x2000000;
            }

            // make sure walking button is clear if they are running, to avoid
            // proxy no-footsteps cheats
            if (Math.Abs(pm.cmd.forwardmove) > 64 || Math.Abs(pm.cmd.rightmove) > 64)
            {
                pm.cmd.buttons &= ~16;
            }

            //// set the firing flag for continuous beam weapons
            //if ((pm.ps.pm_flags & PMFlags.RESPAWNED) != PMFlags.RESPAWNED && pm.ps.pm_type != PMType.INTERMISSION
            //    && (pm.cmd.buttons & 1) == 1 && pm.ps.ammo[pm.ps.weapon] > 0)
            //{
            //    pm.ps.eFlags |= EntityFlags.EF_FIRING;
            //}
            //else
            //    pm.ps.eFlags &= ~EntityFlags.EF_FIRING;

            // clear the respawned flag if attack and use are cleared
            if(pm.ps.stats[0] > 0 && (pm.cmd.buttons & (1|4)) == 0)
                pm.ps.pm_flags &= ~PMFlags.RESPAWNED;

            // clear all pmove local vars
            pml = new pml_t();
            pml.forward = Vector3.Zero;
            pml.up = Vector3.Zero;
            pml.right = Vector3.Zero;

            // determine the time
            pml.msec = pm.cmd.serverTime - pm.ps.commandTime;
            if (pml.msec < 1)
                pml.msec = 1;
            else if (pml.msec > 200)
                pml.msec = 200;

            pm.ps.commandTime = pm.cmd.serverTime;

            // save old org in case we get stuck
            pml.previous_origin = pm.ps.origin;

            // save old velocity for crashlanding
            pml.previous_velocity = pm.ps.velocity;

            pml.frametime = pml.msec * 0.001f;

            // update the viewangles
            UpdateViewAngles(ref pm.ps, pm.cmd);

            AngleVectors(pm.ps.viewangles, ref pml.forward, ref pml.right, ref pml.up);

            if (pm.cmd.upmove < 10)
                // not holding jump
                pm.ps.pm_flags &= ~PMFlags.JUMP_HELD;

            // decide if backpedaling animations should be used
            if (pm.cmd.forwardmove < 0)
                pm.ps.pm_flags |= PMFlags.BACKWARDS_RUN;
            else if (pm.cmd.forwardmove > 0 || (pm.cmd.forwardmove == 0 && pm.cmd.rightmove > 0))
                pm.ps.pm_flags &= ~PMFlags.BACKWARDS_RUN;

            if (pm.ps.pm_type == PMType.DEAD)
            {
                pm.cmd.forwardmove = 0;
                pm.cmd.rightmove = 0;
                pm.cmd.upmove = 0;
            }

            if (pm.ps.pm_type == PMType.SPECTATOR)
            {
                CheckDuck();
                FlyMove();
                DropTimers();
                return;
            }

            if (pm.ps.pm_type == PMType.NOCLIP)
            {
                //NoclipMove();
                DropTimers();
                return;
            }

            if (pm.ps.pm_type == PMType.FREEZE)
                return; // no movement at all

            if (pm.ps.pm_type == PMType.INTERMISSION || pm.ps.pm_type == PMType.SPINTERMISSION)
                return; // no movement at all

            // set mins, maxs, and viewheight
            CheckDuck();

            // set groundentity
            GroundTrace();

            if (pm.ps.pm_type == PMType.DEAD)
            {
                //DeadMove();
            }

            DropTimers();

            if (pml.walking)
            {
                // walking on ground
                WalkMove();
            }
            else
            {
                // airborne
                AirMove();
            }

            // set groundentity, watertype, and waterlevel
            GroundTrace();

            // snap some parts of playerstate to save network bandwidth
            CGame.SnapVector(pm.ps.velocity);
        }

        void CheckDuck()
        {
            pm.mins[0] = -15;
            pm.mins[1] = -15;

            pm.maxs[0] = 15;
            pm.maxs[1] = 15;

            pm.mins[2] = -24;

            if (pm.ps.pm_type == PMType.DEAD)
            {
                pm.maxs[2] = -8;
                pm.ps.viewheight = -16;
                return;
            }

            if (pm.cmd.upmove < 0)
            {
                pm.ps.pm_flags |= PMFlags.DUCKED;
            }
            else
            {
                // stand up if possible
                if ((pm.ps.pm_flags & PMFlags.DUCKED) == PMFlags.DUCKED)
                {
                    // try to stand up
                    pm.maxs[2] = 32;
                    trace_t trace = pm.DoTrace(pm.ps.origin, pm.ps.origin, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
                    if (!trace.allsolid)
                        pm.ps.pm_flags &= ~PMFlags.DUCKED;
                }
            }

            if ((pm.ps.pm_flags & PMFlags.DUCKED) == PMFlags.DUCKED)
            {
                pm.maxs[2] = 16;
                pm.ps.viewheight = 12;
            }
            else
            {
                pm.maxs[2] = 32;
                pm.ps.viewheight = 26;
            }
        }

        bool CheckJump()
        {
            if ((pm.ps.pm_flags & PMFlags.RESPAWNED) == PMFlags.RESPAWNED)
            {
                return false;   // don't allow jump until all buttons are up
            }

            if (pm.cmd.upmove < 10)
            {
                // not holding jump
                return false;
            }

            // must wait for jump to be released
            if ((pm.ps.pm_flags & PMFlags.JUMP_HELD) == PMFlags.JUMP_HELD)
            {
                // clear upmove so cmdscale doesn't lower running speed
                pm.cmd.upmove = 0;
                return false;
            }

            pml.groundPlane = false;    // jumping away
            pml.walking = false;
            pm.ps.pm_flags |= PMFlags.JUMP_HELD;
            pm.ps.groundEntityNum = 1023;
            pm.ps.velocity[2] = 270;

            if (pm.cmd.forwardmove >= 0)
            {
                pm.ps.pm_flags &= ~PMFlags.BACKWARDS_JUMP;
            }
            else
            {
                pm.ps.pm_flags |= PMFlags.BACKWARDS_JUMP;
            }

            return true;
        }

        void AirMove()
        {
            Friction();

            float fmove = pm.cmd.forwardmove;
            float smove = pm.cmd.rightmove;

            Input.UserCommand cmd = pm.cmd;
            float scale = CommandScale(cmd);

            // project moves down to flat plane
            pml.forward[2] = 0;
            pml.right[2] = 0;
            pml.forward.Normalize();
            pml.right.Normalize();

            Vector3 wishvel = (pml.forward * fmove) + (pml.right * smove);
            wishvel[2] = 0;

            Vector3 wishdir = wishvel;
            float wishspeed = wishdir.Length();
            wishdir.Normalize();
            wishspeed *= scale;

            // not on ground, so little effect on velocity
            Accelerate(wishdir, wishspeed, pm_airaccelerate);

            // we may have a ground plane that is very steep, even
            // though we don't have a groundentity
            // slide along the steep plane
            if (pml.groundPlane)
            {
                ClipVelocity(pm.ps.velocity, pml.groundTrace.plane.normal, ref pm.ps.velocity, 1.001f);
            }

            StepSlideMove(true);
        }


        void DropTimers()
        {
            // drop misc timing counter
            if (pm.ps.pm_time > 0)
            {
                if (pml.msec >= pm.ps.pm_time)
                {
                    pm.ps.pm_flags &= ~PMFlags.ALL_TIMES;
                    pm.ps.pm_time = 0;
                }
                else
                    pm.ps.pm_time -= pml.msec;
            }
        }

        void WalkMove()
        {
            if (CheckJump())
            {
                // jumped away
                AirMove();
                return;
            }

            Friction();

            float fmove = pm.cmd.forwardmove;
            float smove = pm.cmd.rightmove;
            Input.UserCommand cmd = pm.cmd;
            float scale = CommandScale(cmd);

            // set the movementDir so clients can rotate the legs for strafing
            //SetMovementDir();

            // project moves down to flat plane
            pml.forward[2] = 0;
            pml.right[2] = 0;

            // project the forward and right directions onto the ground plane
            ClipVelocity(pml.forward, pml.groundTrace.plane.normal, ref pml.forward, 1.001f);
            ClipVelocity(pml.right, pml.groundTrace.plane.normal, ref pml.right, 1.001f);
            pml.forward.Normalize();
            pml.right.Normalize();

            Vector3 wishvel = (pml.forward * fmove) + (pml.right * smove);
            Vector3 wishdir = wishvel;
            float wishpeed = wishdir.Length();
            wishdir.Normalize();
            wishpeed *= scale;

            // clamp the speed lower if ducking
            if ((pm.ps.pm_flags & PMFlags.DUCKED) == PMFlags.DUCKED)
            {
                if (wishpeed > pm.ps.speed * pm_duckScale)
                {
                    wishpeed = pm.ps.speed * pm_duckScale;
                }
            }

            // when a player gets hit, they temporarily lose
            // full control, which allows them to be moved a bit
            float accelerate;
            if ((pml.groundTrace.surfaceFlags & (int)SurfFlags.SURF_SLICK) > 0 || (pm.ps.pm_flags & PMFlags.TIME_KNOCKBACK) == PMFlags.TIME_KNOCKBACK)
            {
                accelerate = pm_airaccelerate;
            }
            else
                accelerate = pm_accelerate;

            Accelerate(wishdir, wishpeed, accelerate);

            if ((pml.groundTrace.surfaceFlags & (int)SurfFlags.SURF_SLICK) > 0 || (pm.ps.pm_flags & PMFlags.TIME_KNOCKBACK) == PMFlags.TIME_KNOCKBACK)
            {
                pm.ps.velocity[2] -= pm.ps.gravity * pml.frametime;
            }

            float vel = pm.ps.velocity.Length();
            // slide along the ground plane
            ClipVelocity(pm.ps.velocity, pml.groundTrace.plane.normal, ref pm.ps.velocity, 1.001f);

            // don't decrease velocity when going up or down a slope
            pm.ps.velocity.Normalize();
            pm.ps.velocity = Vector3.Multiply(pm.ps.velocity, vel);

            // don't do anything if standing still
            if (pm.ps.velocity[0] == 0 && pm.ps.velocity[1] == 0)
                return;

            StepSlideMove(false);

            if (pm.ps.groundEntityNum != 1023)
                pm.ps.velocity[2] = 0;
        }

        void GroundTrace()
        {
            Vector3 point = pm.ps.origin;
            point[2] -= 0.25f;
            trace_t trace = pm.DoTrace(pm.ps.origin, point, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
            pml.groundTrace = trace;

            // do something corrective if the trace starts in a solid...
            if (trace.allsolid)
            {
                if (!CorrectAllSolid(trace))
                    return;
            }

            // if the trace didn't hit anything, we are in free fall
            if (trace.fraction == 1.0f)
            {
                GroundTraceMissed();
                pml.groundPlane = false;
                pml.walking = false;
                return;
            }

            // check if getting thrown off the ground
            if (pm.ps.velocity[2] > 0 && Vector3.Dot(pm.ps.velocity, trace.plane.normal) > 10f)
            {
                //Common.Instance.WriteLine("kickoff");
                pm.ps.groundEntityNum = 1023;
                pml.groundPlane = false;
                pml.walking = false;
                return;
            }

            // slopes that are too steep will not be considered onground
            if (trace.plane.normal[2] < 0.7f)
            {
                //Common.Instance.WriteLine("steep");
                // FIXME: if they can't slide down the slope, let them
                // walk (sharp crevices)
                pm.ps.groundEntityNum = 1023;
                pml.groundPlane = true;
                pml.walking = false;
                return;
            }

            pml.groundPlane = true;
            pml.walking = true;

            if (pm.ps.groundEntityNum == 1023)
            {
                //Common.Instance.WriteLine("land");
                // just hit the ground
                //CrashLand();

                // don't do landing time if we were just going down a slope
                if (pml.previous_velocity[2] < -200)
                {
                    // don't allow another jump for a little while
                    pm.ps.pm_flags |= PMFlags.TIME_LAND;
                    pm.ps.pm_time = 250;
                }
            }

            pm.ps.groundEntityNum = trace.entityNum;
            //AddTouchEnt(trace.entityNum);
        }

        /*
        =============
        PM_GroundTraceMissed

        The ground trace didn't hit a surface, so we are in freefall
        =============
        */
        void GroundTraceMissed()
        {
            if (pm.ps.groundEntityNum != 1023)
            {
                // we just transitioned into freefall

                // if they aren't in a jumping animation and the ground is a ways away, force into it
                // if we didn't do the trace, the player would be backflipping down staircases
                Vector3 point = pm.ps.origin;
                point[2] -= 0.25f;

                trace_t trace = pm.DoTrace(pm.ps.origin, point, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
                if (trace.fraction == 1.0f)
                {
                    if(pm.cmd.forwardmove >= 0)
                        pm.ps.pm_flags &= ~PMFlags.BACKWARDS_JUMP;
                    else
                    {
                        pm.ps.pm_flags |= PMFlags.BACKWARDS_JUMP;
                    }
                }
                
            }

            pm.ps.groundEntityNum = 1023;
            pml.groundPlane = false;
            pml.walking = false;
        }

        bool CorrectAllSolid(trace_t trace)
        {
            Vector3 point;

            // jitter around
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    for (int k = -1; k <= 1; k++)
                    {
                        point = pm.ps.origin;
                        point[0] += (float)i;
                        point[1] += (float)j;
                        point[2] += (float)k;
                        trace = pm.DoTrace(point, point, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
                        if (!trace.allsolid)
                        {
                            point = pm.ps.origin;
                            point[2] -= 0.25f;

                            trace = pm.DoTrace(pm.ps.origin, point, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
                            pml.groundTrace = trace;
                            return true;
                        }
                    }
                }
            }

            pm.ps.groundEntityNum = 1023;
            pml.groundPlane = false;
            pml.walking = false;

            return false;
        }

        void FlyMove()
        {
            // normal slowdown
            Friction();
            float scale = CommandScale(pm.cmd);

            //
            // user intentions
            //
            Vector3 wishvel = Vector3.Zero;
            if (scale != 0f)
            {
                for (int i = 0; i < 3; i++)
                {
                    wishvel[i] = (scale * pml.forward[i] * pm.cmd.forwardmove) + (scale * pml.right[i] * pm.cmd.rightmove);
                }
                wishvel[2] += scale * pm.cmd.upmove;
            }

            Vector3 wishdir = new Vector3(wishvel.X, wishvel.Y, wishvel.Z);
            float wishspeed = wishdir.Length();
            wishdir.Normalize();

            //// Cap speed
            //if (wishspeed > pm_maxspeed)
            //{
            //    wishspeed = pm_maxspeed;
            //}

            // Apply acceleration
            Accelerate(wishdir, wishspeed, pm_accelerate);

            //SlideMove(false);
            StepSlideMove(false);
        }

        private void Accelerate(Vector3 wishdir, float wishspeed, float accel)
        {
            // q2 style
            float currentspeed = Vector3.Dot(pm.ps.velocity, wishdir);
            float addspeed = wishspeed - currentspeed;
            if (addspeed <= 0f)
                return;

            float accelspeed = accel * pml.frametime * wishspeed;
            if (accelspeed > addspeed)
                accelspeed = addspeed;

            //System.Console.WriteLine("Acceleration += " + accelspeed);
            pm.ps.velocity += accelspeed * wishdir;
            //for (int i = 0; i < 3; i++)
            //{
            //    pm.ps.velocity[i] += accelspeed * wishdir[i];
            //}
        }

        private void StepSlideMove(bool gravity)
        {
            Vector3 start_o = pm.ps.origin;
            Vector3 start_v = pm.ps.velocity;

            if (!SlideMove(gravity))
            {
                return; // we got exactly where we wanted to go first try	
            }
            
            Vector3 down = start_o;
            down[2] -= 18;
            trace_t trace = pm.DoTrace(start_o, down, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
            Vector3 up = new Vector3(0f, 0f, 1f);
            // never step up when you still have up velocity
            float dotUp = Vector3.Dot(trace.plane.normal, up);
            if (pm.ps.velocity[2] > 0f && (trace.fraction == 1.0f || dotUp < 0.7f))
            {
                return;
            }

            Vector3 down_o = pm.ps.origin;
            Vector3 down_v = pm.ps.velocity;
            up = start_o;
            up[2] += 18;

            // test the player position if they were a stepheight higher
            trace = pm.DoTrace(start_o, up, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
            if (trace.allsolid)
            {
                return; // can't step up
            }

            float stepSize = trace.endpos[2] - start_o[2];
            
            // try slidemove from this position
            pm.ps.origin = trace.endpos;
            pm.ps.velocity = start_v;

            SlideMove(gravity);
            // push down the final amount
            down = pm.ps.origin;
            down[2] -= 18;
            trace = pm.DoTrace(pm.ps.origin, down, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
            if (!trace.allsolid)
                pm.ps.origin = trace.endpos;
            if (trace.fraction < 1.0f)
                ClipVelocity(pm.ps.velocity, trace.plane.normal, ref pm.ps.velocity, 1.001f);

            float delta = pm.ps.origin[2] - start_o[2];
            if (delta > 2)
            {
                if (delta < 7)
                {
                    //PM_AddEvent(EV_STEP_4);
                }
                else if (delta < 11)
                {
                    //PM_AddEvent(EV_STEP_8);
                }
                else if (delta < 15)
                {
                    //PM_AddEvent(EV_STEP_12);
                }
                else
                {
                    //PM_AddEvent(EV_STEP_16);
                }
            }
           //Common.Instance.WriteLine("stepped");

        }

        /*
        ==================
        PM_SlideMove

        Returns qtrue if the velocity was clipped in some way
        ==================
        */
        private bool SlideMove(bool gravity)
        {
            int numbumps = 4;

            Vector3 primal_velocity = pm.ps.velocity;
            Vector3 endVelocity = Vector3.Zero;
            if (gravity)
            {
                endVelocity = pm.ps.velocity;
                endVelocity[2] -= 800f * pml.frametime;
                pm.ps.velocity[2] = (pm.ps.velocity[2] + endVelocity[2]) * 0.5f;
                primal_velocity[2] = endVelocity[2];
                if (pml.groundPlane)
                {
                    // slide along the ground plane
                    ClipVelocity(pm.ps.velocity, pml.groundTrace.plane.normal, ref pm.ps.velocity, 1.001f);
                }
            }

            float time_left = pml.frametime;

            // never turn against the ground plane
            int numplanes;
            Vector3[] planes = new Vector3[5];
            if (pml.groundPlane)
            {
                numplanes = 1;
                planes[0] = pml.groundTrace.plane.normal;
            }
            else
                numplanes = 0;

            // never turn against original velocity
            planes[numplanes] = Vector3.Zero;
            VectorNormalize2(pm.ps.velocity, ref planes[numplanes]);
            numplanes++;
            int bumpcount;
            for (bumpcount = 0; bumpcount < numbumps; bumpcount++)
            {
                // calculate position we are trying to move to
                Vector3 end = ViewParams.VectorMA(pm.ps.origin, time_left, pm.ps.velocity);

                // see if we can make it there
                trace_t trace = pm.DoTrace(pm.ps.origin, end, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
                
                if (trace.allsolid)
                {
                    // entity is completely trapped in another solid
                    pm.ps.velocity[2] = 0;  // don't build up falling damage, but allow sideways acceleration
                    return true;
                }

                if (trace.fraction > 0f)
                {
                    // actually covered some distance
                    Vector3 save = trace.endpos;
                    pm.ps.origin = save;
                }

                if (trace.fraction == 1.0f)
                    break;  // moved the entire distance

                //lastTrace = trace;

                // save entity for contact
                //AddTouchEnt(trace.entityNum);

                time_left -= time_left * trace.fraction;

                if (numplanes >= 5)
                {
                    // this shouldn't really happen
                    pm.ps.velocity = Vector3.Zero;
                    return true;
                }

                //
                // if this is the same plane we hit before, nudge velocity
                // out along it, which fixes some epsilon issues with
                // non-axial planes
                //
                int i;
                for (i = 0; i < numplanes; i++)
                {
                    if (Vector3.Dot(trace.plane.normal, planes[i]) > 0.99f)
                    {
                        pm.ps.velocity = trace.plane.normal + pm.ps.velocity;
                        break;
                    }
                }

                if (i < numplanes)
                    continue;

                planes[numplanes] = trace.plane.normal;
                numplanes++;
                
                //
                // modify velocity so it parallels all of the clip planes
                //
                // find a plane that it enters
                for (i = 0; i < numplanes; i++)
                {
                    float into = Vector3.Dot(pm.ps.velocity, planes[i]);
                    if (into >= 0.1f)
                        continue;   // move doesn't interact with the plane

                    // see how hard we are hitting things
                    if (-into > pml.impactSpeed)
                        pml.impactSpeed = -into;

                    // slide along the plane
                    Vector3 clipVelocity = Vector3.Zero;
                    ClipVelocity(pm.ps.velocity, planes[i], ref clipVelocity, 1.001f);

                    // slide along the plane
                    Vector3 endClipVelocity = Vector3.Zero;
                    ClipVelocity(endVelocity, planes[i], ref endClipVelocity, 1.001f);

                    // see if there is a second plane that the new move enters
                    for (int j = 0; j < numplanes; j++)
                    {
                        if (j == i)
                            continue;

                        if (Vector3.Dot(clipVelocity, planes[j]) >= 0.1f)
                            continue;   // move doesn't interact with the plane

                        // try clipping the move to the plane
                        ClipVelocity(clipVelocity, planes[j], ref clipVelocity, 1.001f);
                        ClipVelocity(endClipVelocity, planes[j], ref endClipVelocity, 1.001f);

                        // see if it goes back into the first clip plane
                        if (Vector3.Dot(clipVelocity, planes[i]) >= 0f)
                            continue;

                        // slide the original velocity along the crease
                        Vector3 dir = Vector3.Cross(planes[i], planes[j]);
                        dir.Normalize();
                        float d = Vector3.Dot(dir, pm.ps.velocity);
                        clipVelocity = Vector3.Multiply(dir, d);

                        dir = Vector3.Cross(planes[i], planes[j]);
                        dir.Normalize();
                        d = Vector3.Dot(dir, endVelocity);
                        endClipVelocity = Vector3.Multiply(dir, d);

                        // see if there is a third plane the the new move enters
                        for (int k = 0; k < numplanes; k++)
                        {
                            if (k == i || k == j)
                                continue;

                            if (Vector3.Dot(clipVelocity, planes[k]) >= 0.1f)
                                continue;   // move doesn't interact with the plane

                            // stop dead at a tripple plane interaction
                            pm.ps.velocity = Vector3.Zero;
                            return true;
                        }
                    }

                    // if we have fixed all interactions, try another move
                    pm.ps.velocity = clipVelocity;
                    endVelocity = endClipVelocity;
                    break;
                }
            }

            if (gravity)
                pm.ps.velocity = endVelocity;

            // don't change velocity if in a timer (FIXME: is this correct?)
            if (pm.ps.pm_time > 0)
                pm.ps.velocity = primal_velocity;

            return (bumpcount != 0);
        }

        /*
        ==================
        PM_ClipVelocity

        Slide off of the impacting surface
        ==================
        */
        void ClipVelocity(Vector3 inv, Vector3 normal, ref Vector3 outv, float overbounce)
        {
            float backoff = Vector3.Dot(inv, normal);

            if (backoff < 0f)
                backoff *= overbounce;
            else
                backoff /= overbounce;

            for (int i = 0; i < 3; i++)
            {
                float change = normal[i] * backoff;
                outv[i] = inv[i] - change;
            }
        }

        float VectorNormalize2(  Vector3 v, ref Vector3 outv) 
        {
        	float	length, ilength;

        	length = v[0]*v[0] + v[1]*v[1] + v[2]*v[2];
        	length = (float)Math.Sqrt (length);

        	if (length > 0f)
        	{
        		ilength = 1/length;
        		outv[0] = v[0]*ilength;
        		outv[1] = v[1]*ilength;
        		outv[2] = v[2]*ilength;
        	} else {
                outv = Vector3.Zero;
        	}
        		
        	return length;
        }

        private float CommandScale(CubeHags.client.Input.UserCommand cmd)
        {
            int max = Math.Abs((int)cmd.forwardmove);
            if (Math.Abs((int)cmd.rightmove) > max)
            {
                max = Math.Abs((int)cmd.rightmove);
            }
            if (Math.Abs((int)cmd.upmove) > max)
                max = Math.Abs((int)cmd.upmove);

            if (max == 0)
                return 0f;

            float total = (float)Math.Sqrt(cmd.forwardmove * cmd.forwardmove + cmd.rightmove * cmd.rightmove + cmd.upmove * cmd.upmove);
            float scale = (float)pm.ps.speed * max / (127f * total);
            return scale;
        }

        // Handles ground and water friction
        private void Friction()
        {
            Vector3 velocity = pm.ps.velocity;

            Vector3 vec = velocity;
            if (pml.walking)
            {
                vec[2] = 0;
            }

            float speed = vec.Length();
            if (speed < 1f)
            {
                velocity[0] = 0;
                velocity[1] = 0;
                return;
            }

            float drop = 0;

            // apply ground friction
            //if (pm.waterlevel <= 1)
            //{
            if (pml.walking && (pml.groundTrace.surfaceFlags & (int)SurfFlags.SURF_SLICK) == 0)
                {
                    // if getting knocked back, no friction
                    if ((pm.ps.pm_flags & PMFlags.TIME_KNOCKBACK) != PMFlags.TIME_KNOCKBACK)
                    {
                        float control = (speed < pm_stopspeed) ? pm_stopspeed : speed;
                        drop += control * pm_friction * pml.frametime;
                    }
                }
            //}

            
            // if spectator
            if(pm.ps.pm_type == PMType.SPECTATOR)
                drop += speed * pm_friction * pml.frametime;
            //System.Console.WriteLine("Friction speed drop: " + drop + " - delta: " + frametime);

            float newspeed = speed - drop;
            if (newspeed < 0f)
                newspeed = 0;
            newspeed /= speed;

            velocity[0] *= newspeed;
            velocity[1] *= newspeed;
            velocity[2] *= newspeed;

            pm.ps.velocity = velocity;
        }

        public void AngleVectors(Vector3 angles, ref Vector3 forward, ref Vector3 right, ref Vector3 up)
        {
            double angle = angles[1] * (Math.PI * 2 / 360f);
            float sy = (float)Math.Sin(angle);
            float cy = (float)Math.Cos(angle);

            angle = angles[0] * (Math.PI * 2 / 360f);
            float sp = (float)Math.Sin(angle);
            float cp = (float)Math.Cos(angle);

            angle = angles[2] * (Math.PI * 2 / 360f);
            float sr = (float)Math.Sin(angle);
            float cr = (float)Math.Cos(angle);

            if (forward != null)
            {
                forward[0] = cp * cy;
                forward[1] = cp * sy;
                forward[2] = -sp;
            }
            if (right != null)
            {
                right[0] = (-1 * sr * sp * cy + -1 * cr * -sy);
                right[1] = (-1 * sr * sp * sy + -1 * cr * cy);
                right[2] = -1 * sr * cp;
            }
            if (up != null)
            {
                up[0] = (cr * sp * cy + -sr * -sy);
                up[1] = (cr * sp * sy + -sr * cy);
                up[2] = cr * cp;
            }
        }

        public static void UpdateViewAngles(ref Common.PlayerState ps, Input.UserCommand cmd)
        {
            if (ps.pm_type == Common.PMType.INTERMISSION || ps.pm_type == Common.PMType.SPINTERMISSION)
            {
                return;
            }

            //if (ps.pm_type != Common.PMType.SPECTATOR )//&& ps.stats[0] <= 0)
            //{
            //    return;
            //}

            
            // circularly clamp the angles with deltas
            short temp = (short)(cmd.anglex + ps.delta_angles[0]);
            // don't let the player look up or down more than 90 degrees
            if (temp > 16000)
            {
                ps.delta_angles[0] = 16000 - cmd.anglex;
                temp = 16000;
            }
            else if (temp < -16000)
            {
                ps.delta_angles[0] = -16000 - cmd.anglex;
                temp = -16000;
            }

            ps.viewangles[0] = temp * (360.0f / 65536);
            temp = (short)(cmd.angley + ps.delta_angles[1]);
            ps.viewangles[1] = temp * (360.0f / 65536);
            temp = (short)(cmd.anglez + ps.delta_angles[2]);
            ps.viewangles[2] = temp * (360.0f / 65536);
            //Common.Instance.WriteLine(ps.viewangles[0] + "\t:\t" + ps.viewangles[1]);
        }

        // all of the locals will be zeroed before each
        // pmove, just to make damn sure we don't have
        // any differences when running on client or server
        public struct pml_t
        {
        	public Vector3		forward, right, up;
            public float frametime;

            public int msec;

            public bool walking;
            public bool groundPlane;
            public trace_t groundTrace;

            public float impactSpeed;

            public Vector3 previous_origin;
            public Vector3 previous_velocity;
            public int previous_waterlevel;
        }
    }
}
