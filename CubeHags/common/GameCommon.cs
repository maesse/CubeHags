using System;
using System.Collections.Generic;
 
using System.Text;

namespace CubeHags.common
{

    public enum SayMode : int
    {
        ALL = 0,
        TEAM = 1,
        TELL = 2
    }

    public enum team_t 
    {
    	TEAM_FREE,
    	TEAM_RED,
    	TEAM_BLUE,
    	TEAM_SPECTATOR,

    	TEAM_NUM_TEAMS
    } 

    // player_state->persistant[] indexes
    // these fields are the only part of player_state that isn't
    // cleared on respawn
    // NOTE: may not have more than 16
    public enum persEnum_t : int
    {
    	PERS_SCORE= 0,						// !!! MUST NOT CHANGE, SERVER AND GAME BOTH REFERENCE !!!
    	PERS_HITS,						// total points damage inflicted so damage beeps can sound on change
    	PERS_RANK,						// player rank or team rank
    	PERS_TEAM,						// player team
    	PERS_SPAWN_COUNT,				// incremented every respawn
    	PERS_PLAYEREVENTS,				// 16 bits that can be flipped for events
    	PERS_ATTACKER,					// clientnum of last damage inflicter
    	PERS_ATTACKEE_ARMOR,			// health/armor of last person we attacked
    	PERS_KILLED,					// count of the number of times you died
    	// player awards tracking
    	PERS_IMPRESSIVE_COUNT,			// two railgun hits in a row
    	PERS_EXCELLENT_COUNT,			// two successive kills in a short amount of time
    	PERS_DEFEND_COUNT,				// defend awards
    	PERS_ASSIST_COUNT,				// assist awards
    	PERS_GAUNTLET_FRAG_COUNT,		// kills with the guantlet
    	PERS_CAPTURES					// captures
    }
}
