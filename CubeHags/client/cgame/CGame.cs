using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;
using CubeHags.client.map.Source;
using CubeHags.client.render;
using CubeHags.client.common;
using SlimDX;
using CubeHags.client.gui;

namespace CubeHags.client.cgame
{
    public sealed partial class CGame
    {
        private static readonly CGame _Instance = new CGame();
        public static CGame Instance { get { return _Instance; } }

        public cgs_t cgs;
        public cg_t cg;
        public centity_t[] Entities;

        CVar cg_nopredict = CVars.Instance.Get("cg_nopredict", "0", CVarFlags.TEMP);
        CVar cg_synchronousClients = CVars.Instance.Get("cg_synchronousClients", "0", CVarFlags.TEMP);
        CVar pmove_fixed = CVars.Instance.Get("pmove_fixed", "0", CVarFlags.SYSTEM_INFO);
        CVar pmove_msec = CVars.Instance.Get("pmove_msec", "8", CVarFlags.SYSTEM_INFO);
        CVar cg_errorDecay = CVars.Instance.Get("cg_errorDecay", "100", CVarFlags.TEMP);
        CVar cg_runpitch = CVars.Instance.Get("cg_runpitch", "0.002", CVarFlags.ARCHIVE);
        CVar cg_runroll = CVars.Instance.Get("cg_runroll", "0.002", CVarFlags.ARCHIVE);
        CVar cg_bobpitch = CVars.Instance.Get("cg_bobpitch", "0.002", CVarFlags.ARCHIVE);
        CVar cg_bobroll = CVars.Instance.Get("cg_bobroll", "0.002", CVarFlags.ARCHIVE);
        CVar cg_bobup = CVars.Instance.Get("cg_bobroll", "0.004", CVarFlags.CHEAT);

        CGame()
        {
        }

        public void DrawActiveFrame(float serverTime)
        {
            cg.time = (int)serverTime;
            // set up cg.snap and possibly cg.nextSnap
            ProcessSnapshots();

            // if we haven't received any snapshots yet, all
            // we can draw is the information screen
            if (cg.snap == null || (cg.snap.snapFlags & 2) == 2)
            {
                // Todo: DrawInformation
                return;
            }

            // this counter will be bumped for every valid scene we generate
            cg.clientFrame++;

            // update cg.predictedPlayerState
            PredictPlayerState();

            ViewParams view = CalcViewValues();
            view.time = cg.time;
            cg.oldTime = cg.time;

            AddPacketEntities();

            if(Renderer.Instance.SourceMap != null)
                Renderer.Instance.SourceMap.VisualizeBBox();

            AddLagometerFrameInfo();

            Renderer.Instance.Render(view);
        }

        void AddLagometerFrameInfo()
        {
            int offset = cg.time - cg.latestSnapshotTime;
            Client.Instance.lagometer.frameSamples[Client.Instance.lagometer.frameCount++ & Lagometer.LAGBUFFER - 1] = offset;
        }

        void OffsetFirstPersonView()
        {
            if (cg.snap.ps.pm_type == Common.PMType.INTERMISSION)
                return;

            
            

            // if dead, fix the angle and don't add any kick
            if (cg.snap.ps.stats[0] <= 0)
            {
                cg.refdefViewAngles[2] = 40;
                cg.refdefViewAngles[0] = -15;
                cg.refdefViewAngles[1] = cg.snap.ps.stats[4];
                cg.refdef.vieworg[2] += cg.predictedPlayerState.viewheight;
                return;
            }

            Vector3 predVel = cg.predictedPlayerState.velocity;

            // add angles based on velocity
            float delta = Vector3.Dot(predVel, cg.refdef.viewaxis[0]);
            cg.refdefViewAngles[0] += delta * cg_runpitch.Value;
            delta = Vector3.Dot(predVel, cg.refdef.viewaxis[1]);
            cg.refdefViewAngles[2] += delta * cg_runroll.Value;

            // add angles based on bob
            // make sure the bob is visible even at low speeds
            float speed = cg.xyspeed > 200 ? cg.xyspeed : 200;
            delta = cg.bobfracsin * cg_bobpitch.Value * speed;
            if ((cg.predictedPlayerState.pm_flags & PMFlags.DUCKED) == PMFlags.DUCKED)
                delta *= 3; // crouching
            cg.refdefViewAngles[0] += delta;
            delta = cg.bobfracsin * cg_bobroll.Value * speed;
            if ((cg.predictedPlayerState.pm_flags & PMFlags.DUCKED) == PMFlags.DUCKED)
                delta *= 3; // crouching accentuates roll
            if ((cg.bobcycle & 1) > 0)
                delta = -delta;
            cg.refdefViewAngles[2] += delta;

            // add view height
            cg.refdef.vieworg[2] += cg.predictedPlayerState.viewheight;

            //// smooth out duck height changes
            float timeDelta = cg.time - cg.duckTime;
            if (timeDelta < 100)
            {
                cg.refdef.vieworg[2] -= cg.duckChange * (100 - timeDelta) / 100;
            }

            // add bob height
            float bob = cg.bobfracsin * cg.xyspeed * cg_bobup.Value;
            if (bob > 6)
                bob = 6;
            cg.refdef.vieworg[2] += bob;


            StepOffset();
        }

        void StepOffset()
        {
            //// smooth out stair climbing
            //int timeDelta = cg.time - cg.stepTime;
            //if (timeDelta < 200)
            //{
            //    cg.refdef.vieworg[2] -= cg.stepChange * (200 - timeDelta) / 200;
            //}
        }

        ViewParams CalcViewValues()
        {
            ViewParams view = new ViewParams();
            view.viewportWidth = Renderer.Instance.RenderSize.Width;
            view.viewportHeight = Renderer.Instance.RenderSize.Height;
            view.fovX = 80f;
            float x = (float)(view.viewportWidth / Math.Tan(view.fovX / 360 * Math.PI));
            float fovY = (float)Math.Atan2(view.viewportHeight, x);
            fovY = (float)(fovY * 360 / Math.PI);
            view.fovY = fovY;

            Common.PlayerState ps = cg.predictedPlayerState;

            cg.bobcycle = (ps.bobCycle & 127)>>7; // FIX
            cg.bobfracsin = (float)Math.Abs(Math.Sin(ps.bobCycle & 127) / 127.0f * Math.PI);
            cg.xyspeed = (float)Math.Sqrt(ps.velocity[0] * ps.velocity[0] + ps.velocity[1] * ps.velocity[1]);
            view.vieworg = ps.origin;
            cg.refdef = view;
            cg.refdefViewAngles = ps.viewangles;
            view.viewangles = ps.viewangles;
            //if (cg.renderingThirdPerson)
            //{
            //}
            //else
            {
                AnglesToAxis(cg.refdefViewAngles, out view.viewaxis);
                OffsetFirstPersonView();
            }

            if (cg_errorDecay.Value > 0)
            {
                int t = cg.time - cg.predictedErrorTime;
                float f = (cg_errorDecay.Value - t) / cg_errorDecay.Value;
                if (f > 0 && f < 1)
                    view.vieworg = ViewParams.VectorMA(view.vieworg, f, cg.predictedError);
                else
                    cg.predictedErrorTime = 0;
            }

            // position eye reletive to origin
            AnglesToAxis(cg.refdefViewAngles, out view.viewaxis);

            return view;
        }

        public static void AnglesToAxis(Vector3 angles, out Vector3[] axis)
        {
            axis = new Vector3[3];
            Vector3 right = Vector3.Zero;
            Common.Instance.AngleVectors(angles, ref axis[0], ref right, ref axis[2]);
            axis[1] = Vector3.Subtract(Vector3.Zero, right);
        }

        /*
        ===============
        CG_ResetPlayerEntity

        A player just came into view or teleported, so reset all animation info
        ===============
        */
        void ResetPlayerEntity(centity_t cent)
        {
            cent.errorTime = -99999;        // guarantee no error decay added
            cent.extrapolated = false;

            //ClearLerpFrame(cgs.clientinfo[cent.currentState.clientNum], cent.pe

            Common.Instance.EvaluateTrajectory(cent.currentState.pos, cg.time, out cent.lerpOrigin);
            Common.Instance.EvaluateTrajectory(cent.currentState.apos, cg.time, out cent.lerpAngles);

            cent.rawOrigin = cent.lerpOrigin;
            cent.rawAngles = cent.lerpAngles;

            // Todo: Move torse & legs
        }

        /*
        =================
        CG_Init

        Called after every level change or subsystem restart
        Will perform callbacks to make the loading info screen update.
        =================
        */
        public void Init(int serverMessageNum, int serverCommandSequence, int clientNum)
        {
            // clear everything
            cgs = new cgs_t();
            cgs.clientinfo = new clientInfo_t[64];
            cg = new cg_t();
            Entities = new centity_t[1024];
            for (int i = 0; i < 1024; i++)
            {
                Entities[i] = new centity_t();
            }

            cg.clientNum = clientNum;
            cgs.processedSnapshotNum = serverMessageNum;
            cgs.serverCommandSequence = serverCommandSequence;

            RegisterCVars();
            InitConsoleCommands();

            // get the gamestate from the client system
            cgs.gameState = Client.Instance.cl.gamestate;

            string s = cgs.gameState.data[(int)ConfigString.CS_LEVEL_START_TIME];
            cgs.levelStartTime = int.Parse(s);

            ParseServerInfo();

            // load the new map
            LoadString("Collision map");
            ClipMap.Instance.LoadMap(cgs.mapname, true);

            cg.loading = true;  // force players to load instead of defer <- ??

            LoadString("Graphics");
            RegisterGraphics();

            LoadString("Clients");
            RegisterClients();

            cg.loading = false; // future players will be deferred

            //InitLocalEntities();

            // Make sure we have update values (scores)
            SetConfigValues();

            LoadString("");
        }

        void RegisterClients()
        {
            //LoadingClient(cg.clientNum);
            NewClientInfo(cg.clientNum);

            for (int i = 0; i < 64; i++)
            {
                if (cg.clientNum == i)
                    continue;
                
                string clientInfo = CG_ConfigString((int)ConfigString.CS_PLAYERS + i);
                if (clientInfo == null)
                    continue;

                //LoadingClient(i);
                NewClientInfo(i);
            }

            //BuildSpectatorString();
        }

        /*
        =================
        CG_Shutdown

        Called before every level change or subsystem restart
        =================
        */
        public void CG_Shutdown()
        {
            // some mods may need to do cleanup work here,
            // like closing files or archiving session data
        }

        public string CG_ConfigString(int index)
        {
            if (index < 0 || index >= (int)ConfigString.CS_MAX)
                Common.Instance.Error("CG_ConfigString: bad index: " + index);

            if (!cgs.gameState.data.ContainsKey(index))
                return null;

            return cgs.gameState.data[index];
        }

        public static Vector3 SnapVector(Vector3 v)
        {
            return new Vector3((int)v.X, (int)v.Y, (int)v.Z);
        }

        void RegisterGraphics()
        {
            LoadString(cgs.mapname);
            SourceParser.LoadWorldMap(cgs.mapname);

        }

        void LoadString(string s)
        {
            WindowManager.Instance.connectGUI.LoadString(s);
            Client.Instance.EndFrame();
        }

        void RegisterCVars()
        {
            CVars.Instance.Get("r_fov", "90", CVarFlags.ARCHIVE);

            // see if we are also running the server on this machine
            if (CVars.Instance.FindVar("sv_running").Bool)
                cgs.localServer = true;
        }

        void InitConsoleCommands()
        {
            Commands.Instance.AddCommand("kill", null);
            Commands.Instance.AddCommand("say", null);
            Commands.Instance.AddCommand("say_team", null);
            Commands.Instance.AddCommand("god", null);
            Commands.Instance.AddCommand("noclip", null);
            Commands.Instance.AddCommand("team", null);
            Commands.Instance.AddCommand("kill", null);
        }
    }
}
