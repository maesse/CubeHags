using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;
using SlimDX;
using CubeHags.server;
using CubeHags.client.gui;

namespace CubeHags.client.cgame
{
    public sealed partial class CGame
    {
        pmove_t pmove = new pmove_t();
        int cg_numSolidEntities;
        centity_t[] cg_solidEntities = new centity_t[256];
        int cg_numTriggerEntities;
        centity_t[] cg_triggerEntities = new centity_t[256];

        void PredictPlayerState()
        {
            // if this is the first frame we must guarantee
            // predictedPlayerState is valid even if there is some
            // other error condition
            if (!cg.validPPS)
            {
                cg.validPPS = true;
                cg.predictedPlayerState = cg.snap.ps;
            }

            // non-predicting local movement will grab the latest angles
            if (cg_nopredict.Integer == 1 || cg_synchronousClients.Integer == 1)
            {
                InterpolatePlayerState(true);
                return;
            }

            // prepare for pmove
            pmove.ps = cg.predictedPlayerState;
            pmove.Trace = new TraceDelegate(CG_Trace);
            // save the state before the pmove so we can detect transitions
            Common.PlayerState oldPlayerStateref = cg.predictedPlayerState;
            int current = Client.Instance.cl.cmdNumber;

            // if we don't have the commands right after the snapshot, we
            // can't accurately predict a current position, so just freeze at
            // the last good position we had
            //if (current < 64)
            //    return;
            int cmdNum = current - 63;
            if (cmdNum < 0)
                cmdNum = 1;

            Input.UserCommand oldestCmd = Client.Instance.GetUserCommand(cmdNum);
            if (oldestCmd == null)
                return;
            if (oldestCmd.serverTime > cg.snap.ps.commandTime
                && oldestCmd.serverTime < cg.time)  // special check for map_restart
            {
                Common.Instance.WriteLine("Exceeded packet_backup on commands");
                return;
            }

            // get the latest command so we can know which commands are from previous map_restarts
            Input.UserCommand latestCmd = Client.Instance.GetUserCommand(current);
            
            // get the most recent information we have, even if
            // the server time is beyond our current cg.time,
            // because predicted player positions are going to 
            // be ahead of everything else anyway
            if (cg.nextSnap != null && !cg.nextFrameTeleport && !cg.thisFrameTeleport)
            {
                cg.predictedPlayerState = cg.nextSnap.ps;
                cg.physicsTime = cg.nextSnap.serverTime;
            }
            else
            {
                cg.predictedPlayerState = cg.snap.ps;
                cg.physicsTime = cg.snap.serverTime;
            }

            pmove.pmove_fixed = pmove_fixed.Integer;
            pmove.pmove_msec = pmove_msec.Integer;
            Common.PlayerState oldPlayerState = cg.predictedPlayerState.Clone();
            pmove.ps = cg.predictedPlayerState;
            pmove.tracemask = 1;

            pmove.mins = new Vector3( -16, -16, -36 );
            pmove.maxs = new Vector3(16, 16, 36);
            
            // run cmds
            bool moved = false;
            for (cmdNum = current - 63; cmdNum <= current; cmdNum++)
            {
                if (cmdNum <= 0)
                    continue;
                // get the command
                pmove.cmd = Client.Instance.GetUserCommand(cmdNum);

                if (pmove.pmove_fixed > 0)
                {
                    Common.UpdateViewAngles(ref pmove.ps, pmove.cmd);
                }

                

                // don't do anything if the time is before the snapshot player time
                if (pmove.cmd.serverTime <= cg.predictedPlayerState.commandTime)
                    continue;

                // don't do anything if the command was from a previous map_restart
                if (pmove.cmd.serverTime > latestCmd.serverTime)
                    continue;

                

                // check for a prediction error from last frame
                // on a lan, this will often be the exact value
                // from the snapshot, but on a wan we will have
                // to predict several commands to get to the point
                // we want to compare
                if (cg.predictedPlayerState.commandTime == oldPlayerState.commandTime)
                {
                    if (cg.thisFrameTeleport)
                    {
                        // a teleport will not cause an error decay
                        cg.predictedError = Vector3.Zero;
                        cg.thisFrameTeleport = false;
                    }
                    else
                    {
                        Vector3 adjusted;
                        AdjustPositionForMover(cg.predictedPlayerState.origin, cg.predictedPlayerState.groundEntityNum, cg.physicsTime, cg.oldTime, out adjusted);
                        if (!oldPlayerState.origin.Equals(adjusted))
                            Common.Instance.WriteLine("Prediction error");
                        Vector3 delta = oldPlayerState.origin - adjusted;
                        float len = delta.Length();
                        if (len > 0.1f)
                        {
                            Common.Instance.WriteLine("Predition miss: {0}", len);
                            if (cg_errorDecay.Integer > 0)
                            {
                                int t = cg.time - cg.predictedErrorTime;
                                float f = (cg_errorDecay.Value - t) / cg_errorDecay.Value;
                                if (f < 0)
                                    f = 0;
                                cg.predictedError = Vector3.Multiply(cg.predictedError, f);
                            }
                            else
                                cg.predictedError = Vector3.Zero;

                            cg.predictedError += delta;
                            cg.predictedErrorTime = cg.oldTime;
                        }
                    }
                }

                if (pmove.pmove_fixed > 0)
                    pmove.cmd.serverTime = ((pmove.cmd.serverTime + pmove_msec.Integer - 1) / pmove_msec.Integer) * pmove_msec.Integer;
                Vector3 oldOrigin = cg.predictedPlayerState.origin;

                Common.Instance.Pmove(pmove);

                
                //// Test for stuck
                //trace_t trace = pmove.DoTrace(cg.predictedPlayerState.origin, pmove.mins, pmove.maxs, oldOrigin, 0, 1);
                //if (trace.fraction != 1.0f)
                //{
                //    int test = 2;
                //}
                
                moved = true;
            }

            if (!moved)
                return;

            // adjust for the movement of the groundentity
            AdjustPositionForMover(cg.predictedPlayerState.origin, cg.predictedPlayerState.groundEntityNum, cg.physicsTime, cg.time, out cg.predictedPlayerState.origin);
            
            
            // fire events and other transition triggered things
            TransitionPlayerState(cg.predictedPlayerState, oldPlayerState);
            WindowManager.Instance.info.SetPos(cg.predictedPlayerState.origin);
        }

        public trace_t CG_Trace(Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, int skipNumber, int mask) 
        {
            trace_t t = ClipMap.Instance.Box_Trace(start, end, mins, maxs, 0, mask, 0);
            t.entityNum = t.fraction != 1.0f ? 1022 : 1023;

            // TODO: Check entities
            return t;
        }

        /*
        ========================
        CG_InterpolatePlayerState

        Generates cg.predictedPlayerState by interpolating between
        cg.snap->player_state and cg.nextFrame->player_state
        ========================
        */
        void InterpolatePlayerState(bool grabAngles)
        {
            
            snapshot_t prev = cg.snap;
            snapshot_t next = cg.nextSnap;

            // not 100% sure this is correct
            Common.PlayerState outs = cg.snap.ps;
            

            // if we are still allowing local input, short circuit the view angles
            if (grabAngles)
            {
                int cmdNum = Client.Instance.cl.cmdNumber;
                Input.UserCommand cmd = Client.Instance.GetUserCommand(cmdNum);
                Common.UpdateViewAngles(ref outs, cmd);
            }
            cg.predictedPlayerState = outs;

            // if the next frame is a teleport, we can't lerp to it
            if (cg.nextFrameTeleport)
                return;

            if (next == null || next.serverTime <= prev.serverTime)
                return;

            float f = (float)(cg.time / prev.serverTime) / (next.serverTime - prev.serverTime);

            for (int i = 0; i < 3; i++)
            {
                outs.origin[i] = prev.ps.origin[i] + f * (next.ps.origin[i] - prev.ps.origin[i]);
                if (!grabAngles)
                {
                    outs.viewangles[i] = LerpAngle(prev.ps.viewangles[i], next.ps.viewangles[i], f);
                }
                outs.velocity[i] = prev.ps.velocity[i] + f * (next.ps.velocity[i] - prev.ps.velocity[i]);
            }
        }

        float LerpAngle(float from, float to, float frac)
        {
            if (to - from > 180f)
            {
                to -= 180f;
            }
            if (to - from < -180f)
            {
                to += 180f;
            }
            float a = from + frac * (to - from);

            return a;
        }
    }
}
