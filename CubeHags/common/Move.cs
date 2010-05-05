using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client;
using SlimDX;
using CubeHags.client.render;

namespace CubeHags.common
{
    public sealed partial class Common
    {
        pml_t pml;
        pmove_t pm;

        float pm_maxspeed = 400f;
        float pm_accelerate = 8f;
        float pm_friction = 6f;

        /*
        ================
        Pmove

        Can be called by either the server or the client
        ================
        */
        public void Pmove(pmove_t pm)
        {
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
            // clear results
            pm.numtouch = 0;

            // clear the respawned flag if attack and use are cleared
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

            if (pm.ps.pm_type == PMType.SPECTATOR)
            {
                //CheckDuck();
                FlyMove();
                //DropTimers();
                return;
            }
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
                    wishvel[i] = (scale * pml.forward[i] * Input.Instance.UserCmd.forwardmove) + (scale * pml.right[i] * Input.Instance.UserCmd.rightmove);
                }
                wishvel[2] += scale * Input.Instance.UserCmd.upmove;
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

            StepSlideMove(false);
        }

        private void Accelerate(Vector3 wishdir, float wishspeed, float accel)
        {
            // q2 style
            float currentspeed = Vector3.Dot(pm.ps.velocity, wishdir);
            float addspeed = wishspeed - currentspeed;
            if (addspeed <= 0)
                return;

            float accelspeed = accel * pml.frametime * wishspeed;
            if (accelspeed > addspeed)
                accelspeed = addspeed;

            //System.Console.WriteLine("Acceleration += " + accelspeed);

            for (int i = 0; i < 3; i++)
            {
                pm.ps.velocity[i] += accelspeed * wishdir[i];
            }
        }

        private void StepSlideMove(bool gravity)
        {
            Vector3 start_o = pm.ps.origin;
            Vector3 start_v = pm.ps.velocity;

            if (!SlideMove(gravity))
                return; // we got exactly where we wanted to go first try	

            Vector3 down = start_o;
            down[2] -= 18;
            trace_t trace = new trace_t();
            trace = pm.DoTrace(trace, start_o, pm.mins, pm.maxs, down, pm.ps.clientNum, pm.tracemask);

            Vector3 up = new Vector3(0, 0, 1);
            // never step up when you still have up velocity
            if (pm.ps.velocity[2] > 0f && (trace.fraction == 1.0f || Vector3.Dot(trace.plane.normal, up) > 0.7f))
            {
                return;
            }

            Vector3 down_o = pm.ps.origin;
            Vector3 down_v = pm.ps.velocity;
            up = start_o;
            up[2] += 18;

            // test the player position if they were a stepheight higher
            trace = pm.DoTrace(trace, start_o, pm.mins, pm.maxs, up, pm.ps.clientNum, pm.tracemask);
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
            trace = pm.DoTrace(trace, pm.ps.origin, pm.mins, pm.maxs, down, pm.ps.clientNum, pm.tracemask);
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
                endVelocity[2] -= pm.ps.gravity * pml.frametime;
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
                trace_t trace = new trace_t();

                
                //
                //if(end.Z > -110)
                //    trace.endpos = end;
                //else
                    trace = pm.DoTrace(trace, pm.ps.origin, pm.mins, pm.maxs, end, pm.ps.clientNum, pm.tracemask);

                //trace.fraction = 1f;
                
                if (trace.allsolid)
                {
                    // entity is completely trapped in another solid
                    pm.ps.velocity[2] = 0;  // don't build up falling damage, but allow sideways acceleration
                    return true;
                }

                if (trace.fraction > 0f)
                {
                    // actually covered some distance
                    pm.ps.origin = trace.endpos;
                }

                if (trace.fraction == 1)
                    break;  // moved the entire distance

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

                        if (Vector3.Dot(clipVelocity, planes[i]) > 0.1f)
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

                        d = Vector3.Dot(dir, endVelocity);
                        endClipVelocity = Vector3.Multiply(dir, d);

                        // see if there is a third plane the the new move enters
                        for (int k = 0; k < numplanes; k++)
                        {
                            if (k == i || k == j)
                                continue;

                            if (Vector3.Dot(clipVelocity, planes[k]) >= 0.1f)
                                continue;

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


            //frametime /= 1000;
            float speed = velocity.Length();
            if (speed < 1)
            {
                velocity[0] = 0;
                velocity[1] = 0;
                return;
            }

            float drop = 0;

            
            // if spectator
            if(pm.ps.pm_type == PMType.SPECTATOR)
                drop += speed * pm_friction * pml.frametime;
            //System.Console.WriteLine("Friction speed drop: " + drop + " - delta: " + frametime);

            float newspeed = speed - drop;
            if (newspeed < 0)
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

        public static void UpdateViewAngles(ref Common.playerState_t ps, Input.UserCommand cmd)
        {
            if (ps.pm_type == Common.PMType.INTERMISSION || ps.pm_type == Common.PMType.SPINTERMISSION)
            {
                return;
            }

            if (ps.pm_type != Common.PMType.SPECTATOR && ps.stats[0] <= 0)
            {
                return;
            }

            
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
