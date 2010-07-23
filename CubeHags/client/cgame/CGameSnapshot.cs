using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;

namespace CubeHags.client.cgame
{
    public sealed partial class CGame
    {
        /*
        ============
        CG_ProcessSnapshots

        We are trying to set up a renderable view, so determine
        what the simulated time is, and try to get snapshots
        both before and after that time if available.

        If we don't have a valid cg.snap after exiting this function,
        then a 3D game view cannot be rendered.  This should only happen
        right after the initial connection.  After cg.snap has been valid
        once, it will never turn invalid.

        Even if cg.snap is valid, cg.nextSnap may not be, if the snapshot
        hasn't arrived yet (it becomes an extrapolating situation instead
        of an interpolating one)

        ============
        */
        private void ProcessSnapshots()
        {
            // see what the latest snapshot the client system has is
            
            cg.latestSnapshotTime = Client.Instance.cl.snap.serverTime;
            int n = Client.Instance.cl.snap.messageNum;
            if (n != cg.latestSnapshotNum)
            {
                if (n < cg.latestSnapshotNum)
                {
                    // this should never happen
                    Common.Instance.Error("ProcessSnapshots(): n < cg.latestSnapshotNum");
                }
                cg.latestSnapshotNum = n;
            }

            // If we have yet to receive a snapshot, check for it.
            // Once we have gotten the first snapshot, cg.snap will
            // always have valid data for the rest of the game
            while (cg.snap == null)
            {
                snapshot_t snap = ReadNextSnapshot();
                if (snap == null)
                {
                    // we can't continue until we get a snapshot
                    return;
                }
                // set our weapon selection to what
                // the playerstate is currently using
                if ((snap.snapFlags & 2) != 2)
                {
                    SetInitialSnapshot(snap);
                }
            }

            // loop until we either have a valid nextSnap with a serverTime
            // greater than cg.time to interpolate towards, or we run
            // out of available snapshots
            do
            {
                // if we don't have a nextframe, try and read a new one in
                if (cg.nextSnap == null)
                {
                    snapshot_t snap = ReadNextSnapshot();

                    // if we still don't have a nextframe, we will just have to
                    // extrapolate
                    if (snap == null)
                        break;

                    SetNextSnap(snap);

                    // if time went backwards, we have a level restart
                    if (cg.nextSnap.serverTime < cg.snap.serverTime)
                    {
                        Common.Instance.Error("ProcessSnapshots: Server went back in time");
                    }
                }

                // if our time is < nextFrame's, we have a nice interpolating state
                if (cg.time >= cg.snap.serverTime && cg.time < cg.nextSnap.serverTime)
                {
                    break;
                }

                // we have passed the transition from nextFrame to frame
                TransitionSnapshot();
            } while (true);

            // assert our valid conditions upon exiting
            if (cg.snap == null)
                Common.Instance.Error("ProcessSnapshots: cg.snap == NULL");

            if (cg.time < cg.snap.serverTime)
            {
                cg.time = cg.snap.serverTime;
            }

            if (cg.nextSnap != null && cg.nextSnap.serverTime <= cg.time)
                Common.Instance.Error("ProcessSnapshots: cg.nextSnap->serverTime <= cg.time");
        }

        snapshot_t ReadNextSnapshot()
        {
            if (cg.latestSnapshotNum > cgs.processedSnapshotNum + 1000)
            {
                Common.Instance.WriteLine("WARNING: ReadNextSnapshot: way out of range, {0} > {1}", cg.latestSnapshotNum, cgs.processedSnapshotNum);
            }

            int dest = 0;
            while (cgs.processedSnapshotNum < cg.latestSnapshotNum)
            {
                // decide which of the two slots to load it into
                if (cg.snap == cg.activeSnapshots[0])
                {
                    dest = 1;
                }
                else
                {
                    dest = 0;
                }

                // try to read the snapshot from the client system
                cgs.processedSnapshotNum++;
                snapshot_t snap = GetSnapshot(cgs.processedSnapshotNum);

                // if it succeeded, return
                if (snap != null)
                {
                    cg.activeSnapshots[dest] = snap;
                    AddLagometerSnapshotInfo(snap);
                    return snap;
                }

                // a GetSnapshot will return failure if the snapshot
                // never arrived, or  is so old that its entities
                // have been shoved off the end of the circular
                // buffer in the client system.
                AddLagometerSnapshotInfo(null);

                // If there are additional snapshots, continue trying to
                // read them.
            }
            
            return null;
        }

        void AddLagometerSnapshotInfo(snapshot_t snap)
        {
            if (snap == null)
            {
                // Dropped
                Client.Instance.lagometer.snapshotSamples[Client.Instance.lagometer.snapshotCount++ & Lagometer.LAGBUFFER-1] = -1;
                return;
            }

            // Add this snapshots info
            Client.Instance.lagometer.snapshotSamples[Client.Instance.lagometer.snapshotCount & Lagometer.LAGBUFFER - 1] = snap.ping;
            Client.Instance.lagometer.snapshotFlags[Client.Instance.lagometer.snapshotCount & Lagometer.LAGBUFFER - 1] = snap.snapFlags;
            Client.Instance.lagometer.snapshotCount++;
        }

        void ResetEntity(centity_t cent)
        {
            // if the previous snapshot this entity was updated in is at least
            // an event window back in time then we can reset the previous event
            if (cent.snapShotTime <= cg.time - 300)
            {
                cent.previousEvent = 0;
            }

            cent.trailTime = cg.snap.serverTime;

            cent.lerpOrigin = cent.currentState.origin;
            cent.lerpAngles = cent.currentState.angles;
            if (cent.currentState.eType == 1)
            {
                ResetPlayerEntity(cent);
            }
        }


        /*
        ==================
        CG_SetInitialSnapshot

        This will only happen on the very first snapshot, or
        on tourney restarts.  All other times will use 
        CG_TransitionSnapshot instead.

        FIXME: Also called by map_restart?
        ==================
        */
        void SetInitialSnapshot(snapshot_t snap)
        {
            cg.snap = snap;
            PlayerStateToEntityState(snap.ps, Client.Instance.cg_entities[snap.ps.clientNum].currentState, false);

            ExecuteNewServerCommands(snap.serverCommandSequence);

            for (int i = 0; i < cg.snap.numEntities; i++)
            {
                Common.entityState_t state = cg.snap.entities[i];
                centity_t cent = Client.Instance.cg_entities[state.number];
                cent.currentState = state;
                cent.interpolate = false;
                cent.currentValid = true;

                ResetEntity(cent);

                // Fix: Check events
            }

            if (Net.Instance.ClientStatistic != null)
                Net.Instance.ClientStatistic.Reset();
        }

        /*
        ===================
        CG_SetNextSnap

        A new snapshot has just been read in from the client system.
        ===================
        */
        void SetNextSnap(snapshot_t snap)
        {
            cg.nextSnap = snap;
            PlayerStateToEntityState(snap.ps, Client.Instance.cg_entities[snap.ps.clientNum].nextState, false);
            Client.Instance.cg_entities[cg.snap.ps.clientNum].interpolate = true;

            // check for extrapolation errors
            for (int i = 0; i < snap.numEntities; i++)
            {
                Common.entityState_t es = snap.entities[i];
                centity_t cent = Client.Instance.cg_entities[es.number];

                cent.nextState = es;

                // if this frame is a teleport, or the entity wasn't in the
                // previous frame, don't interpolate
                if (!cent.currentValid || (((cent.currentState.eFlags ^ es.eFlags) & Common.EntityFlags.EF_TELEPORT_BIT) == Common.EntityFlags.EF_TELEPORT_BIT))
                {
                    cent.interpolate = false;
                }
                else
                    cent.interpolate = true;
            }

            // if the next frame is a teleport for the playerstate, we
            // can't interpolate during demos
            if (cg.snap != null && ((snap.ps.eFlags ^ cg.snap.ps.eFlags) & Common.EntityFlags.EF_TELEPORT_BIT) == Common.EntityFlags.EF_TELEPORT_BIT)
            {
                cg.nextFrameTeleport = true;
            }
            else
                cg.nextFrameTeleport = false;

            // if changing follow mode, don't interpolate
            if (cg.nextSnap.ps.clientNum != cg.snap.ps.clientNum)
            {
                cg.nextFrameTeleport = true;
            }

            // if changing server restarts, don't interpolate
            if (((cg.nextSnap.snapFlags ^ cg.snap.snapFlags) & 4) == 4)
            {
                cg.nextFrameTeleport = true;
            }

        }

        /*
        ===============
        CG_TransitionEntity

        cent->nextState is moved to cent->currentState and events are fired
        ===============
        */
        void TransitionEntity(centity_t cent)
        {
            cent.currentState = cent.nextState;
            cent.currentValid = true;

            // reset if the entity wasn't in the last frame or was teleported
            if (!cent.interpolate)
            {
                ResetEntity(cent);
            }

            // clear the next state.  if will be set by the next CG_SetNextSnap
            cent.interpolate = false;

            // Todo: Check for events
        }

        snapshot_t GetSnapshot(int snapshotNumber)
        {
            if (snapshotNumber > Client.Instance.cl.snap.messageNum)
            {
                Common.Instance.Error("GetSnapshot: snapshotNumber > cl.snap.messageNum");
            }

            // if the frame has fallen out of the circular buffer, we can't return it
            if (Client.Instance.cl.snap.messageNum - snapshotNumber >= 32)
                return null;

            // if the frame is not valid, we can't return it
            clSnapshot_t clsnap = Client.Instance.cl.snapshots[snapshotNumber & 31];
            if (!clsnap.valid)
                return null;

            // if the entities in the frame have fallen out of their
            // circular buffer, we can't return it
            if (Client.Instance.cl.parseEntitiesNum - clsnap.parseEntitiesNum >= 2048)
                return null;

            // write the snapshot
            snapshot_t snap = new snapshot_t();
            snap.snapFlags = clsnap.snapFlags;
            snap.serverCommandSequence = clsnap.ServerCommandNum;
            snap.ping = clsnap.ping;
            snap.serverTime = clsnap.serverTime;
            snap.areamask = clsnap.areamask;
            snap.ps = clsnap.ps;
            int count = clsnap.numEntities;
            if (count > 256)
            {
                Common.Instance.WriteLine("Truncated {0} entitied to 256", clsnap.numEntities);
                count = 256;
            }
            snap.numEntities = count;
            snap.entities = new Common.entityState_t[count];
            for (int i = 0; i < count; i++)
            {
                snap.entities[i] = Client.Instance.cl.parseEntities[(clsnap.parseEntitiesNum + i) & 2047];
            }
            return snap;

        }

        /*
        ===================
        CG_TransitionSnapshot

        The transition point from snap to nextSnap has passed
        ===================
        */
        void TransitionSnapshot()
        {
            if (cg.snap == null)
                Common.Instance.Error("TransitionSnapshot: Null cg.snap");

            if (cg.nextSnap == null)
                Common.Instance.Error("TransitionSnapshot: Null cg.nextsnap");

            // execute any server string commands before transitioning entities
            ExecuteNewServerCommands(cg.nextSnap.serverCommandSequence);

            // clear the currentValid flag for all entities in the existing snapshot
            for (int i = 0; i < cg.snap.numEntities; i++)
            {
                centity_t cent = Client.Instance.cg_entities[cg.snap.entities[i].number];
                cent.currentValid = false;
            }

            // move nextSnap to snap and do the transitions
            snapshot_t oldFrame = cg.snap;
            cg.snap = cg.nextSnap;

            PlayerStateToEntityState(cg.snap.ps, Client.Instance.cg_entities[cg.snap.ps.clientNum].currentState, false);
            Client.Instance.cg_entities[cg.snap.ps.clientNum].interpolate = false;

            for (int i = 0; i < cg.snap.numEntities; i++)
            {
                centity_t cent = Client.Instance.cg_entities[cg.snap.entities[i].number];
                TransitionEntity(cent);

                // remember time of snapshot this entity was last updated in
                cent.snapShotTime = cg.snap.serverTime;
            }

            cg.nextSnap = null;

            if (oldFrame != null)
            {
                Common.PlayerState ops = oldFrame.ps;
                Common.PlayerState ps = cg.snap.ps;

                // teleporting checks are irrespective of prediction
                if (((ps.eFlags ^ ops.eFlags) & Common.EntityFlags.EF_TELEPORT_BIT) == Common.EntityFlags.EF_TELEPORT_BIT)
                {
                    cg.thisFrameTeleport = true;    // will be cleared by prediction code
                }

                // if we are not doing client side movement prediction for any
                // reason, then the client events and view changes will be issued now
                if ((cg.snap.ps.pm_flags & PMFlags.FOLLOW) == PMFlags.FOLLOW || cg_nopredict.Integer == 1)
                {
                    TransitionPlayerState(ps, ops);
                }
            }
        }


    }
}
