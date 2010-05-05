using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;
using CubeHags.client.common;
using CubeHags.client.cgame;

namespace CubeHags.client
{
    public sealed partial class Client
    {
        public cg_t cg = new cg_t();
        public cgs_t cgs;
        public centity_t[] cg_entities = new centity_t[1024];

        void InitCGame()
        {
            for (int i = 0; i < 1024; i++)
            {
                cg_entities[i] = new centity_t();
            }
            float t1 = Common.Instance.Milliseconds();

            // find the current mapname
            string mapname = Info.ValueForKey(cl.gamestate.data[0], "mapname");
            cl.mapname = string.Format("maps/{0}", mapname);

            cls.state = connstate_t.LOADING;

            // init for this gamestate
            // use the lastExecutedServerCommand instead of the serverCommandSequence
            // otherwise server commands sent just before a gamestate are dropped
            CGame.Instance.Init(clc.serverMessageSequence, clc.lastExecutedServerCommand, clc.clientNum);

            // we will send a usercmd this frame, which
            // will cause the server to send us the first snapshot
            cls.state = connstate_t.PRIMED;
            float t2 = Common.Instance.Milliseconds();

            Common.Instance.WriteLine("InitCGame(): {0:0.000} seconds", (t2 - t1) / 1000f);
        }

        public void FirstSnapshot()
        {
            // ignore snapshots that don't have entities
            if ((cl.snap.snapFlags & 2) == 2)
                return;

            cls.state = connstate_t.ACTIVE;

            // set the timedelta so we are exactly on this first frame
            cl.serverTimeDelta = cl.snap.serverTime - cls.realtime;
            cl.oldServerTime = cl.snap.serverTime;
        }

        public void SetCGameTime()
        {
            // getting a valid frame message ends the connection process
            if (cls.state != CubeHags.common.connstate_t.ACTIVE)
            {
                if (cls.state != CubeHags.common.connstate_t.PRIMED)
                    return;

                if (cl.newSnapshots)
                {
                    cl.newSnapshots = false;
                    FirstSnapshot();
                }

                if (cls.state != CubeHags.common.connstate_t.ACTIVE)
                    return;
            }

            // if we have gotten to this point, cl.snap is guaranteed to be valid
            if (!cl.snap.valid)
            {
                Common.Instance.Error("SetCGameTime: !cl.snap.valid");
            }

            if (cl.snap.serverTime < cl.oldFrameServerTime)
            {
                Common.Instance.Error("cl.snap.serverTime < cl.oldFrameServerTime");
            }
            cl.oldFrameServerTime = cl.snap.serverTime;

            // cl_timeNudge is a user adjustable cvar that allows more
            // or less latency to be added in the interest of better 
            // smoothness or better responsiveness.
            int tn = cl_timeNudge.Integer;
            if (tn < -30)
                tn = -30;
            if (tn > 30)
                tn = 30;

            // get our current view of time
            cl.serverTime = cls.realtime + cl.serverTimeDelta - tn;

            // guarantee that time will never flow backwards, even if
            // serverTimeDelta made an adjustment or cl_timeNudge was changed
            if (cl.serverTime < cl.oldServerTime)
            {
                cl.serverTime = cl.oldServerTime;
            }
            cl.oldServerTime = cl.serverTime;

            // note if we are almost past the latest frame (without timeNudge),
            // so we will try and adjust back a bit when the next snapshot arrives
            if (cls.realtime + cl.serverTimeDelta >= cl.snap.serverTime - 5)
            {
                cl.extrapolatedSnapshot = true;
            }

            // if we have gotten new snapshots, drift serverTimeDelta
            // don't do this every frame, or a period of packet loss would
            // make a huge adjustment
            if (cl.newSnapshots)
            {
                AdjustTimeDelta();
            }
        }


        /*
        =================
        CL_AdjustTimeDelta

        Adjust the clients view of server time.

        We attempt to have cl.serverTime exactly equal the server's view
        of time plus the timeNudge, but with variable latencies over
        the internet it will often need to drift a bit to match conditions.

        Our ideal time would be to have the adjusted time approach, but not pass,
        the very latest snapshot.

        Adjustments are only made when a new snapshot arrives with a rational
        latency, which keeps the adjustment process framerate independent and
        prevents massive overadjustment during times of significant packet loss
        or bursted delayed packets.
        =================
        */
        void AdjustTimeDelta()
        {
            cl.newSnapshots = false;

            int newDelta = cl.snap.serverTime - cls.realtime;
            int deltaDelta = Math.Abs(newDelta - cl.serverTimeDelta);

            if (deltaDelta > 500)
            {
                cl.serverTimeDelta = newDelta;
                cl.oldServerTime = cl.snap.serverTime;
                cl.serverTime = cl.snap.serverTime;
            }
            else if (deltaDelta > 100)
            {
                // fast adjust, cut the difference in half
                cl.serverTimeDelta = (cl.serverTimeDelta + newDelta) >>1;
            }
            else
            {
                // slow drift adjust, only move 1 or 2 msec

                // if any of the frames between this and the previous snapshot
                // had to be extrapolated, nudge our sense of time back a little
                // the granularity of +1 / -2 is too high for timescale modified frametimes
                if (Common.Instance.timescale.Value == 0f || Common.Instance.timescale.Value == 1f)
                {
                    if (cl.extrapolatedSnapshot)
                    {
                        cl.extrapolatedSnapshot = false;
                        cl.serverTimeDelta -= 2;
                    }
                    else
                    {
                        // otherwise, move our sense of time forward to minimize total latency
                        cl.serverTimeDelta++;
                    }
                }
            }
        }

        

        public string[] GetServerCommand(int serverCommandNumber)
        {
            // if we have irretrievably lost a reliable command, drop the connection
            if (serverCommandNumber <= clc.serverCommandSequence - 64)
            {
                Common.Instance.Error("GetServerCommand: a reliable command was cycled out");
                return null;
            }

            if (serverCommandNumber >= clc.serverCommandSequence)
            {
                Common.Instance.Error("GetServerCommand: requested a command not received");
                return null;
            }

            string s = clc.serverCommands[serverCommandNumber & 63];
            clc.lastExecutedServerCommand = serverCommandNumber;

            string[] tokens = Commands.TokenizeString(s);
            if (tokens.Length == 0)
                return null;

            if (tokens[0].Equals("disconnect"))
            {
                if (tokens.Length >= 2)
                    Common.Instance.Error(string.Format("Server disconnected - {0}", Commands.Args(tokens)));
                else
                    Common.Instance.Error("Server disconnected");
            }

            return tokens;
        }
    }
}
