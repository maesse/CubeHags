using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using SlimDX;
using CubeHags.common;
using CubeHags.client;

namespace CubeHags.server
{
    public struct level_locals_t{
    	public List<gclient_t>	clients;		// [maxclients]

    	public List<gentity_t>	gentities;
    	public int			gentitySize;
    	public int			num_entities;		// current number, <= MAX_GENTITIES

    	public int			warmupTime;			// restart match at this time

    	public StreamWriter	logFile;

    	// store latched cvars here that we want to get at often
    	public int			maxclients;

    	public int			framenum;
    	public float			time;					// in msec
    	public float			previousTime;			// so movers can back up when blocked

    	public float			startTime;				// level.time the map was started

    	public int[]			teamScores; // 2
    	public int			lastTeamLocationTime;		// last time of client team location update

    	public bool	newSession;				// don't use any old session data, because
    										// we changed gametype

    	public bool	restarted;				// waiting for a map_restart to fire

    	public int			numConnectedClients;
    	public int			numNonSpectatorClients;	// includes connecting clients
    	public int			numPlayingClients;		// connected, non-spectators
    	public int[]			sortedClients;		// 64 sorted by score
    	public int			follow1, follow2;		// clientNums for auto-follow spectators

    	public int			snd_fry;				// sound index for standing in lava

    	public int			warmupModificationCount;	// for detecting if g_warmup is changed

    	// voting state
    	public string		voteString;
    	public string		voteDisplayString;
    	public int			voteTime;				// level.time vote was called
    	public int			voteExecuteTime;		// time the vote is executed
    	public int			voteYes;
    	public int			voteNo;
    	public int			numVotingClients;		// set by CalculateRanks

    	// team voting state
    	public string[]		teamVoteString; // 2
    	public int[]			teamVoteTime;		// level.time vote was called
    	public int[]			teamVoteYes;
    	public int[]			teamVoteNo;
    	public int[]			numteamVotingClients;// set by CalculateRanks

    	// spawn variables
    	public bool	spawning;				// the G_Spawn*() functions are valid
        public List<KeyValuePair<string, string>> spawnVars;

    	// intermission state
    	public int			intermissionQueued;		// intermission was qualified, but
    										// wait INTERMISSION_DELAY_TIME before
    										// actually going there so the last
    										// frag can be watched.  Disable future
    										// kills during this delay
    	public int			intermissiontime;		// time the intermission was started
    	public string		changemap;
    	public bool	readyToExit;			// at least one client wants to exit
    	public int			exitTime;
    	public Vector3		intermission_origin;	// also used for spectator spawns
    	public Vector3		intermission_angle;

    	public bool	locationLinked;			// target_locations get linked
        public List<gentity_t> locations;
    	//public gentity_t	*locationHead;			// head of the location list
    	public int			bodyQueIndex;			// dead bodies
    	public gentity_t[]	bodyQue; // 8
    }

    [Flags]
    public enum gentityFlags : int
    {
        FL_NONE             =   0x00000000,
        FL_GODMODE			=	0x00000010,
        FL_NOTARGET			=	0x00000020,
        FL_TEAMSLAVE		=	0x00000400,	// not the first on the team
        FL_NO_KNOCKBACK		=	0x00000800,
        FL_DROPPED_ITEM		=	0x00001000,
        FL_NO_BOTS			=	0x00002000,	// spawn point not for bot use
        FL_NO_HUMANS		=	0x00004000,	// spawn point just for bots
        FL_FORCE_GESTURE	=	0x00008000	// force gesture on client
    }

    // the server looks at a sharedEntity, which is the start of the game's gentity_t structure
    public class sharedEntity
    {
        public sharedEntity()
        {
            s = new Common.entityState_t();
            r = new Common.entityShared_t();
        }
        public Common.entityState_t s;  // communicated by server to clients
        public Common.entityShared_t r;     // shared by both the server system and game
    }

    public class gentity_t {
        public gentity_t()
        {
            shEnt = new sharedEntity();
        }
        public sharedEntity shEnt;
        public Common.entityState_t s { get { return shEnt.s; } }
        public Common.entityShared_t r { get { return shEnt.r; } }
        
    	//public Common.entityState_t	s;				// communicated by server to clients
    	//public Common.entityShared_t	r;				// shared by both the server system and game

    	// DO NOT MODIFY ANYTHING ABOVE THIS, THE SERVER
    	// EXPECTS THE FIELDS IN THAT ORDER!
    	//================================

    	public gclient_t	client;			// NULL if not a client

    	public bool	inuse;

    	public string      classname;			// set in QuakeEd
    	public int			spawnflags;			// set in QuakeEd

    	public bool	neverFree;			// if true, FreeEntity will only unlink
    									// bodyque uses this

        public gentityFlags flags;				// FL_* variables

    	public string model;
    	public string		model2;
    	public float			freetime;			// level.time when the object was freed
    	
    	public int			eventTime;			// events will be cleared EVENT_VALID_MSEC after set
    	public bool	freeAfterEvent;
    	public bool	unlinkAfterEvent;

    	public bool	physicsObject;		// if true, it can be pushed by movers and fall off edges
    									// all game items are physicsObjects, 
    	public float		physicsBounce;		// 1.0 = continuous bounce, 0.0 = no bounce
    	public int			clipmask;			// brushes with this content value will be collided against
    									// when moving.  items and corpses do not collide against
    									// players, for instance

    	// movers
    	public moverState_t moverState;
    	public int			soundPos1;
    	public int			sound1to2;
    	public int			sound2to1;
    	public int			soundPos2;
    	public int			soundLoop;
        public gentity_t	parent;
        public gentity_t	nextTrain;
        public gentity_t	prevTrain;
    	public Vector3		pos1, pos2;

    	public string		message;

    	public int			timestamp;		// body queue sinking, etc

    	public float		angle;			// set in editor, -1 = up, -2 = down
    	public string		target;
    	public string		targetname;
    	public string		team;
    	public string		targetShaderName;
    	public string		targetShaderNewName;
    	public gentity_t	target_ent;

    	public float		speed;
    	public Vector3		movedir;

    	public int			nextthink;
        public event        ThinkDelegate think;
        public void RunThink(gentity_t ent) { think(ent); }
        //void		(*reached)(gentity_t *self);	// movers call this when hitting endpoint
        //void		(*blocked)(gentity_t *self, gentity_t *other);
        //void		(*touch)(gentity_t *self, gentity_t *other, trace_t *trace);
        //void		(*use)(gentity_t *self, gentity_t *other, gentity_t *activator);
        //void		(*pain)(gentity_t *self, gentity_t *attacker, int damage);
        //void		(*die)(gentity_t *self, gentity_t *inflictor, gentity_t *attacker, int damage, int mod);

    	public int			pain_debounce_time;
    	public int			fly_sound_debounce_time;	// wind tunnel
    	public int			last_move_time;

    	public int			health;

    	public bool	takedamage;

    	public int			damage;
    	public int			splashDamage;	// quad will increase this without increasing radius
    	public int			splashRadius;
    	public int			methodOfDeath;
    	public int			splashMethodOfDeath;

    	public int			count;

    	public gentity_t	chain;
    	public gentity_t	enemy;
    	public gentity_t	activator;
    	public gentity_t	teamchain;		// next entity in team
    	public gentity_t	teammaster;	// master of the team

    	public int			watertype;
    	public int			waterlevel;

    	public int			noise_index;

    	// timing variables
    	public float		wait;
    	public float		random;

    	//gitem_t		*item;			// for bonus items
    }

    // movers are things like doors, plats, buttons, etc
    public enum moverState_t
    {
    	MOVER_POS1,
    	MOVER_POS2,
    	MOVER_1TO2,
    	MOVER_2TO1
    } 

    // this structure is cleared on each ClientSpawn(),
    // except for 'client->pers' and 'client->sess'
    public class gclient_t {

    	// ps MUST be the first element, because the server expects it
        public Common.playerState_t ps = new Common.playerState_t();				// communicated by server to clients

    	// the rest of the structure is private to game
    	public clientPersistant_t	pers;
    	public clientSession_t		sess;

    	public bool	readyToExit;		// wishes to leave the intermission

    	public bool	noclip;

    	public int			lastCmdTime;		// level.time of last usercmd_t, for EF_CONNECTION
    									// we can't just use pers.lastCommand.time, because
    									// of the g_sycronousclients case
    	public int			buttons;
    	public int			oldbuttons;
    	public int			latched_buttons;

    	public Vector3		oldOrigin;

    	// sum up damage over an entire frame, so
    	// shotgun blasts give a single big kick
    	public int			damage_armor;		// damage absorbed by armor
    	public int			damage_blood;		// damage taken out of health
    	public int			damage_knockback;	// impact damage
    	public Vector3		damage_from;		// origin for vector calculation
    	public bool	damage_fromWorld;	// if true, don't use the damage_from vector

    	public int			accurateCount;		// for "impressive" reward sound

    	public int			accuracy_shots;		// total number of shots
    	public int			accuracy_hits;		// total number of hits

    	//
    	public int			lastkilled_client;	// last client that this client killed
    	public int			lasthurt_client;	// last client that damaged this client
    	public int			lasthurt_mod;		// type of damage the client did

    	// timers
    	public int			respawnTime;		// can respawn when time > this, force after g_forcerespwan
    	public int			inactivityTime;		// kick players when time > this
    	public bool	inactivityWarning;	// qtrue if the five seoond warning has been given
    	public int			rewardTime;			// clear the EF_AWARD_IMPRESSIVE, etc when time > this

    	public int			airOutTime;

    	public int			lastKillTime;		// for multiple kill rewards

    	public bool	fireHeld;			// used for hook
    	//gentity_t	*hook;				// grapple hook if out

    	public int			switchTeamTime;		// time the player switched teams

    	// timeResidual is used to handle events that happen every second
    	// like health / armor countdowns and regeneration
    	public int			timeResidual;

    	public char[]		areabits;
    }

    // client data that stays across multiple respawns, but is cleared
    // on each level change or team change at ClientBegin()
    public struct clientPersistant_t {
    	public clientConnected_t	connected;	
    	public Input.UserCommand	cmd;				// we would lose angles if not persistant
    	public bool	localClient;		// true if "ip" info key is "localhost"
    	public bool	initialSpawn;		// the first spawn should be at a cool location
    	public bool	predictItemPickup;	// based on cg_predictItems userinfo
    	public bool	pmoveFixed;			//
    	public string		netname; // 36
    	public int			maxHealth;			// for handicapping
    	public int			enterTime;			// level.time the client entered the game
    	public playerTeamState_t teamState;	// status in teamplay games
    	public int			voteCount;			// to prevent people from constantly calling votes
    	public int			teamVoteCount;		// to prevent people from constantly calling votes
    	public bool	teamInfo;			// send team overlay updates?
    } 

    public enum clientConnected_t {
    	CON_DISCONNECTED,
    	CON_CONNECTING,
    	CON_CONNECTED
    } 

    public struct playerTeamState_t{
    	public playerTeamStateState_t	state;

    	public int			location;

    	public int			captures;
    	public int			basedefense;
    	public int			carrierdefense;
    	public int			flagrecovery;
    	public int			fragcarrier;
    	public int			assists;

    	public float		lasthurtcarrier;
    	public float		lastreturnedflag;
    	public float		flagsince;
    	public float		lastfraggedcarrier;
    } 

    public enum playerTeamStateState_t{
    	TEAM_BEGIN,		// Beginning a team game, spawn at base
    	TEAM_ACTIVE		// Now actively playing
    } 

    // client data that stays across multiple levels or tournament restarts
    // this is achieved by writing all the data to cvar strings at game shutdown
    // time and reading them back at connection time.  Anything added here
    // MUST be dealt with in G_InitSessionData() / G_ReadSessionData() / G_WriteSessionData()
    public struct clientSession_t
    {
    	public team_t		sessionTeam;
        public int spectatorTime;		// for determining next-in-line to play
        public spectatorState_t spectatorState;
        public int spectatorClient;	// for chasecam and follow mode
        public int wins, losses;		// tournament stats
        public bool teamLeader;			// true when this client is a team leader
    }

    public enum spectatorState_t
    {
    	SPECTATOR_NOT,
    	SPECTATOR_FREE,
    	SPECTATOR_FOLLOW,
    	SPECTATOR_SCOREBOARD
    } 
}
