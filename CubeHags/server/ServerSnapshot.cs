using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;
using SlimDX;
using Lidgren.Network;

namespace CubeHags.server
{
    public sealed partial class Server
    {
        static readonly int 	SNAPFLAG_RATE_DELAYED =	1;
        static readonly int 	SNAPFLAG_NOT_ACTIVE =	2; // snapshot used during connection and for zombies
        static readonly int 	SNAPFLAG_SERVERCOUNT =	4; // toggled every map_restart so transitions can be detected

        void SendClientSnapshot(client_t client, int index)
        {
            // build the snapshot
            BuildClientSnapshot(client, index);

            NetBuffer msg = new NetBuffer();
            // let the client know which reliable clientCommands we have received
            msg.Write(client.lastClientCommand);
            // (re)send any reliable server commands
            UpdateServerCommandsToClient(client, msg);

            // send over all the relevant entityState_t
            // and the playerState_t
            WriteSnapshotToClient(client, msg);

            SendMessageToClient(msg, client);
        }

        void WriteSnapshotToClient(client_t cl, NetBuffer msg)
        {
            // this is the snapshot we are creating
            clientSnapshot_t frame = cl.frames[cl.netchan.outgoingSequence & 31];


            // try to use a previous frame as the source for delta compressing the snapshot
            clientSnapshot_t oldframe = null;
            int lastframe;
            if (cl.deltaMessage <= 0 || cl.state != clientState_t.CS_ACTIVE)
            {
                // client is asking for a retransmit
                oldframe = null;
                lastframe = 0;
            }
            else if (cl.netchan.outgoingSequence - cl.deltaMessage >= (31 - 3))
            {
                // client hasn't gotten a good message through in a long time
                Common.Instance.WriteLine("{0}:  Delta request from out of date packet.", cl.name);
                oldframe = null;
                lastframe = 0;
            }
            else
            {
                // we have a valid snapshot to delta from
                oldframe = cl.frames[cl.deltaMessage & 31];
                lastframe = cl.netchan.outgoingSequence - cl.deltaMessage;
                // the snapshot's entities may still have rolled off the buffer, though
                if (oldframe.first_entity <= svs.nextSnapshotEntities - svs.numSnapshotEntities)
                {
                    Common.Instance.WriteLine("{0}: Delta request from out of date entities.", cl.name);
                    oldframe = null;
                    lastframe = 0;
                }
            }

            msg.Write((byte)svc_ops_e.svc_snapshot);
            // send over the current server time so the client can drift
            // its view of time to try to match
            if (cl.oldServerTime > 0)
            {
                // The server has not yet got an acknowledgement of the
                // new gamestate from this client, so continue to send it
                // a time as if the server has not restarted. Note from
                // the client's perspective this time is strictly speaking
                // incorrect, but since it'll be busy loading a map at
                // the time it doesn't really matter.
                msg.Write((int)(sv.time + cl.oldServerTime));
            }
            else
                msg.Write((int)sv.time);

            // what we are delta'ing from
            msg.Write((byte)lastframe);

            int snapFlags = svs.snapFlagServerBit;
            if (cl.rateDelayed)
                snapFlags |= SNAPFLAG_RATE_DELAYED;
            if (cl.state != clientState_t.CS_ACTIVE)
                snapFlags |= SNAPFLAG_NOT_ACTIVE;

            msg.Write((byte)snapFlags);

            // send over the areabits
            msg.Write((byte)frame.areabytes);
            msg.Write(frame.areabits);

            // delta encode the playerstate
            if (oldframe != null)
                Net.WriteDeltaPlayerstate(msg, oldframe.ps, frame.ps);
            else
                Net.WriteDeltaPlayerstate(msg, null, frame.ps);

            // delta encode the entities
            EmitPacketEntities(oldframe, frame, msg);
        }

        /*
        =============
        SV_EmitPacketEntities

        Writes a delta update of an entityState_t list to the message.
        =============
        */
        void EmitPacketEntities(clientSnapshot_t from, clientSnapshot_t to, NetBuffer msg)
        {
            int fromnumentities = 0;

            if (from != null)
                fromnumentities = from.num_entities;

            Common.entityState_t oldent = null, newent = null;
            int oldindex = 0, newindex = 0;
            int newnum, oldnum;

            while (newindex < to.num_entities || oldindex < fromnumentities)
            {
                if (newindex >= to.num_entities)
                    newnum = 9999;
                else
                {
                    newent = svs.snapshotEntities[(from.first_entity + newindex) % svs.numSnapshotEntities];
                    newnum = newent.number;
                }

                if (oldindex >= fromnumentities)
                    oldnum = 9999;
                else
                {
                    oldent = svs.snapshotEntities[(from.first_entity + oldindex) % svs.numSnapshotEntities];
                    oldnum = oldent.number;
                }

                if (newnum == oldnum)
                {
                    // delta update from old position
                    // because the force parm is qfalse, this will not result
                    // in any bytes being emited if the entity has not changed at all
                    Net.Instance.MSG_WriteDeltaEntity(msg, ref oldent, ref newent, false);
                    oldindex++;
                    newindex++;
                    continue;
                }

                if (newnum < oldnum)
                {
                    // this is a new entity, send it from the baseline
                    Net.Instance.MSG_WriteDeltaEntity(msg, ref sv.svEntities[newnum].baseline, ref newent, true);
                    newindex++;
                    continue;
                }

                if (newnum > oldnum)
                {
                    // the old entity isn't present in the new message
                    Common.entityState_t nullEnt = null;
                    Net.Instance.MSG_WriteDeltaEntity(msg, ref oldent, ref nullEnt, true);
                    oldindex++;
                    continue;
                }
            }

            msg.Write((uint)1023, 10);    // end of packetentities
        }

        


        /*
        =============
        SV_BuildClientSnapshot

        Decides which entities are going to be visible to the client, and
        copies off the playerstate and areabits.

        This properly handles multiple recursive portals, but the render
        currently doesn't.

        For viewing through other player's eyes, clent can be something other than client->gentity
        =============
        */
        void BuildClientSnapshot(client_t client, int index)
        {
            List<int> snapshotEntityNumbers = new List<int>();
            // bump the counter used to prevent double adding
            sv.snapshotCounter++;

            // this is the frame we are creating
            clientSnapshot_t frame = client.frames[client.netchan.outgoingSequence & 31];

            // clear everything in this snapshot
            frame.num_entities = 0;
            frame.areabits = new byte[32];

            Common.sharedEntity_t clent = client.gentity;
            if (clent == null || client.state == clientState_t.CS_ZOMBIE)
            {
                return;
            }

            // grab the current playerState_t
            Common.playerState_t ps = GameClientNum(index);

            frame.ps = ps.Clone();

            // never send client's own entity, because it can
            // be regenerated from the playerstate
            int clientnum = frame.ps.clientNum;
            if (clientnum < 0 || clientnum >= 1024)
            {
                Common.Instance.Error("bad gEnt");
            }

            svEntity_t svEnt = sv.svEntities[clientnum];
            svEnt.snapshotCounter = sv.snapshotCounter;

            // find the client's viewpoint
            Vector3 org = ps.origin;
            org[2] += ps.viewheight;

            // add all the entities directly visible to the eye, which
            // may include portal entities that merge other viewpoints
            AddEntitiesVisibleFromPoint(org, ref frame, snapshotEntityNumbers, false);

            // if there were portals visible, there may be out of order entities
            // in the list which will need to be resorted for the delta compression
            // to work correctly.  This also catches the error condition
            // of an entity being included twice.
            snapshotEntityNumbers.Sort((a, b) => { return a.CompareTo(b); });

            // now that all viewpoint's areabits have been OR'd together, invert
            // all of them to make it a mask vector, which is what the renderer wants
            for (int i = 0; i < 8; i++)
            {
                frame.areabits[i] = (byte)(frame.areabits[i] ^ -1);
            }

            // copy the entity states out
            frame.num_entities = 0;
            frame.first_entity = svs.nextSnapshotEntities;
            for (int i = 0; i < snapshotEntityNumbers.Count; i++)
            {
                Common.sharedEntity_t ent = sv.gentities[snapshotEntityNumbers[i]];
                //Common.entityState_t state = ;
                svs.snapshotEntities[svs.nextSnapshotEntities % svs.numSnapshotEntities] = ent.s;
                frame.num_entities++;
            }


        }

        void AddEntitiesVisibleFromPoint(Vector3 origin, ref clientSnapshot_t frame, List<int> snapshotEntitiesNumbers, bool portal)
        {
            // during an error shutdown message we may need to transmit
            // the shutdown message after the server has shutdown, so
            // specfically check for it
            if ((int)sv.state == 0)
                return;

            int leafnum = ClipMap.Instance.PointLeafnum(origin);
            int clientarea = ClipMap.Instance.LeafArea(leafnum);
            int clientcluster = ClipMap.Instance.LeafCluster(leafnum);

            // calculate the visible areas
            frame.areabytes = ClipMap.Instance.WriteAreaBits(ref frame.areabits, clientarea);
            bool[] clientpvs = ClipMap.Instance.ClusterPVS(clientcluster);

            Common.sharedEntity_t ent;
            for (int i = 0; i < sv.num_entities; i++)
            {
                ent = sv.gentities[i];

                // never send entities that aren't linked in
                if (!ent.r.linked)
                    continue;

                if (ent.s.number != i)
                {
                    Common.Instance.WriteLine("FIXING ENT.S.NUMBER!!!");
                    ent.s.number = i;
                }

                // entities can be flagged to explicitly not be sent to the client
                if ((ent.r.svFlags & Common.svFlags.NOCLIENT) == Common.svFlags.NOCLIENT)
                    continue;

                // entities can be flagged to be sent to only one client
                if ((ent.r.svFlags & Common.svFlags.SINGLECLIENT) == Common.svFlags.SINGLECLIENT)
                    if (ent.r.singleClient != frame.ps.clientNum)
                        continue;

                // entities can be flagged to be sent to everyone but one client
                if ((ent.r.svFlags & Common.svFlags.NOTSINGLECLIENT) == Common.svFlags.NOTSINGLECLIENT)
                    if (ent.r.singleClient == frame.ps.clientNum)
                        continue;

                // entities can be flagged to be sent to a given mask of clients
                if ((ent.r.svFlags & Common.svFlags.CLIENTMASK) == Common.svFlags.CLIENTMASK)
                {
                    if (frame.ps.clientNum >= 32)
                        Common.Instance.Error("CLIENTMASK: clientNum > 32");
                    if ((~ent.r.singleClient & (1 << frame.ps.clientNum)) == (1 << frame.ps.clientNum))
                        continue;
                }

                
                svEntity_t svEnt = Game.Instance.SvEntityForGentity(ent);

                // don't double add an entity through portals
                if (svEnt.snapshotCounter == sv.snapshotCounter)
                    continue;

                // broadcast entities are always sent
                if ((ent.r.svFlags & Common.svFlags.BROADCAST) == Common.svFlags.BROADCAST)
                {
                    AddEntToSnapshot(svEnt, ent, snapshotEntitiesNumbers);
                    continue;
                }

                // ignore if not touching a PV leaf
                // check area
                if (!ClipMap.Instance.AreasConnected(clientarea, svEnt.areanum))
                {
                    // doors can legally straddle two areas, so
                    // we may need to check another one
                    if (!ClipMap.Instance.AreasConnected(clientarea, svEnt.areanum2))
                        continue;   // blocked by a door
                }

                if (svEnt.numClusters <= 0)
                    continue;
                int l;
                for (i = 0; i < svEnt.numClusters; i++)
                {
                    l = svEnt.clusternums[i];
                    if (clientpvs[l])
                        break;
                }

                // not visible
                if (i == svEnt.numClusters)
                    continue;

                // add it
                AddEntToSnapshot(svEnt, ent, snapshotEntitiesNumbers);

                // if its a portal entity, add everything visible from its camera position
                if ((ent.r.svFlags & Common.svFlags.PORTAL) == Common.svFlags.PORTAL)
                {
                    if (ent.s.generic1 > 0)
                    {
                        Vector3 dir = ent.s.origin - origin;
                        if (dir.LengthSquared() > (float)ent.s.generic1 * ent.s.generic1)
                            continue;
                    }
                    AddEntitiesVisibleFromPoint(ent.s.origin2, ref frame, snapshotEntitiesNumbers, true);
                }

            }
        }

        void AddEntToSnapshot(svEntity_t svEnt, Common.sharedEntity_t gEnt, List<int> eNums)
        {
            // if we have already added this entity to this snapshot, don't add again
            if (svEnt.snapshotCounter == sv.snapshotCounter)
                return;

            svEnt.snapshotCounter = sv.snapshotCounter;

            // if we are full, silently discard entities
            if (eNums.Count > 1024)
            {
                Common.Instance.WriteLine("Snapshot is full");
                return;
            }

            eNums.Add(gEnt.s.number);
        }

        /*
        ====================
        SV_RateMsec

        Return the number of msec a given size message is supposed
        to take to clear, based on the current rate
        ====================
        */
        int RateMsec(client_t client, int msgSize)
        {
            int headerSize = 48;
            // individual messages will never be larger than fragment size
            if (msgSize > 1500)
                msgSize = 1500;

            int rate = 20000; // FIX: variable rate
            int rateMsec = (int)((msgSize + headerSize) * 1000 / rate * Common.Instance.timescale.Value);
            return rateMsec;
        }

        void SendMessageToClient(NetBuffer msg, client_t client)
        {
            // record information about the message
            client.frames[client.netchan.outgoingSequence & 31].messageSize = msg.LengthBytes;
            client.frames[client.netchan.outgoingSequence & 31].messageSent = (int)svs.time;
            client.frames[client.netchan.outgoingSequence & 31].messageAcked = -1;

            // send the datagram
            NetChan_Transmit(client, msg);
        }

        /*
        ==================
        SV_UpdateServerCommandsToClient

        (re)send all server commands the client hasn't acknowledged yet
        ==================
        */
        void UpdateServerCommandsToClient(client_t cl, NetBuffer msg)
        {
            // write any unacknowledged serverCommands
            for (int i = cl.reliableAcknowledge+1; i <= cl.reliableSequence; i++)
            {
                msg.Write((byte)svc_ops_e.svc_serverCommand);
                msg.Write(i);
                msg.Write(cl.reliableCommands[i & 63]);
            }
            cl.reliableSent = cl.reliableSequence;
        }
    }
}
