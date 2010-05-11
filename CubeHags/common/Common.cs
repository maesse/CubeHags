using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client.common;
using CubeHags.server;
using CubeHags.client;
using System.IO;
using System.Threading;
using SlimDX;
using CubeHags.client.map.Source;
using CubeHags.client.render;
using System.Runtime.InteropServices;
using CubeHags.client.input;

namespace CubeHags.common
{
    public sealed partial class Common
    {
        private static readonly Common _Instance = new Common();
        public static Common Instance { get { return _Instance; } }
        private static string CONFIG_NAME = "cubehags.cfg";

        private StreamWriter logWriter;
        public CVar maxfps;
        public CVar logfile;
        public CVar cl_running;
        public CVar sv_running;
        public CVar timescale;

        public int frameTime;
        static int lastTime = 0;

        public int frameNumber;
        private long startTime;
        public float frameMsec;
        
        Queue<sysEvent_t> pushedEventQueue = new Queue<sysEvent_t>(256);
        Queue<sysEvent_t> eventQueue = new Queue<sysEvent_t>(256);

        List<string> commandLines = new List<string>();

        public static Random Rand = new Random();

        public Common()
        {
        }

        public void Frame()
        {
            // Write config file if anything has changed
            WriteConfiguration();

            //
            // main event loop
            //

            // we may want to spin here if things are going too fast
            int minMsec = (int)(1000f / maxfps.Integer);

            int msec = minMsec;
            do
            {
                int timeRemaining = minMsec - msec;
                if (timeRemaining >= 7)
                {
                    //Thread.Sleep(1);
                    //Thread.Sleep((int)timeRemaining);
                }

                frameTime = (int)EventLoop();
                if (lastTime > frameTime)
                    lastTime = frameTime; // possible on first frame

                msec = frameTime - lastTime;
            } while (msec < minMsec);

            Commands.Instance.Execute();

            lastTime = frameTime;

            // mess with msec if needed
            frameMsec = msec;
            if (msec > 200)
            {
                msec = 200;
            }

            //
            // server side
            //
            Server.Instance.Frame(msec);

            //
            // client system
            //
            //
            // run event loop a second time to get server to client packets
            // without a frame of latency
            //
            EventLoop();
            Commands.Instance.Execute();

            //
            // client side
            //
            Client.Instance.Frame(msec);

            frameNumber++;
        }


        /*
        =================
        Com_EventLoop

        Returns last event time
        =================
        */
        public float EventLoop()
        {
            sysEvent_t ev;
            
            while (true)
            {
                ev = GetEvent();
                // if no more events are available
                if (ev.evType == sysEventType_t.SE_NONE)
                {
                    return ev.evTime;
                }

                switch (ev.evType)
                {
                    case sysEventType_t.SE_NONE:
                        break;
                    case sysEventType_t.SE_PACKET:
                        Net.Packet packet = (Net.Packet)ev.data;
                        if (sv_running.Integer == 1 && packet.Address.Port != Net.Instance.net_port.Integer)
                            Server.Instance.PacketEvent(packet);
                        else
                            Client.Instance.PacketEvent(packet);
                        break;
                }
                break;
            }


            //if (ev != null)
                return ev.evTime;
            //return 0f;
        }

       

        sysEvent_t GetEvent()
        {
            if (pushedEventQueue.Count > 0)
                return pushedEventQueue.Dequeue();

            return GetRealEvent();
        }

        sysEvent_t GetRealEvent()
        {
            // return if we have data
            if (eventQueue.Count > 0)
                return eventQueue.Dequeue();

            // check for network packets
            Net.Packet packet = Net.Instance.GetPacket();
            if (packet != null)
            {
                QueueEvent(0f, sysEventType_t.SE_PACKET, 0, 0, packet.Buffer.LengthBytes, packet);
            }

            // return if we have data
            if (eventQueue.Count > 0)
                return eventQueue.Dequeue();

            // create an empty event to return
            sysEvent_t evt = new sysEvent_t();
            evt.evTime = Milliseconds();
            return evt;
        }


        /*
        ================
        Com_QueueEvent

        A time of 0 will get the current time
        Ptr should either be null, or point to a block of data that can
        be freed by the game later.
        ================
        */
        void QueueEvent(float time, sysEventType_t type, int value, int value2, int dataSize, object data)
        {
            sysEvent_t evt = new sysEvent_t();
            if (time == 0f)
                time = Milliseconds();

            evt.evTime = time;
            evt.evType = type;
            evt.evValue = value;
            evt.evValue2 = value2;
            evt.dataSize = dataSize;
            evt.data = data;

            eventQueue.Enqueue(evt);
        }

        public void WriteConfiguration()
        {
            // Got anything new to write?
            if ((CVars.Instance.modifiedFlags & CVarFlags.ARCHIVE) != CVarFlags.ARCHIVE)
                return;
            CVars.Instance.modifiedFlags &= ~CVarFlags.ARCHIVE;

            StreamWriter writer = new StreamWriter(File.Create(CONFIG_NAME));
            writer.WriteLine("// Generated by Cubehags, do not modify");
            KeyHags.Instance.WriteBinds(writer);
            CVars.Instance.WriteVariables(writer);
            writer.Close();
        }

        void Quit_f(string[] tokens)
        {
            Shutdown();
        }

        void ParseCommandLine(string commandLine)
        {
            bool inq = false; // in quote
            int pos = 0;
            StringBuilder strBuilder = new StringBuilder();
            while (pos < commandLine.Length)
            {
                if (commandLine[pos].Equals('"'))
                {
                    inq = !inq;
                }
                // look for a + seperating character
                // if commandLine came from a file, we might have real line seperators
                if ((commandLine[pos].Equals('+') && !inq) || commandLine[pos].Equals('\n') || commandLine[pos].Equals('\r'))
                {
                    commandLines.Add(strBuilder.ToString());
                    strBuilder.Length = 0;
                }
                else
                {
                    strBuilder.Append(commandLine[pos]);
                    pos++;
                }
            }
            if (strBuilder.Length > 0)
                commandLines.Add(strBuilder.ToString());
        }

        /*
        ===============
        Com_StartupVariable

        Searches for command line parameters that are set commands.
        If match is not NULL, only that cvar will be looked for.
        That is necessary because cddir and basedir need to be set
        before the filesystem is started, but all other sets should
        be after execing the config and default.
        ===============
        */
        void StartupVariable(string match)
        {
            foreach (string str in commandLines)
            {
                string[] tokens = Commands.TokenizeString(str);
                if (!tokens[0].Equals("set"))
                    continue;

                if (match == null || match.Equals(tokens[1]))
                {
                    CVars.Instance.Set(tokens[1], tokens[2]);
                    CVar cv = CVars.Instance.Get(tokens[1], "", CVarFlags.NONE);
                    cv.Flags |= CVarFlags.USER_CREATED;
                }
            }
        }

        public void Init(string commandline)
        {
            System.Console.WriteLine("Cubehags os:{0} cpus:{1}", Environment.OSVersion, Environment.ProcessorCount);

            CVars.Instance.Init();

            // prepare enough of the subsystems to handle
            // cvar and command buffer management
            ParseCommandLine(commandline);
            StartupVariable(null);

            // done early so bind command exists
            KeyHags.Instance.Init();

            FileCache.Instance.Init();

            Commands.Instance.AddCommand("quit", new CommandDelegate(Quit_f));

            ExecuteCfg();
            StartupVariable(null);

            // if any archived cvars are modified after this, we will trigger a writing
            // of the config file
            CVars.Instance.modifiedFlags &= ~CVarFlags.ARCHIVE;

            //
            // init commands and vars
            //
            maxfps = CVars.Instance.Get("maxfps", "115", CVarFlags.ARCHIVE);
            logfile = CVars.Instance.Get("logfile", "0", CVarFlags.TEMP);
            cl_running = CVars.Instance.Get("cl_running", "0", CVarFlags.ROM);
            sv_running = CVars.Instance.Get("sv_running", "0", CVarFlags.ROM);
            timescale = CVars.Instance.Get("timescale", "1", CVarFlags.TEMP);

            FileStream stream  = File.OpenWrite("cubehagslog.txt");
            logWriter = new StreamWriter(stream);

            Server.Instance.Init();
            Client.Instance.Init();

            // set com_frameTime so that if a map is started on the
            // command line it will still be able to count on com_frameTime
            // being random enough for a serverid
            frameTime = (int)Milliseconds();

            // add + commands from command line
            if (!AddStartupCommands())
            {
                // if the user didn't give any commands, run default action
                Client.Instance.cin.AlterGameState = true;
                Commands.Instance.AddText("cinematic cube.avi\n");
                CVars.Instance.Set("nextmap", "map menu");
                Commands.Instance.Execute();
            }
        }

        /*
        =================
        Com_AddStartupCommands

        Adds command line parameters as script statements
        Commands are seperated by + signs

        Returns qtrue if any late commands were added, which
        will keep the demoloop from immediately starting
        =================
        */
        bool AddStartupCommands()
        {
            bool added = false;
            // quote every token, so args with semicolons can work
            foreach (string str in commandLines)
            {
                if (str.StartsWith("set"))
                    continue;

                added = true;
                Commands.Instance.AddText(str);
                Commands.Instance.AddText("\n");
            }

            return added;
        }

        public float Milliseconds()
        {
            if (startTime == 0)
                startTime = HighResolutionTimer.Ticks;

            return (float)(HighResolutionTimer.Ticks - startTime) / HighResolutionTimer.Frequency * 1000f;
        }

        // Loads default cfgs
        void ExecuteCfg()
        {
            Commands.Instance.ExecuteText(Commands.EXECTYPE.EXEC_NOW, "exec default.cfg\n");
            Commands.Instance.Execute();

            Commands.Instance.ExecuteText(Commands.EXECTYPE.EXEC_NOW, string.Format("exec {0}\n", CONFIG_NAME));
            Commands.Instance.Execute();

            Commands.Instance.ExecuteText(Commands.EXECTYPE.EXEC_NOW, "exec autoexec.cfg\n");
            Commands.Instance.Execute();
        }

        public void Error(string str)
        {
            Write(str);
            Shutdown();
            Environment.Exit(0);
        }

        //public static string Parse(string data, bool allowLineBreaks, ref int nRead)
        //{
        //    if (data == null)
        //    {
        //        return com_token;
        //    }

        //    bool hasnewLines = false;
        //    int i = 0;
        //    while (true)
        //    {
        //        // skip whitespace
        //        data = data.Trim();
        //        while (data.Length > 0 && data[0].Equals('\n'))
        //        {
        //            data = data.Substring(1);
        //            data = data.Trim();
        //            hasnewLines = true;
        //        }
        //        if (data.Length == 0)
        //            return com_token;
        //        if (hasnewLines && !allowLineBreaks)
        //            return com_token;

                
        //        // skip double slash comments
        //        if (data[i] == '/' && data[i + 1] == '/')
        //        {
        //            i += 2;
        //            while (i < data.Length && data[i] != '\n')
        //            {
        //                i++;
        //            }
        //        }
        //        // skip /* */ comments
        //        else if (data[i] == '/' && data[i + 1] == '*')
        //        {
        //            i += 2;
        //            while (i < data.Length && (data[i] != '*' || data[i + 1] != '/'))
        //            {
        //                i++;
        //            }
        //            if (i < data.Length)
        //                i += 2;
        //        }
        //        else
        //            break;

        //    }

        //    // handle quoted strings
        //    if (data[i] == '"')
        //    {
        //        i++;
        //        int end = data.IndexOf('"', i);
        //        if (end < 0)
        //        {
        //            com_token = data.Substring(i);
        //        }
        //        else
        //        {
        //            com_token = data.Substring(i, end-i);
        //        }
        //    }
        //    // parse a regular word
        //    do {
        //        string.
        //    } while(
        //}
        //static string com_token = "";
        public void Write(string str, params object[] args)
        {
            string formatted = string.Format(str, args);
            Write(formatted);
        }

        public void Write(string str)
        {
            System.Console.Write(str);
            Client.Instance.ConsolePrint(str);
            logWriter.WriteLine(str);
            logWriter.Flush();
        }

        public void WriteLine(string str, params object[] args)
        {
            Write(str + '\n', args);
        }

        public void WriteLine(string str)
        {
            Write(str + '\n');
        }

        public void Shutdown()
        {
            logWriter.Flush();
            logWriter.Close();
            logWriter.Dispose();
        }

        public static void SetPlaneSignbits (cplane_t plane) {
        	int	bits, j;

        	// for fast box on planeside test
        	bits = 0;
        	for (j=0 ; j<3 ; j++) {
        		if (plane.normal[j] < 0) {
        			bits |= 1<<j;
        		}
        	}
        	plane.signbits = (byte)bits;
        }

        public void EvaluateTrajectory(trajectory_t tr, int atTime, out Vector3 result)
        {
            result = Vector3.Zero;

            switch (tr.trType)
            {
                case trType_t.TR_STATIONARY:
                case trType_t.TR_INTERPOLATE:
                    result = tr.trBase;
                    break;
                case trType_t.TR_LINEAR:
                    float deltaTime = (float)(atTime - tr.trTime) * 0.001f;
                    result = ViewParams.VectorMA(tr.trBase, deltaTime, tr.trDelta);
                    break;
                case trType_t.TR_SINE:
                    deltaTime = (float)(atTime - tr.trTime) / tr.trDuration;
                    float phase = (float)Math.Sin(deltaTime * Math.PI * 2);
                    result = ViewParams.VectorMA(tr.trBase, phase, tr.trDelta);
                    break;
                case trType_t.TR_LINEAR_STOP:
                    if (atTime > tr.trTime + tr.trDuration)
                    {
                        atTime = tr.trTime + tr.trDuration;
                    }
                    deltaTime = (float)(atTime - tr.trTime) * 0.001f;
                    if (deltaTime < 0f)
                        deltaTime = 0f;
                    result = ViewParams.VectorMA(tr.trBase, deltaTime, tr.trDelta);
                    break;
                case trType_t.TR_GRAVITY:
                    deltaTime = (float)(atTime - tr.trTime) * 0.001f;
                    result = ViewParams.VectorMA(tr.trBase, deltaTime, tr.trDelta);
                    result[2] -= 0.5f * 800 * deltaTime * deltaTime;
                    break;
                default:
                    Common.Instance.Error(string.Format("Unknown TrType: {0}", tr.trType));
                    break;
            }
        }
        /*
        ==================
        BoxOnPlaneSide

        Returns 1, 2, or 1 + 2
        ==================
        */
        public int BoxOnPlaneSide(ref Vector3 mins, ref Vector3 maxs, cplane_t frustum)
        {
            // fast axial cases
            if (frustum.type < 3)
            {
                if (frustum.dist <= mins[frustum.type])
                    return 1;
                if (frustum.dist >= maxs[frustum.type])
                    return 2;
                return 3;
            }

            // general case
            float[] dist = new float[2];
            dist[0] = dist[1] = 0f;
            int b;
            if (frustum.signbits < 8)
            {
                for (int i = 0; i < 3; i++)
                {
                    b = (frustum.signbits >> i) & 1;
                    dist[(b == 1 ? 1 : 0)] += frustum.normal[i] * maxs[i];
                    dist[(b != 1 ? 1 : 0)] += frustum.normal[i] * mins[i];
                }
            }

            int sides = 0;
            if (dist[0] >= frustum.dist)
                sides = 1;
            else if (dist[1] < frustum.dist)
                sides |= 2;

            return sides;
        }

        // the server looks at a sharedEntity, which is the start of the game's gentity_t structure
        public class sharedEntity_t
        {
        	public entityState_t	s;				// communicated by server to clients
            public entityShared_t r;				// shared by both the server system and game
        }

        // entity->svFlags
        // the server does not know how to interpret most of the values
        // in entityStates (level eType), so the game must explicitly flag
        // special server behaviors
        [Flags]
        public enum svFlags : int
        {
            NONE     = 0x00000000,
            NOCLIENT = 0x00000001,              // don't send entity to clients, even if it has effects
            CLIENTMASK = 0x00000002,
            BROADCAST = 0x00000020,             // send to all connected clients
            PORTAL = 0x00000040,                // merge a second pvs at origin2 into snapshots
            USE_CURRENT_ORIGIN = 0x00000080,    // entity->r.currentOrigin instead of entity->s.origin
                                                // for link position (missiles and movers)
            SINGLECLIENT = 0x00000100,          // only send to a single client (entityShared_t->singleClient)
            NOSERVERINFO = 0x00000200,          // don't send CS_SERVERINFO updates to this client
                                                // so that it can be updated for ping tools without
                                                // lagging clients
            CAPSULE = 0x00000400,               // use capsule for collision detection instead of bbox
            NOTSINGLECLIENT = 0x00000800        // send entity to everyone but one client   
                                                // (entityShared_t->singleClient)
        }

        public class entityShared_t
        {
        	public entityState_t	s;				// communicated by server to clients

            public bool linked;				// qfalse if not in any good cluster
            public int linkcount;

            public svFlags svFlags;			// SVF_NOCLIENT, SVF_BROADCAST, etc

        	// only send to this client when SVF_SINGLECLIENT is set	
        	// if SVF_CLIENTMASK is set, use bitmask for clients to send to (maxclients must be <= 32, up to the mod to enforce this)
            public int singleClient;

            public bool bmodel;				// if false, assume an explicit mins / maxs bounding box
        									// only set by trap_SetBrushModel
            public Vector3 mins, maxs;
            public int contents;			// CONTENTS_TRIGGER, CONTENTS_SOLID, CONTENTS_BODY, etc
        									// a non-solid entity should set to 0

            public Vector3 absmin, absmax;		// derived from mins/maxs and origin + rotation

        	// currentOrigin will be used for all collision detection and world linking.
        	// it will not necessarily be the same as the trajectory evaluation for the current
        	// time, because each entity must be moved one at a time after time is advanced
        	// to avoid simultanious collision issues
            public Vector3 currentOrigin;
            public Vector3 currentAngles;

        	// when a trace call is made and passEntityNum != ENTITYNUM_NONE,
        	// an ent will be excluded from testing if:
        	// ent->s.number == passEntityNum	(don't interact with self)
        	// ent->s.ownerNum = passEntityNum	(don't interact with your own missiles)
        	// entity[ent->s.ownerNum].ownerNum = passEntityNum	(don't interact with other missiles from owner)
            public int ownerNum;
        }

        

        public enum PMType : int
        {
        	NORMAL,		// can accelerate and turn
        	NOCLIP,		// noclip movement
        	SPECTATOR,	// still run into walls
        	DEAD,		// no acceleration or turning, but free falling
        	FREEZE,		// stuck in place with no control
        	INTERMISSION,	// no movement or status bar
        	SPINTERMISSION	// no movement or status bar
        }

        public class playerState_t
        {
            public playerState_t Clone()
            {
                playerState_t s = new playerState_t();
                s.commandTime = commandTime;
                s.pm_flags = pm_flags;
                s.pm_time = pm_time;
                s.origin = origin;
                s.delta_angles = delta_angles;
                s.movementDir = movementDir;
                s.clientNum = clientNum;
                s.pm_type = pm_type;
                s.pmove_framecount = pmove_framecount;
                s.speed = speed;
                s.stats = stats;
                s.velocity = velocity;
                s.viewangles = viewangles;
                s.viewheight = viewheight;
                s.weaponTime = weaponTime;
                return s;
            }
            public int commandTime;	// cmd->serverTime of last executed command
            public PMType pm_type;
            //public int bobCycle;		// for view bobbing and footstep generation
            public PMFlags pm_flags;		// ducked, jump_held, etc
            public int pm_time;

            public Vector3 origin;
            public Vector3 velocity;
            public int weaponTime;
            public int gravity;
            public int speed;
            public int[] delta_angles = new int[3];	// 3 add to command angles to get view direction
            // changed by spawns, rotating objects, and teleporters

            public int groundEntityNum;// ENTITYNUM_NONE = in air

            //public int legsTimer;		// don't change low priority animations until this runs out
            //public int legsAnim;		// mask off ANIM_TOGGLEBIT

            //public int torsoTimer;		// don't change low priority animations until this runs out
            //public int torsoAnim;		// mask off ANIM_TOGGLEBIT

            public int movementDir;	// a number 0 to 7 that represents the reletive angle
            // of movement to the view angle (axial and diagonals)
            // when at rest, the value will remain unchanged
            // used to twist the legs during strafing

            public Vector3 grapplePoint;	// location of grapple to pull towards if PMF_GRAPPLE_PULL

            public int eFlags;			// copied to entityState_t->eFlags

            public int eventSequence;	// pmove generated events
            public int[] events = new int[2];     // 2
            public int[] eventParms = new int[2]; // 2

            public int externalEvent;	// events set on player from another source
            public int externalEventParm;
            public int externalEventTime;

            public int clientNum;		// ranges from 0 to MAX_CLIENTS-1
            //public int weapon;			// copied to entityState_t->weapon
            //public int weaponstate;

            public Vector3 viewangles;		// for fixed views
            public int viewheight;

            // damage feedback
            //public int damageEvent;	// when it changes, latch the other parms
            //public int damageYaw;
            //public int damagePitch;
            //public int damageCount;

            public int[] stats = new int[16]; // 16
            public int[] persistant = new int[16];	// stats that aren't cleared on death
            //public int[] powerups = new int[16];	// level.time that the powerup runs out
            //public int[] ammo = new int[16];

            public int generic1;
            //public int loopSound;
            //public int jumppad_ent;	// jumppad entity hit this frame

            // not communicated over the net at all
            public int ping;			// server to game info for scoreboard
            public int pmove_framecount;	// FIXME: don't transmit over the network
            //public int jumppad_frame;
            public int entityEventSequence;
        }

        // entityState_t is the information conveyed from the server
        // in an update message about entities that the client will
        // need to render in some way
        // Different eTypes may use the information in different ways
        // The messages are delta compressed, so it doesn't really matter if
        // the structure size is fairly large
        public class entityState_t
        {
            public int number;			// entity index
            public int eType;			// entityType_t
            public int eFlags;

            public trajectory_t pos;	// for calculating position
            public trajectory_t apos;	// for calculating angles

            public int time;
            public int time2;

            public Vector3 origin;
            public Vector3 origin2;

            public Vector3 angles;
            public Vector3 angles2;

            public int otherEntityNum;	// shotgun sources, etc
            public int otherEntityNum2;

            public int groundEntityNum;	// -1 = in air

            public int modelindex;
            public int clientNum;		// 0 to (MAX_CLIENTS - 1), for players and corpses
            public int frame;

            public int solid;			// for client side prediction, trap_linkentity sets this properly
            public int generic1;
        }

        public struct trajectory_t
        {
            public trType_t trType;
            public int trTime;
            public int trDuration;			// if non 0, trTime + trDuration = stop time
            public Vector3 trBase;
            public Vector3 trDelta;			// velocity, etc
        }

        public enum trType_t
        {
            TR_STATIONARY,
            TR_INTERPOLATE,				// non-parametric, but interpolate between snapshots
            TR_LINEAR,
            TR_LINEAR_STOP,
            TR_SINE,					// value = base + sin( time / duration ) * delta
            TR_GRAVITY
        }

        public struct cmodel_t {
        	public Vector3		mins, maxs;
            public cLeaf_t leaf;			// submodels don't reference the main tree
        }

        public struct cLeaf_t
        {
            public int cluster;
            public int area;

            public int firstLeafBrush;
            public int numLeafBrushes;

            public int firstLeafSurface;
            public int numLeafSurfaces;
        }

        public struct sysEvent_t {
            public float evTime;
            public sysEventType_t evType;
            public int evValue, evValue2;
            public int dataSize;
            public object data;			// this must be manually freed if not NULL
        }
    }

    //
    // config strings are a general means of communicating variable length strings
    // from the server to all connected clients.
    //
    public enum ConfigString : int
    {
        CS_SERVERINFO       = 0,
        CS_SYSTEMINFO       = 1,
        CS_MUSIC		=		2,
        CS_MESSAGE		=		3,		// from the map worldspawn's message field
        CS_MOTD			=		4,		// g_motd string for server message of the day
        CS_WARMUP		=		5,		// server time when the match will be restarted
        CS_SCORES1		=		6,
        CS_SCORES2		=		7,
        CS_VOTE_TIME		=	8,
        CS_VOTE_STRING	=		9,
        CS_VOTE_YES		=		10,
        CS_VOTE_NO		=		11,

        CS_TEAMVOTE_TIME	=	12,
        CS_TEAMVOTE_STRING	=	14,
        CS_TEAMVOTE_YES		=	16,
        CS_TEAMVOTE_NO		=	18,

        CS_GAME_VERSION		=	20,
        CS_LEVEL_START_TIME	=	21,		// so the timer only shows the current level
        CS_INTERMISSION		=	22,		// when 1, fraglimit/timelimit has been hit and intermission will start in a second or two
        CS_FLAGSTATUS		=	23,		// string indicating flag status in CTF
        CS_SHADERSTATE	=		24,
        CS_BOTINFO		=		25,

        CS_ITEMS		=		27,		// string of 0's and 1's that tell which items are present

        CS_MODELS		=		32,
        CS_SOUNDS		=		(CS_MODELS+256),
        CS_PLAYERS = (CS_SOUNDS + 256),
        CS_LOCATIONS		=	(CS_PLAYERS+64),
        CS_PARTICLES		=	(CS_LOCATIONS+64) ,

        CS_MAX			=		(CS_PARTICLES+64)
    }
}
