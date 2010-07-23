using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;

namespace CubeHags.client.cgame
{
    public sealed partial class CGame
    {
        public static void PlayerStateToEntityState(Common.PlayerState ps, Common.entityState_t s, bool snap)
        {
            if (ps.pm_type == Common.PMType.INTERMISSION || ps.pm_type == Common.PMType.SPECTATOR)
                s.eType = 10; // ET_INVISIBLE
            else
                s.eType = 1; // ET_PLAYER

            s.number = ps.clientNum;
            s.pos.trType = Common.trType_t.TR_INTERPOLATE;
            s.pos.trBase = ps.origin;
            if (snap)
            {
                s.pos.trBase = SnapVector(s.pos.trBase);
            }

            // set the trDelta for flag direction
            s.pos.trDelta = ps.velocity;

            s.apos.trType = Common.trType_t.TR_INTERPOLATE;
            s.apos.trBase = ps.viewangles;
            if (snap)
            {
                s.apos.trBase = SnapVector(s.apos.trBase);
            }

            s.angles2[1] = ps.movementDir;
            //s.legsAnim = ps.legsAnim;
            //s.torsoAnim = ps.torsoAnim;
            s.clientNum = ps.clientNum; // ET_PLAYER looks here instead of at number    
            // so corpses can also reference the proper config
            s.eFlags = ps.eFlags;

            // FIX: Add event

            //s.weapon = ps.weapon;
            s.groundEntityNum = ps.groundEntityNum;
            //s.powerups = 0;
            //s.loopSound = ps.loopSound;
            //s.generic1 = ps.generic1;
        }

        void TransitionPlayerState(Common.PlayerState ps, Common.PlayerState ops)
        {
            // check for changing follow mode
            if (ps.clientNum != ops.clientNum)
            {
                cg.thisFrameTeleport = true;
                // make sure we don't get any unwanted transition effects
                ops = ps;
            }

            // damage events (player is getting wounded)
            //if (ps.damageEvent != ops.damageEvent && ps.damageCount != 0)
            {
                // Todo: DamageFeedback
            }

            // respawning
            if (ps.persistant[4] != ops.persistant[4])
            {
                // Todo: Respawn
            }

            if (cg.snap.ps.pm_type != Common.PMType.INTERMISSION && ps.persistant[3] != 3)
            {
                // ? Check localsounds
            }

            // Check ammo

            // run events

            // smooth the ducking viewheight change
            if (ps.viewheight != ops.viewheight)
            {
                cg.duckChange = ps.viewheight - ops.viewheight;
                cg.duckTime = cg.time;
            }
        }
    }
}
