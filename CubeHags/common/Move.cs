using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client;
using SlimDX;
using CubeHags.client.render;
using CubeHags.client.cgame;
using CubeHags.client.map.Source;
using CubeHags.client.common;

namespace CubeHags.common
{
    public sealed partial class Common
    {
        public static float BUNNYJUMP_MAX_SPEED_FACTOR = 1.7f;
        pml_t pml;
        pmove_t pm = null;
        //trace_t lastTrace;

        float pm_maxspeed = 4000f;
        float pm_maxvelocity = 2000f;
        float pm_stopspeed = 100.0f;
        float pm_accelerate = 10f;
        float pm_airaccelerate = 10f;
        float pm_friction = 4f;

        bool speedCropped = false;
        //float pm_duckScale = 0.25f;
        //int c_pmove = 0;

        /*
        ================
        Pmove

        Can be called by either the server or the client
        ================
        */
        public void Pmove(pmove_t pm)
        {
            //this.pm = pm;
            speedCropped = false;
            int finalTime = pm.cmd.serverTime;

            if (finalTime < pm.ps.commandTime)
                return; // should not happen

            if (finalTime > pm.ps.commandTime + 1000)
                pm.ps.commandTime = finalTime - 1000;

            //pm.ps.speed = (int)pm_maxspeed;
            pm.ps.gravity = CVars.Instance.VariableIntegerValue("sv_gravity");
            
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
                    if (msec > 50)
                        msec = 50;
                }
                pm.cmd.serverTime = pm.ps.commandTime + msec;
                pm.maxSpeed = pm_maxspeed;
                PlayerMoveSingle(pm);
                //PmoveSingle(pm);

                //if ((pm.ps.pm_flags & PMFlags.JUMP_HELD) == PMFlags.JUMP_HELD)
                //    pm.cmd.upmove = 20;
            }
            //pm.ps.OldButtons = pm.cmd.buttons;
        }

        void PlayerMoveSingle(pmove_t pmove)
        {
            pm = pmove;

            // clear all pmove local vars
            pml = new pml_t();
            pml.forward = Vector3.Zero;
            pml.up = Vector3.Zero;
            pml.right = Vector3.Zero;
            pm.upmove = pm.cmd.upmove;
            pm.rightmove = pm.cmd.rightmove;
            pm.forwardmove = pm.cmd.forwardmove;

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

            UpdateViewAngles(ref pm.ps, pm.cmd);
            // Convert view angles to vectors
            AngleVectors(pm.ps.viewangles, ref pml.forward, ref pml.right, ref pml.up);

            // Adjust speeds etc.
            CheckParameters();

            // Assume we don't touch anything
            pm.numtouch = 0;

            DropTimers();

            

            // UnStuck

            // Now that we are "unstuck", see where we are ( waterlevel and type, pmove->onground ).
            CategorizePosition();

            // If we are not on ground, store off how fast we are moving down
            if (!pml.groundPlane)
                pml.fallVelocity = -pm.ps.velocity[2];


            Duck();

            // LadderMove();

            //if (pml.groundPlane && (pm.cmd.buttons & (int)Input.ButtonDef.USE) > 0 && !speedCropped)
            //{
            //    float frac = 1f / 3.0f;
            //    pm.rightmove *= frac;
            //    pm.upmove *= frac;
            //    pm.forwardmove *= frac;
            //    speedCropped = true;
            //}

            if (pm.ps.pm_type == PMType.SPECTATOR)
            {
                FlyMove2();
                DropTimers();
                return;
            }

            // Walk
            StartGravity();

            // Jump pressed
            if ((pm.cmd.buttons & (int)Input.ButtonDef.JUMP) > 0)
            {
                //pm.ps.pm_flags |= PMFlags.JUMP_HELD;
                Jump();
            }
            else
                //pm.ps.pm_flags &= ~PMFlags.JUMP_HELD;
                pm.ps.OldButtons &= ~(int)Input.ButtonDef.JUMP;

            // Fricion is handled before we add in any base velocity. That way, if we are on a conveyor, 
            //  we don't slow when standing still, relative to the conveyor.
            if (pml.groundPlane)
            {
                pm.ps.velocity[2] = 0.0f;
                Friction2();
            }

            // Make sure velocity is valid.
            CheckVelocity();

            if (pml.groundPlane)
                WalkMove2();
            else
                AirMove2();  // Take into account movement when in air.

            // Set final flags.
            CategorizePosition();

            CheckVelocity();

            // Add any remaining gravitational component.
            FinishGravity();

            // If we are on ground, no downward velocity.
            if (pml.groundPlane)
                pm.ps.velocity[2] = 0;

            CGame.SnapVector(pm.ps.velocity);
        }

        // Just make sure the velocity don't go absolutely bonkers
        void CheckVelocity()
        {
            for (int i = 0; i < 3; i++)    
            {
                if (float.IsNaN(pm.ps.velocity[i]))
                {
                    Common.Instance.WriteLine("PM  Got a NaN velocity {0}", i);
                    pm.ps.velocity[i] = 0;
                }
                if (float.IsNaN(pm.ps.origin[i]))
                {
                    Common.Instance.WriteLine("PM  Got a NaN origin {0}", i);
                    pm.ps.origin[i] = 0;
                }

                // Bound it
                if (pm.ps.velocity[i] > pm_maxvelocity)
                {
                    Common.Instance.WriteLine("PM Got crazy velocity");
                    pm.ps.velocity[i] = pm_maxvelocity;
                }
                if (pm.ps.velocity[i] < -pm_maxvelocity)
                {
                    Common.Instance.WriteLine("PM Got crazy velocity");
                    pm.ps.velocity[i] = -pm_maxvelocity;
                }
            }
        }

        trace_t TracePlayerBBox(Vector3 start, Vector3 end, int mask)
        {
            return ClipMap.Instance.Box_Trace(start, end, pm.ps.Ducked ? Common.playerDuckedMins : Common.playerMins, pm.ps.Ducked ? Common.playerDuckedMaxs : Common.playerMaxs, 0, mask);
        }

        void Duck()
        {
            pm.mins = Common.playerMins;
            pm.maxs = Common.playerMaxs;

            int buttonsChanged = pm.cmd.buttons ^ pm.ps.OldButtons;
            int buttonsPressed = buttonsChanged & pm.cmd.buttons;
            int buttonsReleased = buttonsChanged & pm.ps.OldButtons;

            if ((pm.cmd.buttons & (int)Input.ButtonDef.DUCK) > 0)
            {
                pm.ps.OldButtons |= (int)Input.ButtonDef.DUCK;
            }
            else
                pm.ps.OldButtons &= ~(int)Input.ButtonDef.DUCK;

            if ((pm.ps.pm_flags & PMFlags.DUCKED) > 0 && !speedCropped)
            {
                // Crop speed
                float frac = 0.5f;
                pm.upmove *= frac;
                pm.rightmove *= frac;
                pm.forwardmove *= frac;
                speedCropped = true;
            }

            // Holding duck, in process of ducking or fully ducked?
            if ((pm.cmd.buttons & (int)Input.ButtonDef.DUCK) > 0 || pm.ps.Ducking || (pm.ps.pm_flags & PMFlags.DUCKED) > 0)
            {
                if ((pm.cmd.buttons & (int)Input.ButtonDef.DUCK) > 0) // Holding duck
                {
                    //Common.Instance.Write("Holding duck...");
                    bool alreadyDucked = (pm.ps.pm_flags & PMFlags.DUCKED) > 0;

                    // Just pressed duck, and not fully ducked?
                    if ((buttonsPressed & (int)Input.ButtonDef.DUCK) > 0 && !alreadyDucked)
                    {
                        pm.ps.Ducking = true;
                        pm.ps.DuckTime = 1000;
                    }

                    float duckms = Math.Max(0f, 1000f - pm.ps.DuckTime);
                    float ducks = duckms / 1000f;


                    // doing a duck movement? (ie. not fully ducked?)
                    if (pm.ps.Ducking)
                    {
                        // Finish ducking immediately if duck time is over or not on ground
                        if (ducks > 0.4f || !pml.groundPlane || alreadyDucked)
                        {
                            //Common.Instance.WriteLine("Finish ducking!");
                            FinishDuck();
                        }
                        else
                        {
                            
                            // Calc parametric time
                            float duckFraction = SimpleSpline(ducks / 0.4f);
                            //Common.Instance.WriteLine("DuckFrac: " + duckFraction + " time:" + duckms);
                            SetDuckedEyeOffset(duckFraction);
                        }
                    }
                }
                else
                {
                    // Try to unduck unless automovement is not allowed
                    // NOTE: When not onground, you can always unduck
                    //if (pm.ps.allowAutoMovement || !pml.groundPlane)
                    {
                        if ((buttonsReleased & (int)Input.ButtonDef.DUCK) > 0 && (pm.ps.pm_flags & PMFlags.DUCKED) == PMFlags.DUCKED)
                        {
                            pm.ps.DuckTime = 1000;
                            pm.ps.Ducking = true; // or unducking
                        }
                    }

                    float duckms = Math.Max(0f, 1000f - pm.ps.DuckTime);
                    float ducks = duckms / 1000f;

                    if (CanUnduck())
                    {
                        if (pm.ps.Ducked || pm.ps.Ducking)  // or unducking
                        {
                            // Finish ducking immediately if duck time is over or not on ground
                            if (ducks > 0.2f || !pml.groundPlane)
                                FinishUnduck();
                            else
                            {
                                // Calc parametric time
                                float duckFraction = SimpleSpline(1.0f - (ducks / 0.2f));
                                SetDuckedEyeOffset(duckFraction);
                            }
                        }
                    }
                    else
                    {
                        // Still under something where we can't unduck, so make sure we reset this timer so
                        //  that we'll unduck once we exit the tunnel, etc.
                        pm.ps.DuckTime = 1000;
                    }
                }
            }

            //pm.ps.viewheight = Common.DEFAULT_VIEWHEIGHT;
        }

        void FinishUnduck()
        {
            Vector3 newOrg = pm.ps.origin;
            if (pml.groundPlane)
                newOrg += Common.playerDuckedMins - Common.playerMins;
            else
            {
                // If in air an letting go of croush, make sure we can offset origin to make
                //  up for uncrouching
                Vector3 hullSizeNormal = Common.playerMaxs - Common.playerMins;
                Vector3 hullSizeDucked = Common.playerDuckedMaxs - Common.playerDuckedMins;

                Vector3 viewDelta = -0.5f * (hullSizeNormal - hullSizeDucked);
                newOrg += viewDelta;
            }

            pm.ps.Ducked = false;
            pm.ps.Ducking = false;
            pm.ps.pm_flags &= ~PMFlags.DUCKED;
            pm.ps.viewheight = (int)Common.playerView.Z;

            pm.ps.origin = newOrg;

            // Recategorize position since ducking can change origin
            CategorizePosition();
        }

        bool CanUnduck()
        {
            Vector3 newOrg = pm.ps.origin;
            if (pml.groundPlane)
                newOrg += Common.playerDuckedMins - Common.playerMins;
            else
            {
                // If in air an letting go of croush, make sure we can offset origin to make
                //  up for uncrouching
                Vector3 hullSizeNormal = Common.playerMaxs - Common.playerMins;
                Vector3 hullSizeDucked = Common.playerDuckedMaxs - Common.playerDuckedMins;

                Vector3 viewDelta = -0.5f * (hullSizeNormal - hullSizeDucked);
                newOrg += viewDelta;
            }

            bool saveDucked = pm.ps.Ducked;
            pm.ps.Ducked = false;
            trace_t trace = TracePlayerBBox(newOrg, newOrg, (int)brushflags.MASK_PLAYERSOLID);
            pm.ps.Ducked = true;
            if (!trace.startsolid)
                return true;

            return false;
        }



        // FInish ducking
        void FinishDuck()
        {
            Vector3 hullSizeNormal = Common.playerMaxs - Common.playerMins;
            Vector3 hullSizeDucked = Common.playerDuckedMaxs - Common.playerDuckedMins;

            Vector3 viewDelta = -0.5f * (hullSizeNormal - hullSizeDucked);

            pm.ps.Ducked = true;
            pm.ps.pm_flags |= PMFlags.DUCKED;
            pm.ps.Ducking = false;
            pm.ps.viewheight = (int)Common.playerDuckedView.Z;

            // HACKHACK - Fudge for collision bug - no time to fix this properly
            if (!pml.groundPlane)
            {
                pm.ps.origin -= Common.playerDuckedMins - Common.playerMins;
            }
            else
            {
                pm.ps.origin += viewDelta;
            }

            CategorizePosition();
        }

        // hermite basis function for smooth interpolation
        // Similar to Gain() above, but very cheap to call
        // value should be between 0 & 1 inclusive
        float SimpleSpline(float val)
        {
            float valSq = val * val;

            // Nice little ease-in, ease-out spline-like curve
            return (3 * valSq - 2 * valSq * val);
        }

        void SetDuckedEyeOffset(float duckFraction)
        {
            float fmore = Common.playerDuckedMins.Z - Common.playerMins.Z;

            Vector3 duckedView = Common.playerDuckedView;
            Vector3 standingView = Common.playerView;
            
            pm.ps.viewheight = (int)(((Common.playerDuckedView.Z -fmore) * duckFraction) + (Common.playerView.Z * (1f - duckFraction)));
        }

        void Friction2()
        {
            float speed = pm.ps.velocity.Length();
            if (speed < 0.1f)
                return;

            float drop = 0;
            float friction = pm_friction;
            if (pml.groundPlane) // On an entity that is the ground
            {
                //
                // NOTE: added a "1.0f" to the player minimum (bbox) value so that the 
                //       trace starts just inside of the bounding box, this make sure
                //       that we don't get any collision epsilon (on surface) errors.
                //		 The significance of the 16 below is this is how many units out front we are checking
                //		 to see if the player box would fall.  The 49 is the number of units down that is required
                //		 to be considered a fall.  49 is derived from 1 (added 1 from above) + 48 the max fall 
                //		 distance a player can fall and still jump back up.
                //
                //		 UNDONE: In some cases there are still problems here.  Specifically, no collision check is
                //		 done so 16 units in front of the player could be inside a volume or past a collision point.
                Vector3 start = Vector3.Zero, stop = Vector3.Zero;
                start[0] = stop[0] = pm.ps.origin[0] + (pm.ps.velocity[0] / speed) * 16;
                start[1] = stop[1] = pm.ps.origin[1] + (pm.ps.velocity[1] / speed) * 16;
                start[2] = pm.ps.origin[2] + ((pm.ps.Ducked)?Common.playerDuckedMins[2]:Common.playerMins[2]) + 1f; // FIX FOR DUCK
                stop[2] = start[2] - 49;

                // This should *not* be a raytrace, it should be a box trace such that we can determine if the
                // player's box would fall off the ledge.  Ray tracing has problems associated with walking on rails
                // or on planks where a single ray would have the code believe the player is going to fall when in fact
                // they wouldn't.  The by product of this not working properly is that when a player gets to what
                // the code believes is an edge, the friction is bumped way up thus slowing the player down.
                // If not done properly, this kicks in way too often and forces big unintentional slowdowns.
                trace_t trace = TracePlayerBBox(start, stop, (int)brushflags.MASK_PLAYERSOLID);

                if (trace.fraction == 1.0f)
                    friction *= CVars.Instance.VariableValue("g_edgefriction");

                // Bleed off some speed, but if we have less than the bleed
                //  threshhold, bleed the theshold amount.
                float control = (speed < pm_stopspeed ? pm_stopspeed : speed);

                // Add the amount to the drop amount.
                drop += control * friction * pml.frametime;
            }

            // scale the velocity
            float newspeed = speed - drop;
            if (newspeed < 0)
                newspeed = 0;

            // Determine proportion of old speed we are using.
            newspeed /= speed;

            // Adjust velocity according to proportion.
            pm.ps.velocity *= newspeed;
        }

        void AirMove2()
        {
            //float scale = CommandScale(pm.cmd);
            float fmove = pm.forwardmove;
            float smove = pm.rightmove;

            pml.forward[2] = 0;
            pml.right[2] = 0;
            pml.forward.Normalize();
            pml.right.Normalize();

            Vector3 wishvel = Vector3.Zero;
            for (int i = 0; i < 2; i++)
            {
                wishvel[i] = (pml.forward[i] * fmove) + (pml.right[i] * smove);
            }
            wishvel[2] = 0;

            Vector3 wishdir = wishvel;
            wishdir.Normalize();
            float wishspeed = wishvel.Length();


            if (wishspeed > pm.ps.speed)
            {
                wishvel = wishvel * (pm.ps.speed / wishspeed);
                wishspeed = pm.ps.speed;
            }

            AirAccelerate(wishdir, wishspeed, pm_airaccelerate);

            FlyMove2();
        }

        void WalkMove2()
        {
            // Copy movement amounts
            float fmove = pm.forwardmove;
            float smove = pm.rightmove;
            //float scale = CommandScale(pm.cmd);

            // Zero out z components of movement vectors
            pml.forward[2] = 0;
            pml.right[2] = 0;

            pml.forward.Normalize();
            pml.right.Normalize();

            Vector3 wishvel = Vector3.Zero;
            for (int i = 0; i < 2; i++)
            {
                wishvel[i] = (pml.forward[i] * fmove) + (pml.right[i] * smove);
            }

            Vector3 wishdir = wishvel;
            wishdir.Normalize();
            float wishspeed = wishvel.Length();

            if (wishspeed > pm.ps.speed)
            {
                wishvel = wishvel * (pm.ps.speed / wishspeed);
                wishspeed = pm.ps.speed;
            }

            pm.ps.velocity[2] = 0;
            Accelerate(wishdir, wishspeed, pm_accelerate);
            pm.ps.velocity[2] = 0;

            float spd = pm.ps.velocity.Length();

            if (spd < 1.0f)
            {
                pm.ps.velocity = Vector3.Zero;
                return;
            }

            bool oldonground = pml.groundPlane;

            // first try just moving to the destination	
            Vector3 dest = new Vector3(pm.ps.origin.X + pm.ps.velocity.X*pml.frametime,
                                       pm.ps.origin.Y + pm.ps.velocity.Y*pml.frametime,
                                       pm.ps.origin.Z);

            // first try moving directly to the next spot
            Vector3 start = dest;
            trace_t trace = ClipMap.Instance.Box_Trace(pm.ps.origin, dest, pm.mins, pm.maxs, 0, pm.tracemask);
            // If we made it all the way, then copy trace end
            //  as new player position.
            if (trace.fraction == 1)
            {
                pm.ps.origin = trace.endpos;
                return;
            }

            // Don't walk up stairs if not on ground.
            if (!oldonground)
                return;

            Vector3 org = pm.ps.origin;
            Vector3 orgvel = pm.ps.velocity;

            // Slide move
            FlyMove2();

            // Copy the results out
            Vector3 down = pm.ps.origin;
            Vector3 downvel = pm.ps.velocity;

            // Reset original values.
            pm.ps.origin = org;
            pm.ps.velocity = orgvel;

            // Start out up one stair height
            dest = pm.ps.origin;
            dest[2] += 18;

            trace = ClipMap.Instance.Box_Trace(pm.ps.origin, dest, pm.mins, pm.maxs, 0, pm.tracemask);
            // If we started okay and made it part of the way at least,
            //  copy the results to the movement start position and then
            //  run another move try.
            if (!trace.allsolid && !trace.startsolid)
                pm.ps.origin = trace.endpos;

            // slide move the rest of the way.
            FlyMove2();

            // Now try going back down from the end point
            //  press down the stepheight
            dest = pm.ps.origin;
            dest[2] -= 18;

            trace = ClipMap.Instance.Box_Trace(pm.ps.origin, dest, pm.mins, pm.maxs, 0, pm.tracemask);

            // If we are not on the ground any more then
            //  use the original movement attempt
            bool usedown = false;
            if (trace.plane.normal[2] < 0.7f)
                usedown = true;
            float downdist = 0f;
            float updist = 0f;
            if (!usedown)
            {
                // If the trace ended up in empty space, copy the end
                //  over to the origin.
                if (!trace.startsolid && !trace.allsolid)
                    pm.ps.origin = trace.endpos;

                // Copy this origion to up.
                pml.up = pm.ps.origin;

                // decide which one went farther
                downdist = (down[0] - org[0]) * (down[0] - org[0]) +
                                 (down[1] - org[1]) * (down[1] - org[1]);
                updist = (pml.up[0] - org[0]) * (pml.up[0] - org[0]) +
                               (pml.up[1] - org[1]) * (pml.up[1] - org[1]);

            }


            if (downdist > updist || usedown)
            {
                pm.ps.origin = down;
                pm.ps.velocity = downvel;
            } else {
                pm.ps.velocity[2] = downvel[2];
            }
        }

        

        int FlyMove2()
        {
            int numbumps = 4;
            int blocked = 0;
            int numplanes = 0;
            Vector3 org_vel = pm.ps.velocity;
            Vector3 pri_vel = pm.ps.velocity;
            Vector3[] planes = new Vector3[5];
            Vector3 new_velocity = Vector3.Zero;

            float allFraction = 0f;
            float time_left = pml.frametime;

            int bumpcount, i, j;
            Vector3 end = Vector3.Zero;
            
            for (bumpcount = 0; bumpcount < numbumps; bumpcount++)
            {
                if (pm.ps.velocity == Vector3.Zero)
                    break;

                // Assume we can move all the way from the current origin to the
                //  end point.
                for (i = 0; i < 3; i++)
                    end[i] = pm.ps.origin[i] + time_left * pm.ps.velocity[i];

                // See if we can make it from origin to end point.
                trace_t trace = ClipMap.Instance.Box_Trace(pm.ps.origin, end, pm.mins, pm.maxs, 0, pm.tracemask);

                allFraction += trace.fraction;
                // If we started in a solid object, or we were in solid space
                //  the whole way, zero out our velocity and return that we
                //  are blocked by floor and wall.
                if (trace.allsolid)
                {
                    // entity is trapped in another solid
                    pm.ps.velocity = Vector3.Zero;
                    return 4;
                }

                // If we moved some portion of the total distance, then
                //  copy the end position into the pmove->origin and 
                //  zero the plane counter.
                if (trace.fraction > 0f)
                {
                    // actually covered some distance
                    pm.ps.origin = trace.endpos;
                    org_vel = pm.ps.velocity;
                    numplanes = 0;
                }

                // If we covered the entire distance, we are done
                //  and can return.
                if (trace.fraction == 1.0f)
                    break;  // moved the entire distance

                // If the plane we hit has a high z component in the normal, then
                //  it's probably a floor
                if (trace.plane.normal[2] > 0.7f)
                {
                    blocked |= 1; // floor
                }
                // If the plane has a zero z component in the normal, then it's a 
                //  step or wall
                if (trace.plane.normal[2] == 0.0f)
                    blocked |= 2; // step/wall

                // Reduce amount of pmove->frametime left by total time left * fraction
                //  that we covered.
                time_left -= time_left * trace.fraction;

                // Did we run out of planes to clip against?
                if (numplanes >= 5)
                {
                    // this shouldn't really happen
                    //  Stop our movement if so.
                    pm.ps.velocity = Vector3.Zero;
                    break;
                }

                // Set up next clipping plane
                planes[numplanes] = trace.plane.normal;
                numplanes++;

                // modify original_velocity so it parallels all of the clip planes
                //
                if (!pml.groundPlane)
                {
                    for (i = 0; i < numplanes; i++)
                    {
                        if (planes[i][2] > 0.7f)
                        {
                            // floor or slope
                            ClipVelocity(org_vel, planes[i], ref new_velocity, 1.001f);
                            org_vel = new_velocity;
                        }
                        else
                            ClipVelocity(org_vel, planes[i], ref new_velocity, 1.001f);
                    }

                    pm.ps.velocity = new_velocity;
                    org_vel = new_velocity;
                }
                else
                {
                    for (i = 0; i < numplanes; i++)
                    {
                        ClipVelocity(org_vel, planes[i], ref pm.ps.velocity, 1.001f);
                        for (j = 0; j < numplanes; j++)
                        {
                            if (j != i)
                            {
                                // Are we now moving against this plane?
                                if (Vector3.Dot(pm.ps.velocity, planes[j]) < 0f)
                                    break; // not ok
                            }
                        }
                        if (j == numplanes) // Didn't have to clip, so we're ok
                            break;
                    }

                    // Did we go all the way through plane set
                    if (i != numplanes)
                    {
                        // go along this plane
                        // pmove->velocity is set in clipping call, no need to set again.
                    }
                    else
                    {
                        // go along the crease
                        if (numplanes != 2)
                        {
                            pm.ps.velocity = Vector3.Zero;
                            break;
                        }
                        Vector3 dir = Vector3.Cross(planes[0], planes[1]);
                        float d = Vector3.Dot(dir, pm.ps.velocity);
                        pm.ps.velocity = dir * d;
                    }

                    //
                    // if original velocity is against the original velocity, stop dead
                    // to avoid tiny occilations in sloping corners
                    //

                    if (Vector3.Dot(pm.ps.velocity, pri_vel) <= 0f)
                    {
                        pm.ps.velocity = Vector3.Zero;
                        break;
                    }
                }
            }

            if (allFraction == 0f)
                pm.ps.velocity = Vector3.Zero;

            return blocked;

        }

        void Jump()
        {

            //bool tfc = false;
            //bool cansuperjump = false;

            if (pm.ps.stats[0] <= 0)
            {
                pm.ps.OldButtons |= (int)Input.ButtonDef.JUMP;
                return;
            }

            if (pm_maxspeed == 1)
                return;

            //if (pm.cmd.upmove < 10)
            //{
            //    // not holding jump
            //    return;
            //}

            pm.ps.pm_flags |= PMFlags.JUMP_HELD;

            if (!pml.groundPlane)
            {
                pm.ps.OldButtons |= (int)Input.ButtonDef.JUMP; // wait for release
                return; // in air, so no effect
            }

            if ((pm.ps.OldButtons & (int)Input.ButtonDef.JUMP) > 0)
            {
                return; // don't pogo-stick
            }

            // In the air now.
            pml.groundPlane = false;
            pml.walking = false;

            PreventMegaBunnyJumping();

            // Acclerate upward
            pm.ps.velocity[2] += 295;

            // Decay it for simulation
            FinishGravity();

            pm.ps.OldButtons |= (int)Input.ButtonDef.JUMP;
        }

        //-----------------------------------------------------------------------------
        // Purpose: Corrects bunny jumping ( where player initiates a bunny jump before other
        //  movement logic runs, thus making onground == -1 thus making PM_Friction get skipped and
        //  running PM_AirMove, which doesn't crop velocity to maxspeed like the ground / other
        //  movement logic does.
        //-----------------------------------------------------------------------------
        void PreventMegaBunnyJumping()
        {
            // How fast do we allow?
            float maxscaledspeed = BUNNYJUMP_MAX_SPEED_FACTOR * pm_maxspeed;
            if (maxscaledspeed <= 0.0f)
                return;
        
            // current speed
            float speed = pm.ps.velocity.Length();

            // Speed is slower than max
            if (speed <= maxscaledspeed)
                return;

            // We need to limit the speed :(
            float frac = (maxscaledspeed / speed) * 0.65f;
            pm.ps.velocity *= frac; // crop it down
        }

        void StartGravity()
        {
            float ent_gravity;
            if (pm.ps.gravity > 0)
                ent_gravity = pm.ps.gravity;
            else
                ent_gravity = 1f;

            // Add gravity so they'll be in the correct position during movement
            // yes, this 0.5 looks wrong, but it's not.  
            pm.ps.velocity[2] -= (ent_gravity  * 0.5f * pml.frametime);

            CheckVelocity();
        }

        void FinishGravity()
        {
            float ent_gravity;
            if (pm.ps.gravity > 0)
                ent_gravity = pm.ps.gravity;
            else
                ent_gravity = 1f;

            pm.ps.velocity[2] -= (ent_gravity * pml.frametime * 0.5f);

            CheckVelocity();
        }

        void CategorizePosition()
        {
            // if the player hull point one unit down is solid, the player
            // is on ground

            // see if standing on something solid	

            // Doing this before we move may introduce a potential latency in water detection, but
            // doing it after can get us stuck on the bottom in water if the amount we move up
            // is less than the 1 pixel 'threshold' we're about to snap to.	Also, we'll call
            // this several times per frame, so we really need to avoid sticking to the bottom of
            // water on each call, and the converse case will correct itself if called twice.

            Vector3 point = pm.ps.origin;
            point[2] -= 2;
            if (pm.ps.velocity[2] > 180)  // Shooting up really fast.  Definitely not on ground.
                pml.groundPlane = false;
            else
            {
                // Try and move down.
                trace_t tr = ClipMap.Instance.Box_Trace(pm.ps.origin, point, pm.mins, pm.maxs, 0, pm.tracemask);
                // If we hit a steep plane, we are not on ground
                if (tr.plane.normal[2] < 0.7f)
                    pml.groundPlane = false;    // too steep
                else
                {
                    pml.groundPlane = true;
                    pml.groundTrace = tr;   // Otherwise, point to index of ent under us.
                }

                // If we are on something...
                if (pml.groundPlane)
                {
                    // If we could make the move, drop us down that 1 pixel
                    if (!tr.startsolid && !tr.allsolid)
                        pm.ps.origin = tr.endpos;
                }
            }
        }

        float CheckParameters()
        {
            float speed = (pm.forwardmove * pm.forwardmove) +
                          (pm.rightmove * pm.rightmove) +
                          (pm.upmove * pm.upmove);
            speed = (float)Math.Sqrt(speed);

            float maxspeed = pm.ps.speed;
            if (maxspeed != 0f)
                pm.maxSpeed = Math.Min(maxspeed, pm.maxSpeed);

            float ratio = 1.0f;
            if ((speed != 0.0f) && (speed > maxspeed))
            {
                ratio = maxspeed / speed;
                pm.forwardmove *= ratio;
                pm.rightmove *= ratio;
                pm.upmove *= ratio;
            }
            //else
            //{
            //    pm.forwardmove = (pm.cmd.forwardmove) * 6;
            //    pm.rightmove = (pm.cmd.rightmove) * 6;
            //    pm.upmove = (pm.cmd.upmove) * 6;
            //}

            //if (!speedCropped && (pm.cmd.buttons & (int)Input.ButtonDef.WALK) > 0 && (pm.cmd.buttons & (int)Input.ButtonDef.DUCK) == 0)
            //{
            //    float frac = 0.47f;

            //    pm.rightmove *= frac;
            //    pm.upmove *= frac;
            //    pm.forwardmove *= frac;
            //    speedCropped = true;
            //}

            // dead
            if (pm.ps.stats[0] <= 0)
            {
                pm.forwardmove = pm.rightmove = pm.upmove = 0;
                pm.ps.viewheight = (int)Common.playerDeadView[2];
            }

            // Adjust client view angles to match values used on server.
            if (pm.ps.viewangles[1] > 180f)
                pm.ps.viewangles[1] -= 360f;

            return ratio;
        }

        Vector3 DropPunchAngle(Vector3 punchangle)
        {
            float len = punchangle.Length();
            punchangle.Normalize();
            len -= (10f + len * 0.5f) * pml.frametime;
            len = Math.Max(len, 0.0f);
            return punchangle * len;
        }

        //void PmoveSingle(pmove_t pmove)
        //{
        //    pm = pmove;
        //    // this counter lets us debug movement problems with a journal
        //    // by setting a conditional breakpoint fot the previous frame
        //    c_pmove++;
            
        //    // clear results
        //    pm.numtouch = 0;

        //    if (pm.ps.stats[0] <= 0)
        //    {
        //        pm.tracemask &= ~0x2000000;
        //    }

        //    // make sure walking button is clear if they are running, to avoid
        //    // proxy no-footsteps cheats
        //    if (Math.Abs(pm.cmd.forwardmove) > 64 || Math.Abs(pm.cmd.rightmove) > 64)
        //    {
        //        pm.cmd.buttons &= ~16;
        //    }

        //    //// set the firing flag for continuous beam weapons
        //    //if ((pm.ps.pm_flags & PMFlags.RESPAWNED) != PMFlags.RESPAWNED && pm.ps.pm_type != PMType.INTERMISSION
        //    //    && (pm.cmd.buttons & 1) == 1 && pm.ps.ammo[pm.ps.weapon] > 0)
        //    //{
        //    //    pm.ps.eFlags |= EntityFlags.EF_FIRING;
        //    //}
        //    //else
        //    //    pm.ps.eFlags &= ~EntityFlags.EF_FIRING;

        //    // clear the respawned flag if attack and use are cleared
        //    if(pm.ps.stats[0] > 0 && (pm.cmd.buttons & (1|4)) == 0)
        //        pm.ps.pm_flags &= ~PMFlags.RESPAWNED;

        //    // clear all pmove local vars
        //    pml = new pml_t();
        //    pml.forward = Vector3.Zero;
        //    pml.up = Vector3.Zero;
        //    pml.right = Vector3.Zero;

        //    // determine the time
        //    pml.msec = pm.cmd.serverTime - pm.ps.commandTime;
        //    if (pml.msec < 1)
        //        pml.msec = 1;
        //    else if (pml.msec > 200)
        //        pml.msec = 200;

        //    pm.ps.commandTime = pm.cmd.serverTime;

        //    // save old org in case we get stuck
        //    pml.previous_origin = pm.ps.origin;

        //    // save old velocity for crashlanding
        //    pml.previous_velocity = pm.ps.velocity;

        //    pml.frametime = pml.msec * 0.001f;

        //    // update the viewangles
        //    UpdateViewAngles(ref pm.ps, pm.cmd);

        //    AngleVectors(pm.ps.viewangles, ref pml.forward, ref pml.right, ref pml.up);

        //    if (pm.cmd.upmove < 10)
        //        // not holding jump
        //        pm.ps.pm_flags &= ~PMFlags.JUMP_HELD;

        //    // decide if backpedaling animations should be used
        //    if (pm.cmd.forwardmove < 0)
        //        pm.ps.pm_flags |= PMFlags.BACKWARDS_RUN;
        //    else if (pm.cmd.forwardmove > 0 || (pm.cmd.forwardmove == 0 && pm.cmd.rightmove > 0))
        //        pm.ps.pm_flags &= ~PMFlags.BACKWARDS_RUN;

        //    if (pm.ps.pm_type == PMType.DEAD)
        //    {
        //        pm.cmd.forwardmove = 0;
        //        pm.cmd.rightmove = 0;
        //        pm.cmd.upmove = 0;
        //    }

        //    if (pm.ps.pm_type == PMType.SPECTATOR)
        //    {
        //        CheckDuck();
        //        FlyMove();
        //        DropTimers();
        //        return;
        //    }

        //    if (pm.ps.pm_type == PMType.NOCLIP)
        //    {
        //        //NoclipMove();
        //        DropTimers();
        //        return;
        //    }

        //    if (pm.ps.pm_type == PMType.FREEZE)
        //        return; // no movement at all

        //    if (pm.ps.pm_type == PMType.INTERMISSION || pm.ps.pm_type == PMType.SPINTERMISSION)
        //        return; // no movement at all

        //    // set mins, maxs, and viewheight
        //    CheckDuck();

        //    // set groundentity
        //    GroundTrace();

        //    if (pm.ps.pm_type == PMType.DEAD)
        //    {
        //        //DeadMove();
        //    }

        //    DropTimers();

        //    if (pml.walking)
        //    {
        //        // walking on ground
        //        WalkMove();
        //    }
        //    else
        //    {
        //        // airborne
        //        AirMove();
        //    }

        //    // set groundentity, watertype, and waterlevel
        //    GroundTrace();

        //    // snap some parts of playerstate to save network bandwidth
        //    CGame.SnapVector(pm.ps.velocity);
        //}

        //void CheckDuck()
        //{
        //    pm.mins[0] = -15;
        //    pm.mins[1] = -15;

        //    pm.maxs[0] = 15;
        //    pm.maxs[1] = 15;

        //    pm.mins[2] = -24;

        //    if (pm.ps.pm_type == PMType.DEAD)
        //    {
        //        pm.maxs[2] = -8;
        //        pm.ps.viewheight = -16;
        //        return;
        //    }

        //    if (pm.cmd.upmove < 0)
        //    {
        //        pm.ps.pm_flags |= PMFlags.DUCKED;
        //    }
        //    else
        //    {
        //        // stand up if possible
        //        if ((pm.ps.pm_flags & PMFlags.DUCKED) == PMFlags.DUCKED)
        //        {
        //            // try to stand up
        //            pm.maxs[2] = 32;
        //            trace_t trace = pm.DoTrace(pm.ps.origin, pm.ps.origin, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
        //            if (!trace.allsolid)
        //                pm.ps.pm_flags &= ~PMFlags.DUCKED;
        //        }
        //    }

        //    if ((pm.ps.pm_flags & PMFlags.DUCKED) == PMFlags.DUCKED)
        //    {
        //        pm.maxs[2] = 16;
        //        pm.ps.viewheight = Common.CROUCH_VIEWHEIGHT;
        //    }
        //    else
        //    {
        //        pm.maxs[2] = 32;
        //        pm.ps.viewheight = Common.DEFAULT_VIEWHEIGHT;
        //    }
        //}

        //bool CheckJump()
        //{
        //    if ((pm.ps.pm_flags & PMFlags.RESPAWNED) == PMFlags.RESPAWNED)
        //    {
        //        return false;   // don't allow jump until all buttons are up
        //    }

        //    if (pm.cmd.upmove < 10)
        //    {
        //        // not holding jump
        //        return false;
        //    }

        //    // must wait for jump to be released
        //    if ((pm.ps.pm_flags & PMFlags.JUMP_HELD) == PMFlags.JUMP_HELD)
        //    {
        //        // clear upmove so cmdscale doesn't lower running speed
        //        pm.cmd.upmove = 0;
        //        return false;
        //    }

        //    pml.groundPlane = false;    // jumping away
        //    pml.walking = false;
        //    pm.ps.pm_flags |= PMFlags.JUMP_HELD;
        //    pm.ps.groundEntityNum = 1023;
        //    pm.ps.velocity[2] = 270;

        //    if (pm.cmd.forwardmove >= 0)
        //    {
        //        pm.ps.pm_flags &= ~PMFlags.BACKWARDS_JUMP;
        //    }
        //    else
        //    {
        //        pm.ps.pm_flags |= PMFlags.BACKWARDS_JUMP;
        //    }

        //    return true;
        //}

        //void AirMove()
        //{
        //    Friction();

        //    float fmove = pm.cmd.forwardmove;
        //    float smove = pm.cmd.rightmove;

        //    Input.UserCommand cmd = pm.cmd;
        //    float scale = CommandScale(cmd);

        //    // project moves down to flat plane
        //    pml.forward[2] = 0;
        //    pml.right[2] = 0;
        //    pml.forward.Normalize();
        //    pml.right.Normalize();

        //    Vector3 wishvel = (pml.forward * fmove) + (pml.right * smove);
        //    wishvel[2] = 0;

        //    Vector3 wishdir = wishvel;
        //    float wishspeed = wishdir.Length();
        //    wishdir.Normalize();
        //    wishspeed *= scale;

        //    // not on ground, so little effect on velocity
        //    AirAccelerate(wishdir, wishspeed, pm_airaccelerate);

        //    // we may have a ground plane that is very steep, even
        //    // though we don't have a groundentity
        //    // slide along the steep plane
        //    if (pml.groundPlane)
        //    {
        //        ClipVelocity(pm.ps.velocity, pml.groundTrace.plane.normal, ref pm.ps.velocity, 1.001f);
        //    }

        //    StepSlideMove(true);
        //}

        


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
            if (pm.ps.DuckTime > 0f)
            {
                pm.ps.DuckTime -= pml.msec;
                if (pm.ps.DuckTime < 0)
                    pm.ps.DuckTime = 0;
            }
        }

        //void WalkMove()
        //{
        //    if (CheckJump())
        //    {
        //        // jumped away
        //        AirMove();
        //        return;
        //    }

        //    Friction();

        //    float fmove = pm.cmd.forwardmove;
        //    float smove = pm.cmd.rightmove;
        //    Input.UserCommand cmd = pm.cmd;
        //    float scale = CommandScale(cmd);

        //    // set the movementDir so clients can rotate the legs for strafing
        //    //SetMovementDir();

        //    // project moves down to flat plane
        //    pml.forward[2] = 0;
        //    pml.right[2] = 0;

        //    // project the forward and right directions onto the ground plane
        //    ClipVelocity(pml.forward, pml.groundTrace.plane.normal, ref pml.forward, 1.001f);
        //    ClipVelocity(pml.right, pml.groundTrace.plane.normal, ref pml.right, 1.001f);
        //    pml.forward.Normalize();
        //    pml.right.Normalize();

        //    Vector3 wishvel = (pml.forward * fmove) + (pml.right * smove);
        //    Vector3 wishdir = wishvel;
        //    float wishpeed = wishdir.Length();
        //    wishdir.Normalize();
        //    wishpeed *= scale;

        //    // clamp the speed lower if ducking
        //    if ((pm.ps.pm_flags & PMFlags.DUCKED) == PMFlags.DUCKED)
        //    {
        //        if (wishpeed > pm.ps.speed * pm_duckScale)
        //        {
        //            wishpeed = pm.ps.speed * pm_duckScale;
        //        }
        //    }

        //    // when a player gets hit, they temporarily lose
        //    // full control, which allows them to be moved a bit
        //    float accelerate;
        //    if ((pml.groundTrace.surfaceFlags & (int)SurfFlags.SURF_SLICK) > 0 || (pm.ps.pm_flags & PMFlags.TIME_KNOCKBACK) == PMFlags.TIME_KNOCKBACK)
        //    {
        //        accelerate = pm_airaccelerate;
        //    }
        //    else
        //        accelerate = pm_accelerate;

        //    Accelerate(wishdir, wishpeed, accelerate);

        //    if ((pml.groundTrace.surfaceFlags & (int)SurfFlags.SURF_SLICK) > 0 || (pm.ps.pm_flags & PMFlags.TIME_KNOCKBACK) == PMFlags.TIME_KNOCKBACK)
        //    {
        //        pm.ps.velocity[2] -= pm.ps.gravity * pml.frametime;
        //    }

        //    float vel = pm.ps.velocity.Length();
        //    // slide along the ground plane
        //    ClipVelocity(pm.ps.velocity, pml.groundTrace.plane.normal, ref pm.ps.velocity, 1.001f);

        //    // don't decrease velocity when going up or down a slope
        //    pm.ps.velocity.Normalize();
        //    pm.ps.velocity = Vector3.Multiply(pm.ps.velocity, vel);

        //    // don't do anything if standing still
        //    if (pm.ps.velocity[0] == 0 && pm.ps.velocity[1] == 0)
        //        return;

        //    StepSlideMove(false);

        //    if (pm.ps.groundEntityNum != 1023)
        //        pm.ps.velocity[2] = 0;
        //}

        //void GroundTrace()
        //{
        //    Vector3 point = pm.ps.origin;
        //    point[2] -= 0.25f;
        //    trace_t trace = pm.DoTrace(pm.ps.origin, point, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
        //    pml.groundTrace = trace;

        //    // do something corrective if the trace starts in a solid...
        //    if (trace.allsolid)
        //    {
        //        if (!CorrectAllSolid(trace))
        //            return;
        //    }

        //    // if the trace didn't hit anything, we are in free fall
        //    if (trace.fraction == 1.0f)
        //    {
        //        GroundTraceMissed();
        //        pml.groundPlane = false;
        //        pml.walking = false;
        //        return;
        //    }

        //    // check if getting thrown off the ground
        //    if (pm.ps.velocity[2] > 0 && Vector3.Dot(pm.ps.velocity, trace.plane.normal) > 10f)
        //    {
        //        //Common.Instance.WriteLine("kickoff");
        //        pm.ps.groundEntityNum = 1023;
        //        pml.groundPlane = false;
        //        pml.walking = false;
        //        return;
        //    }

        //    // slopes that are too steep will not be considered onground
        //    if (trace.plane.normal[2] < 0.7f)
        //    {
        //        //Common.Instance.WriteLine("steep");
        //        // FIXME: if they can't slide down the slope, let them
        //        // walk (sharp crevices)
        //        pm.ps.groundEntityNum = 1023;
        //        pml.groundPlane = true;
        //        pml.walking = false;
        //        return;
        //    }

        //    pml.groundPlane = true;
        //    pml.walking = true;

        //    if (pm.ps.groundEntityNum == 1023)
        //    {
        //        //Common.Instance.WriteLine("land");
        //        // just hit the ground
        //        //CrashLand();

        //        // don't do landing time if we were just going down a slope
        //        if (pml.previous_velocity[2] < -200)
        //        {
        //            // don't allow another jump for a little while
        //            pm.ps.pm_flags |= PMFlags.TIME_LAND;
        //            pm.ps.pm_time = 250;
        //        }
        //    }

        //    pm.ps.groundEntityNum = trace.entityNum;
        //    //AddTouchEnt(trace.entityNum);
        //}

        ///*
        //=============
        //PM_GroundTraceMissed

        //The ground trace didn't hit a surface, so we are in freefall
        //=============
        //*/
        //void GroundTraceMissed()
        //{
        //    if (pm.ps.groundEntityNum != 1023)
        //    {
        //        // we just transitioned into freefall

        //        // if they aren't in a jumping animation and the ground is a ways away, force into it
        //        // if we didn't do the trace, the player would be backflipping down staircases
        //        Vector3 point = pm.ps.origin;
        //        point[2] -= 0.25f;

        //        trace_t trace = pm.DoTrace(pm.ps.origin, point, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
        //        if (trace.fraction == 1.0f)
        //        {
        //            if(pm.cmd.forwardmove >= 0)
        //                pm.ps.pm_flags &= ~PMFlags.BACKWARDS_JUMP;
        //            else
        //            {
        //                pm.ps.pm_flags |= PMFlags.BACKWARDS_JUMP;
        //            }
        //        }
                
        //    }

        //    pm.ps.groundEntityNum = 1023;
        //    pml.groundPlane = false;
        //    pml.walking = false;
        //}

        //bool CorrectAllSolid(trace_t trace)
        //{
        //    Vector3 point;

        //    // jitter around
        //    for (int i = -1; i <= 1; i++)
        //    {
        //        for (int j = -1; j <= 1; j++)
        //        {
        //            for (int k = -1; k <= 1; k++)
        //            {
        //                point = pm.ps.origin;
        //                point[0] += (float)i;
        //                point[1] += (float)j;
        //                point[2] += (float)k;
        //                trace = pm.DoTrace(point, point, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
        //                if (!trace.allsolid)
        //                {
        //                    point = pm.ps.origin;
        //                    point[2] -= 0.25f;

        //                    trace = pm.DoTrace(pm.ps.origin, point, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
        //                    pml.groundTrace = trace;
        //                    return true;
        //                }
        //            }
        //        }
        //    }

        //    pm.ps.groundEntityNum = 1023;
        //    pml.groundPlane = false;
        //    pml.walking = false;

        //    return false;
        //}

        //void FlyMove()
        //{
        //    // normal slowdown
        //    Friction();
        //    float scale = CommandScale(pm.cmd);

        //    //
        //    // user intentions
        //    //
        //    Vector3 wishvel = Vector3.Zero;
        //    if (scale != 0f)
        //    {
        //        for (int i = 0; i < 3; i++)
        //        {
        //            wishvel[i] = (scale * pml.forward[i] * pm.cmd.forwardmove) + (scale * pml.right[i] * pm.cmd.rightmove);
        //        }
        //        wishvel[2] += scale * pm.cmd.upmove;
        //    }

        //    Vector3 wishdir = new Vector3(wishvel.X, wishvel.Y, wishvel.Z);
        //    float wishspeed = wishdir.Length();
        //    wishdir.Normalize();

        //    // Cap speed
        //    if (wishspeed > pm_maxspeed)
        //    {
        //        wishspeed = pm_maxspeed;
        //    }

        //    // Apply acceleration
        //    Accelerate(wishdir, wishspeed, pm_accelerate);

        //    //SlideMove(false);
        //    StepSlideMove(false);
        //}

        private void AirAccelerate(Vector3 wishdir, float wishspeed, float accel)
        {
            // Cap speed
            if (wishspeed > 30)
                wishspeed = 30;

            // Continue with normal accelerate
            Accelerate(wishdir, wishspeed, accel);
        }

        // q2 style
        private void Accelerate(Vector3 wishdir, float wishspeed, float accel)
        {
            // Determine veer amount
            float currentspeed = Vector3.Dot(pm.ps.velocity, wishdir);
            //float currentspeed = pm.ps.velocity.Length();
            // See how much to add
            float addspeed = wishspeed - currentspeed;

            // If not adding any, done.
            if (addspeed <= 0f)
                return;

            // Determine acceleration speed after acceleration
            float accelspeed = accel * pml.frametime * wishspeed;

            // Cap it
            if (accelspeed > addspeed)
                accelspeed = addspeed;

            // Adjust pmove vel.
            pm.ps.velocity += accelspeed * wishdir;
            
        }

        //private void StepSlideMove(bool gravity)
        //{
        //    Vector3 start_o = pm.ps.origin;
        //    Vector3 start_v = pm.ps.velocity;

        //    if (!SlideMove(gravity))
        //    {
        //        return; // we got exactly where we wanted to go first try	
        //    }
            
        //    Vector3 down = start_o;
        //    down[2] -= 18;
        //    trace_t trace = pm.DoTrace(start_o, down, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
        //    Vector3 up = new Vector3(0f, 0f, 1f);
        //    // never step up when you still have up velocity
        //    float dotUp = Vector3.Dot(trace.plane.normal, up);
        //    if (pm.ps.velocity[2] > 0f && (trace.fraction == 1.0f || dotUp < 0.7f))
        //    {
        //        return;
        //    }

        //    Vector3 down_o = pm.ps.origin;
        //    Vector3 down_v = pm.ps.velocity;
        //    up = start_o;
        //    up[2] += 18;

        //    // test the player position if they were a stepheight higher
        //    trace = pm.DoTrace(start_o, up, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
        //    if (trace.allsolid)
        //    {
        //        return; // can't step up
        //    }

        //    float stepSize = trace.endpos[2] - start_o[2];
            
        //    // try slidemove from this position
        //    pm.ps.origin = trace.endpos;
        //    pm.ps.velocity = start_v;

        //    SlideMove(gravity);
        //    // push down the final amount
        //    down = pm.ps.origin;
        //    down[2] -= 18;
        //    trace = pm.DoTrace(pm.ps.origin, down, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
        //    if (!trace.allsolid)
        //        pm.ps.origin = trace.endpos;
        //    if (trace.fraction < 1.0f)
        //        ClipVelocity(pm.ps.velocity, trace.plane.normal, ref pm.ps.velocity, 1.001f);

        //    float delta = pm.ps.origin[2] - start_o[2];
        //    if (delta > 2)
        //    {
        //        if (delta < 7)
        //        {
        //            //PM_AddEvent(EV_STEP_4);
        //        }
        //        else if (delta < 11)
        //        {
        //            //PM_AddEvent(EV_STEP_8);
        //        }
        //        else if (delta < 15)
        //        {
        //            //PM_AddEvent(EV_STEP_12);
        //        }
        //        else
        //        {
        //            //PM_AddEvent(EV_STEP_16);
        //        }
        //    }
        //   //Common.Instance.WriteLine("stepped");

        //}

        ///*
        //==================
        //PM_SlideMove

        //Returns qtrue if the velocity was clipped in some way
        //==================
        //*/
        //private bool SlideMove(bool gravity)
        //{
        //    int numbumps = 4;

        //    Vector3 primal_velocity = pm.ps.velocity;
        //    Vector3 endVelocity = Vector3.Zero;
        //    if (gravity)
        //    {
        //        endVelocity = pm.ps.velocity;
        //        endVelocity[2] -= 800f * pml.frametime;
        //        pm.ps.velocity[2] = (pm.ps.velocity[2] + endVelocity[2]) * 0.5f;
        //        primal_velocity[2] = endVelocity[2];
        //        if (pml.groundPlane)
        //        {
        //            // slide along the ground plane
        //            ClipVelocity(pm.ps.velocity, pml.groundTrace.plane.normal, ref pm.ps.velocity, 1.001f);
        //        }
        //    }

        //    float time_left = pml.frametime;

        //    // never turn against the ground plane
        //    int numplanes;
        //    Vector3[] planes = new Vector3[5];
        //    if (pml.groundPlane)
        //    {
        //        numplanes = 1;
        //        planes[0] = pml.groundTrace.plane.normal;
        //    }
        //    else
        //        numplanes = 0;

        //    // never turn against original velocity
        //    planes[numplanes] = Vector3.Zero;
        //    VectorNormalize2(pm.ps.velocity, ref planes[numplanes]);
        //    numplanes++;
        //    int bumpcount;
        //    for (bumpcount = 0; bumpcount < numbumps; bumpcount++)
        //    {
        //        // calculate position we are trying to move to
        //        Vector3 end = ViewParams.VectorMA(pm.ps.origin, time_left, pm.ps.velocity);

        //        // see if we can make it there
        //        trace_t trace = pm.DoTrace(pm.ps.origin, end, pm.mins, pm.maxs, pm.ps.clientNum, pm.tracemask);
                
        //        if (trace.allsolid)
        //        {
        //            // entity is completely trapped in another solid
        //            pm.ps.velocity[2] = 0;  // don't build up falling damage, but allow sideways acceleration
        //            return true;
        //        }

        //        if (trace.fraction > 0f)
        //        {
        //            // actually covered some distance
        //            Vector3 save = trace.endpos;
        //            pm.ps.origin = save;
        //        }

        //        if (trace.fraction == 1.0f)
        //            break;  // moved the entire distance

        //        //lastTrace = trace;

        //        // save entity for contact
        //        //AddTouchEnt(trace.entityNum);

        //        time_left -= time_left * trace.fraction;

        //        if (numplanes >= 5)
        //        {
        //            // this shouldn't really happen
        //            pm.ps.velocity = Vector3.Zero;
        //            return true;
        //        }

        //        //
        //        // if this is the same plane we hit before, nudge velocity
        //        // out along it, which fixes some epsilon issues with
        //        // non-axial planes
        //        //
        //        int i;
        //        for (i = 0; i < numplanes; i++)
        //        {
        //            if (Vector3.Dot(trace.plane.normal, planes[i]) > 0.99f)
        //            {
        //                pm.ps.velocity = trace.plane.normal + pm.ps.velocity;
        //                break;
        //            }
        //        }

        //        if (i < numplanes)
        //            continue;

        //        planes[numplanes] = trace.plane.normal;
        //        numplanes++;
                
        //        //
        //        // modify velocity so it parallels all of the clip planes
        //        //
        //        // find a plane that it enters
        //        for (i = 0; i < numplanes; i++)
        //        {
        //            float into = Vector3.Dot(pm.ps.velocity, planes[i]);
        //            if (into >= 0.1f)
        //                continue;   // move doesn't interact with the plane

        //            // see how hard we are hitting things
        //            if (-into > pml.impactSpeed)
        //                pml.impactSpeed = -into;

        //            // slide along the plane
        //            Vector3 clipVelocity = Vector3.Zero;
        //            ClipVelocity(pm.ps.velocity, planes[i], ref clipVelocity, 1.001f);

        //            // slide along the plane
        //            Vector3 endClipVelocity = Vector3.Zero;
        //            ClipVelocity(endVelocity, planes[i], ref endClipVelocity, 1.001f);

        //            // see if there is a second plane that the new move enters
        //            for (int j = 0; j < numplanes; j++)
        //            {
        //                if (j == i)
        //                    continue;

        //                if (Vector3.Dot(clipVelocity, planes[j]) >= 0.1f)
        //                    continue;   // move doesn't interact with the plane

        //                // try clipping the move to the plane
        //                ClipVelocity(clipVelocity, planes[j], ref clipVelocity, 1.001f);
        //                ClipVelocity(endClipVelocity, planes[j], ref endClipVelocity, 1.001f);

        //                // see if it goes back into the first clip plane
        //                if (Vector3.Dot(clipVelocity, planes[i]) >= 0f)
        //                    continue;

        //                // slide the original velocity along the crease
        //                Vector3 dir = Vector3.Cross(planes[i], planes[j]);
        //                dir.Normalize();
        //                float d = Vector3.Dot(dir, pm.ps.velocity);
        //                clipVelocity = Vector3.Multiply(dir, d);

        //                dir = Vector3.Cross(planes[i], planes[j]);
        //                dir.Normalize();
        //                d = Vector3.Dot(dir, endVelocity);
        //                endClipVelocity = Vector3.Multiply(dir, d);

        //                // see if there is a third plane the the new move enters
        //                for (int k = 0; k < numplanes; k++)
        //                {
        //                    if (k == i || k == j)
        //                        continue;

        //                    if (Vector3.Dot(clipVelocity, planes[k]) >= 0.1f)
        //                        continue;   // move doesn't interact with the plane

        //                    // stop dead at a tripple plane interaction
        //                    pm.ps.velocity = Vector3.Zero;
        //                    return true;
        //                }
        //            }

        //            // if we have fixed all interactions, try another move
        //            pm.ps.velocity = clipVelocity;
        //            endVelocity = endClipVelocity;
        //            break;
        //        }
        //    }

        //    if (gravity)
        //        pm.ps.velocity = endVelocity;

        //    // don't change velocity if in a timer (FIXME: is this correct?)
        //    if (pm.ps.pm_time > 0)
        //        pm.ps.velocity = primal_velocity;

        //    return (bumpcount != 0);
        //}

        /*
        ==================
        PM_ClipVelocity

        Slide off of the impacting surface
         * returns the blocked flags:
            0x01 == floor
            0x02 == step / wall
        ==================
        */
        int ClipVelocity(Vector3 inv, Vector3 normal, ref Vector3 outv, float overbounce)
        {
            
            int blocked = 0;
            if (normal.Z > 0f)
                blocked |= 1; // Assume floor
            if (normal.Z.Equals(0f))
                blocked |= 2; // Wall or step

            // Determine how far along plane to slide based on incoming direction.
            float backoff = Vector3.Dot(inv, normal) * overbounce;

            for (int i = 0; i < 3; i++)
            {
                float change = normal[i] * backoff;
                outv[i] = inv[i] - change;
                // If out velocity is too small, zero it out.
                if (outv[i] > -0.1f && outv[i] < 0.1f)
                    outv[i] = 0;
            }

            return blocked;
        }

        //float VectorNormalize2(  Vector3 v, ref Vector3 outv) 
        //{
        //    float	length, ilength;

        //    length = v[0]*v[0] + v[1]*v[1] + v[2]*v[2];
        //    length = (float)Math.Sqrt (length);

        //    if (length > 0f)
        //    {
        //        ilength = 1/length;
        //        outv[0] = v[0]*ilength;
        //        outv[1] = v[1]*ilength;
        //        outv[2] = v[2]*ilength;
        //    } else {
        //        outv = Vector3.Zero;
        //    }
        		
        //    return length;
        //}

        //private float CommandScale(CubeHags.client.Input.UserCommand cmd)
        //{
        //    int max = Math.Abs((int)cmd.forwardmove);
        //    if (Math.Abs((int)cmd.rightmove) > max)
        //    {
        //        max = Math.Abs((int)cmd.rightmove);
        //    }
        //    if (Math.Abs((int)cmd.upmove) > max)
        //        max = Math.Abs((int)cmd.upmove);

        //    if (max == 0)
        //        return 0f;

        //    float total = (float)Math.Sqrt(cmd.forwardmove * cmd.forwardmove + cmd.rightmove * cmd.rightmove + cmd.upmove * cmd.upmove);
        //    float scale = (float)pm.ps.speed * max / (127f * total);
        //    return scale;
        //}

        //// Handles ground and water friction
        //private void Friction()
        //{
        //    Vector3 velocity = pm.ps.velocity;

        //    Vector3 vec = velocity;
        //    if (pml.walking)
        //    {
        //        vec[2] = 0;
        //    }

        //    float speed = vec.Length();
        //    if (speed < 1f)
        //    {
        //        velocity[0] = 0;
        //        velocity[1] = 0;
        //        return;
        //    }

        //    float drop = 0;

        //    // apply ground friction
        //    //if (pm.waterlevel <= 1)
        //    //{
        //    if (pml.walking && (pml.groundTrace.surfaceFlags & (int)SurfFlags.SURF_SLICK) == 0)
        //        {
        //            // if getting knocked back, no friction
        //            if ((pm.ps.pm_flags & PMFlags.TIME_KNOCKBACK) != PMFlags.TIME_KNOCKBACK)
        //            {
        //                float control = (speed < pm_stopspeed) ? pm_stopspeed : speed;
        //                drop += control * pm_friction * pml.frametime;
        //            }
        //        }
        //    //}

            
        //    // if spectator
        //    if(pm.ps.pm_type == PMType.SPECTATOR)
        //        drop += speed * pm_friction * pml.frametime;
        //    //System.Console.WriteLine("Friction speed drop: " + drop + " - delta: " + frametime);

        //    float newspeed = speed - drop;
        //    if (newspeed < 0f)
        //        newspeed = 0;
        //    newspeed /= speed;

        //    velocity[0] *= newspeed;
        //    velocity[1] *= newspeed;
        //    velocity[2] *= newspeed;

        //    pm.ps.velocity = velocity;
        //}

        public void AngleVectors(Vector3 angles, ref Vector3 forward, ref Vector3 right, ref Vector3 up)
        {
            float angle = angles[1] * (float)(Math.PI * 2 / 360f);
            float sy = (float)Math.Sin(angle);
            float cy = (float)Math.Cos(angle);

            angle = angles[0] * (float)(Math.PI * 2 / 360f);
            float sp = (float)Math.Sin(angle);
            float cp = (float)Math.Cos(angle);

            angle = angles[2] * (float)(Math.PI * 2 / 360f);
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
            //public bool speedCropped;

            public float impactSpeed;
            public float fallVelocity;

            public Vector3 previous_origin;
            public Vector3 previous_velocity;
            public int previous_waterlevel;
        }
    }
}
