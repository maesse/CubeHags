using System;
using System.Collections.Generic;
 
using System.Text;
using System.Net;
using CubeHags.common;
using SlimDX;
using CubeHags.client.map.Source;
using CubeHags.client.render;
using CubeHags.server;
using CubeHags.client.cgame;

namespace CubeHags.client
{
    // snapshots are a view of the server at a given time

    // Snapshots are generated at regular time intervals by the server,
    // but they may not be sent if a client's rate level is exceeded, or
    // they may be dropped by the network.
    public class snapshot_t {
    	public int				snapFlags;			// SNAPFLAG_RATE_DELAYED, etc
    	public int				ping;

    	public int				serverTime;		// server time the message is valid for (in msec)

    	public byte[]			areamask = new byte[32];		// 32 portalarea visibility bits

    	public CubeHags.common.Common.PlayerState	ps;						// complete information about the current player at this time

    	public int				numEntities;			// all of the entities that need to be presented
    	public CubeHags.common.Common.entityState_t[]	entities;	// 256 at the time of this snapshot

    	public int				numServerCommands;		// text based server commands to execute when this
    	public int				serverCommandSequence;	// snapshot becomes current
    } 

    

    // each client has an associated clientInfo_t
    // that contains media references necessary to present the
    // client model and other color coded effects
    // this is regenerated each time a client's configstring changes,
    // usually as a result of a userinfo (name, model, etc) change
    // #define	MAX_CUSTOM_SOUNDS	32

    public struct clientInfo_t {
    	bool		infoValid;

    	string			name;
    	team_t			team;

    	Vector3			color1;
    	Vector3			color2;

    	int				score;			// updated by score servercmds
    	int				location;		// location index for team mode
    	int				health;			// you only get this info about your teammates
    	int				armor;
    	int				curWeapon;

        //int				handicap;
        //int				wins, losses;	// in tourney mode

        //int				teamTask;		// task in teamplay (offence/defence)
        //bool		teamLeader;		// true when this is a team leader

        //int				powerups;		// so can display quad/flag status

        //int				medkitUsageTime;
        //int				invulnerabilityStartTime;
        //int				invulnerabilityStopTime;

        //int				breathPuffTime;

    	// when clientinfo is changed, the loading of models/skins/sounds
    	// can be deferred until you are dead, to prevent hitches in
    	// gameplay
    	string			modelName;
        //char			skinName[MAX_QPATH];
        //char			headModelName[MAX_QPATH];
        //char			headSkinName[MAX_QPATH];
        //char			redTeam[MAX_TEAMNAME];
        //char			blueTeam[MAX_TEAMNAME];
        //bool		deferred;

        //bool		newAnims;		// true if using the new mission pack animations
        //bool		fixedlegs;		// true if legs yaw is always the same as torso yaw
        //bool		fixedtorso;		// true if torso never changes yaw

        //Vector3			headOffset;		// move head in icon views
        //footstep_t		footsteps;
        //gender_t		gender;			// from model

        //qhandle_t		legsModel;
        //qhandle_t		legsSkin;

        //qhandle_t		torsoModel;
        //qhandle_t		torsoSkin;

        //qhandle_t		headModel;
        //qhandle_t		headSkin;

        //qhandle_t		modelIcon;

        //animation_t		animations[MAX_TOTALANIMATIONS];

        //sfxHandle_t		sounds[MAX_CUSTOM_SOUNDS];
    }

    // centity_t have a direct corespondence with gentity_t in the game, but
    // only the entityState_t is directly communicated to the cgame
    public class centity_t {
    	public Common.entityState_t	currentState = new Common.entityState_t();	// from cg.frame
        public Common.entityState_t nextState = new Common.entityState_t();		// from cg.nextFrame, if available
        public bool interpolate;	// true if next is valid to interpolate to
        public bool currentValid;	// true if cg.frame holds this entity

        public int muzzleFlashTime;	// move to playerEntity?
        public int previousEvent;
        public int teleportFlag;

        public int trailTime;		// so missile trails can handle dropped initial packets
        public int dustTrailTime;
        public int miscTime;

        public int snapShotTime;	// last time this entity was found in a snapshot

        //public playerEntity_t pe;

        public int errorTime;		// decay the error from this time
        public Vector3 errorOrigin;
        public Vector3 errorAngles;

        public bool extrapolated;	// false if origin / angles is an interpolation
        public Vector3 rawOrigin;
        public Vector3 rawAngles;

        public Vector3 beamEnd;

    	// exact interpolated position of entity on this frame
        public Vector3 lerpOrigin;
        public Vector3 lerpAngles;
    }

    // pmove->pm_flags
    [Flags]
    public enum PMFlags
    {
        DUCKED = 1,
        JUMP_HELD = 2,
        BACKWARDS_JUMP = 8,		// go into backwards land
        BACKWARDS_RUN = 16,		// coast down to backwards run
        TIME_LAND = 32,		// pm_time is time before rejump
        TIME_KNOCKBACK = 64,		// pm_time is an air-accelerate only time
        TIME_WATERJUMP = 256,		// pm_time is waterjump
        RESPAWNED = 512,		// clear after attack and jump buttons come up
        USE_ITEM_HELD = 1024,
        GRAPPLE_PULL = 2048,	// pull towards grapple location
        FOLLOW = 4096,	// spectate following another player
        SCOREBOARD = 8192,	// spectate as a scoreboard
        INVULEXPAND = 16384,	// invulnerability sphere set to full size

        ALL_TIMES = (TIME_WATERJUMP | TIME_LAND | TIME_KNOCKBACK)
    }

    public delegate trace_t TraceDelegate(Vector3 start, Vector3 end, Vector3 maxs, Vector3 mins, int passEntityNum, int contentMask);
    public delegate void TraceContentsDelegate(Vector3 point, int passEntityNum);

    public class pmove_t
    {
        // state (in / out)
        public CubeHags.common.Common.PlayerState ps;

        // command (in)
        public Input.UserCommand cmd;
        public int tracemask;			// collide against these types of surfaces
        public int debugLevel;			// if set, diagnostic output will be printed
        //public bool	noFootsteps;		// if the game is setup for no footsteps by the server
        //public bool	gauntletHit;		// true if a gauntlet attack would actually hit something

        public int framecount;

        // results (out)
        public int numtouch;
        public int[] touchents = new int[32];

        public Vector3 mins, maxs;			// bounding box size

        //int			watertype;
        //int			waterlevel;

        public float xyspeed;

        // for fixed msec Pmove
        public int pmove_fixed;
        public int pmove_msec;
        public TraceDelegate Trace;
        // callbacks to test the world
        // these will be different functions during game and cgame
        public trace_t DoTrace(Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, int passEntityNum, int contentMask) 
        {
            return Trace(start, end, mins, maxs, passEntityNum, contentMask);
            //return ClipMap.Instance.Box_Trace(start, end, mins, maxs, 0, contentMask, 0);
            //return ClipMap.Instance.Box_Trace( start, end, maxs, mins, passEntityNum, contentMask); 
        }
        //public void DoPointContents(Vector3 point, int passEntityNum) { PointContents( point,  passEntityNum); }
    }

    // a trace is returned when a box is swept through the world
    public class trace_t
    {
        public bool allsolid;	// if true, plane is not valid
        public bool startsolid;	// if true, the initial point was in a solid area
        public float fraction;	// time completed, 1.0 = didn't hit anything
        public Vector3 endpos;		// final position
        public cplane_t plane = new cplane_t();		// surface normal at impact, transformed to world space
        public int surfaceFlags;	// surface hit
        public int contents;	// contents on other side of surface hit
        public int entityNum;	// entity the contacted sirface is a part of
        public float fractionleftsolid;
    }

    // The client game static (cgs) structure hold everything
    // loaded or calculated from the gamestate.  It will NOT
    // be cleared when a tournement restart is done, allowing
    // all clients to begin playing instantly
    public struct cgs_t
    {
        public gameState_t gameState;			// gamestate from server
        //glconfig_t		glconfig;			// rendering configuration
        //float screenXScale;		// derived from glconfig
        //float screenYScale;
        //float screenXBias;

        public int serverCommandSequence;	// reliable command stream counter
        public int processedSnapshotNum;// the number of snapshots cgame has requested

        //bool localServer;		// detected on startup by checking sv_running

        // parsed from serverinfo
        public int				maxclients;
        public string			mapname;
        //char			redTeam[MAX_QPATH];
        //char			blueTeam[MAX_QPATH];

        public int				levelStartTime;

        //
        // locally derived information from gamestate
        //
        //qhandle_t		gameModels[MAX_MODELS];
        //sfxHandle_t		gameSounds[MAX_SOUNDS];

        //int				numInlineModels;
        //qhandle_t		inlineDrawModel[MAX_MODELS];
        //vec3_t			inlineModelMidpoints[MAX_MODELS];

        public clientInfo_t[] clientinfo; // 64

        //int cursorX;
        //int cursorY;
        //qboolean mouseCaptured;
    } 

    public class cg_t {
    	public int			clientFrame;		// incremented each frame

    	public int			clientNum;
    	
    	bool	demoPlayback;
    	bool	levelShot;			// taking a level menu screenshot
    	int			deferredPlayerLoading;
    	public bool	loading;			// don't defer players at initial startup
    	bool	intermissionStarted;	// don't play voice rewards, because game will end shortly

    	// there are only one or two snapshot_t that are relevent at a time
    	public int			latestSnapshotNum;	// the number of snapshots the client system has received
        public int latestSnapshotTime;	// the time from latestSnapshotNum, so we don't need to read the snapshot yet

    	public snapshot_t	snap;				// cg.snap->serverTime <= cg.time
    	public snapshot_t	nextSnap;			// cg.nextSnap->serverTime > cg.time, or NULL
    	public snapshot_t[]	activeSnapshots = new snapshot_t[2]; // 2

    	float		frameInterpolation;	// (float)( cg.time - cg.frame->serverTime ) / (cg.nextFrame->serverTime - cg.frame->serverTime)

    	public bool	thisFrameTeleport;
        public bool nextFrameTeleport;

    	int			frametime;		// cg.time - cg.oldTime

    	public int			time;			// this is the time value that the client
    								// is rendering at.
    	public int			oldTime;		// time at last frame, used for missile trails and prediction checking

    	public int			physicsTime;	// either cg.snap->time or cg.nextSnap->time

    	int			timelimitWarnings;	// 5 min, 1 min, overtime
    	int			fraglimitWarnings;

    	bool	mapRestart;			// set on a map restart to set back the weapon

    	bool	renderingThirdPerson;		// during deaths, chasecams, etc

    	// prediction state
    	bool	hyperspace;				// true if prediction has hit a trigger_teleport

    	public CubeHags.common.Common.PlayerState	predictedPlayerState;
    	public bool	validPPS;				// clear until the first call to CG_PredictPlayerState
    	public int			predictedErrorTime;
    	public Vector3		predictedError;

    	int			eventSequence;
    	int[]			predictableEvents = new int[16]; // 16

        //float		stepChange;				// for stair up smoothing
        //int			stepTime;

        public float		duckChange;				// for duck viewheight smoothing
        public int			duckTime;

        //float		landChange;				// for landing hard
        //int			landTime;

    	// input state sent to server
    	//int			weaponSelect;

    	// view rendering
        public float xyspeed;
        public ViewParams refdef;
        public Vector3 refdefViewAngles;
    	//refdef_t	refdef;
    	//vec3_t		refdefViewAngles;		// will be converted to refdef.viewaxis

    } 

    public class clientConnect
    {
        public int clientNum;
        public int lastPacketSentTime;  // for retransmits during connection
        public int lastPacketTime;      // for timeouts

        public IPEndPoint serverAddress;
        public int connectTime;         // for connection retransmits
        public int connectPacketCount;
        public string serverMessage;    // for display on connection dialog

        public int challenge;           // from the server to use for connecting

        // these are our reliable messages that go to the server
        public int reliableSequence;
        public int reliableAcknowledge; // the last one the server has executed
        public string[] reliableCommands = new string[64]; // 64

        // server message (unreliable) and command (reliable) sequence
        // numbers are NOT cleared at level changes, but continue to
        // increase as long as the connection is valid

        // message sequence is used by both the network layer and the
        // delta compression layer
        public int serverMessageSequence;

        // reliable messages received from server
        public int serverCommandSequence;
        public int lastExecutedServerCommand;		// last server command grabbed or executed with CL_GetServerCommand
        public string[] serverCommands = new string[64]; // 64
        public Net.netchan_t netchan;
    }

    public class clientActive
    {
        public clientActive()
        {
            gamestate.data = new Dictionary<int, string>();
            for (int i = 0; i < parseEntities.Length; i++)
            {
                parseEntities[i] = new Common.entityState_t();
            }
            for (int i = 0; i < entityBaselines.Length; i++)
            {
                entityBaselines[i] = new Common.entityState_t();
            }
            for (int i = 0; i < snapshots.Length; i++)
            {
                snapshots[i] = new clSnapshot_t();
            }
        }
        public int timeoutCount;        // it requres several frames in a timeout condition
        // to disconnect, preventing debugging breaks from
        // causing immediate disconnects on continue

        public clSnapshot_t snap = new clSnapshot_t();       // latest received from server
        public int serverTime;          // may be paused during play
        public int oldServerTime;       // to prevent time from flowing bakcwards
        public int oldFrameServerTime;  // to check tournament restarts
        public int serverTimeDelta;     // cl.serverTime = cls.realtime + cl.serverTimeDelta
        // this value changes as net lag varies
        public bool extrapolatedSnapshot;// set if any cgame frame has been forced to extrapolate
        // cleared when CL_AdjustTimeDelta looks at it
        public bool newSnapshots;       // set on parse of any valid packet

        public gameState_t gamestate;   // configstrings

        public string mapname;          // extracted from CS_SERVERINFO
        public int parseEntitiesNum;    // index (not anded off) into cl_parse_entities[]

        public int[] mouseDX;           // added to by mouse events
        public int[] mouseDY;
        public int mouseindex;

        // cgame communicates a few values to the client system
        public int cgameUserCmdValue; // current weapon to add to usercmd_t
        public float cgameSensitivity;

        // cmds[cmdNumber] is the predicted command, [cmdNumber-1] is the last
        // properly generated command
        public CubeHags.client.Input.UserCommand[] cmds = new Input.UserCommand[64]; // 64  // each mesage will send several old cmds
        public int cmdNumber;                                   // incremented each frame, because multiple
        // frames may need to be packed into a single packet


        public outPacket_t[] outPackets = new outPacket_t[32]; // 32      // information about each packet we have sent out


        // the client maintains its own idea of view angles, which are
        // sent to the server each frame.  It is cleared to 0 upon entering each level.
        // the server sends a delta each frame which is added to the locally
        // tracked view angles to account for standing on rotating objects,
        // and teleport direction changes
        public Vector3 viewAngles;
        public int serverId;        // included in each client message so the server
        // can tell if it is for a prior map_restart

        public clSnapshot_t[] snapshots = new clSnapshot_t[32]; // 32
        public Common.entityState_t[] entityBaselines = new Common.entityState_t[1024]; // 1<<10 --- 1024?
        public Common.entityState_t[] parseEntities = new Common.entityState_t[2048]; // 2048


    }

    public struct outPacket_t
    {
        public int p_cmdNumber;		// cl.cmdNumber when packet was sent
        public int p_serverTime;		// usercmd->serverTime when packet was sent
        public int p_realtime;			// cls.realtime when packet was sent
    }

    public class clSnapshot_t
    {
        public bool valid;              // cleared if delta parsing was invalid
        public int snapFlags;           // rate delayed and dropped commands
        public int serverTime;          // server time the message is valid for (in msec)
        public int messageNum;          // copied from netchan->incoming_sequence
        public int deltaNum;            // messageNum the delta is from
        public int ping;                // time from when cmdNum-1 was sent to time packet was reeceived
        public byte[] areamask;         // 32 portalarea visibility bits
        public int cmdNum;              // the next cmdNum the server is expecting
        public Common.PlayerState ps = new Common.PlayerState();        // complete information about the current player at this time

        public int numEntities;         // all of the entities that need to be presented
        public int parseEntitiesNum;    // at the time of this snapshot
        public int ServerCommandNum;    // execute all commands up to this before
        // making the snapshot current
    }

    public struct serverInfo_t
    {
        public IPAddress adr;
        public string hostName;
        public string mapName;
        public string game;
        public int netType;
        public int gameType;
        public int clients;
        public int maxClients;
        public int minPing;
        public int maxPing;
        public int ping;
        public bool visible;
        public int punkbuster;
    }

    public struct gameState_t
    {
        public Dictionary<int, string> data;
    }

    public class Lagometer 
    {
        public const int LAGBUFFER = 128;
        public int[] frameSamples = new int[LAGBUFFER];
	    public int		frameCount;
        public int[] snapshotFlags = new int[LAGBUFFER];
        public int[] snapshotSamples = new int[LAGBUFFER];
	    public int		snapshotCount;
    } 
}
