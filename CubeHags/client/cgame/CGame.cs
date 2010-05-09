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

            Renderer.Instance.Render(view);
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

            Common.playerState_t ps = cg.predictedPlayerState;

            cg.xyspeed = (float)Math.Sqrt(ps.velocity[0] * ps.velocity[0] + ps.velocity[1] * ps.velocity[1]);
            view.vieworg = ps.origin;
            cg.refdefViewAngles = ps.viewangles;
            view.viewangles = ps.viewangles;
            // OffsetFirstperonView();

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

        void AnglesToAxis(Vector3 angles, out Vector3[] axis)
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

            //string s = cgs.gameState.data[(int)ConfigString.CS_LEVEL_START_TIME];
            //cgs.levelStartTime = int.Parse(s);

            ParseServerInfo();

            // load the new map
            LoadString("Collision map");
            ClipMap.Instance.LoadMap(cgs.mapname, true);

            cg.loading = true;  // force players to load instead of defer <- ??

            LoadString("Graphics");
            RegisterGraphics();

            LoadString("Clients");
            //RegisterClients();

            cg.loading = false; // future players will be deferred

            //InitLocalEntities();

            // Make sure we have update values (scores)
            //SetConfigValues();

            LoadString("");
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
            WindowManager.Instance.connectGUI.svStats.Text = s;
            Client.Instance.EndFrame();
        }

        void RegisterCVars()
        {

        }

        void InitConsoleCommands()
        {

        }
    }
}
