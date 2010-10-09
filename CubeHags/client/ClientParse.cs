using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;
using Lidgren.Network;
using CubeHags.client.common;

namespace CubeHags.client
{
    public sealed partial class Client
    {
        void ParseGameState(NetBuffer msg)
        {
            clc.connectPacketCount = 0;
            // wipe local client state
            ClearState();
            // a gamestate always marks a server command sequence
            clc.serverCommandSequence = msg.ReadInt32();
            // parse all the configstrings and baselines
            while (true)
            {
                int cmd = msg.ReadByte();

                if (cmd == (int)svc_ops_e.svc_EOF)
                    break;

                if (cmd == (int)svc_ops_e.svc_configstring)
                {
                    int index = msg.ReadInt16();
                    string s = msg.ReadString();
                    cl.gamestate.data.Add(index, s);
                }
                else if (cmd == (int)svc_ops_e.svc_baseline)
                {
                    int newnum = msg.ReadInt32();
                    if (newnum < 0 || newnum >= 1024)
                    {
                        Common.Instance.Error("ParseGameState: Baseline number out of range: " + newnum);
                    }
                    Common.entityState_t nullstate = new Common.entityState_t();
                    Net.Instance.MSG_ReadDeltaEntity(msg, ref nullstate, ref cl.entityBaselines[newnum], newnum);
                }
                else
                {
                    Common.Instance.Error("ParseGameState: bad command byte");
                }
            }

            clc.clientNum = msg.ReadInt32();

            // parse useful values out of CS_SERVERINFO
            //ParseServerInfo();

            // parse serverId and other cvars
            SystemInfoChanged();

            InitDownloads();

        }

        /*
        =================
        CL_InitDownloads

        After receiving a valid game state, we valid the cgame and local zip files here
        and determine if we need to download them
        =================
        */
        void InitDownloads()
        {
            // let the client game init and load data
            state = ConnectState.LOADING;

            // Pump the loop, this may change gamestate!
            Common.Instance.EventLoop();

            // if the gamestate was changed by calling Com_EventLoop
            // then we loaded everything already and we don't want to do it again.
            if (state != ConnectState.LOADING)
                return;

            // initialize the CGame
            InitCGame();

            Input.Instance.WritePacket();
            Input.Instance.WritePacket();
            Input.Instance.WritePacket();
        }

        /*
        ==================
        CL_SystemInfoChanged

        The systeminfo configstring has been changed, so parse
        new information out of it.  This will happen at every
        gamestate, and possibly during gameplay.
        ==================
        */
        void SystemInfoChanged()
        {
            string serverInfo = cl.gamestate.data[0];
            // when the serverId changes, any further messages we send to the server will use this new serverId
            // https://zerowing.idsoftware.com/bugzilla/show_bug.cgi?id=475
            // in some cases, outdated cp commands might get sent with this news serverId
            cl.serverId = int.Parse(Info.ValueForKey(serverInfo, "sv_serverid"));

            // scan through all the variables in the systeminfo and locally set cvars to match
            List<KeyValuePair<string, string>> pairs = Info.GetPairs(serverInfo);
            foreach (KeyValuePair<string,string> pair in pairs)
            {
                CVarFlags flags = CVars.Instance.Flags(pair.Key);
                if (flags == CVarFlags.NONEXISTANT)
                    CVars.Instance.Get(pair.Key, pair.Value, CVarFlags.SERVER_CREATED | CVarFlags.ROM);
                else  
                {
                    if((flags & (CVarFlags.SYSTEM_INFO | CVarFlags.SERVER_CREATED)) == 0) 
                    {
                        Common.Instance.WriteLine("WARNING: server is not allowed to set cvar {0}", pair.Key);
                        continue;
                    }
                    CVars.Instance.Set(pair.Key, pair.Value);
                }
            }
        }

        /*
        ================
        CL_ParseSnapshot

        If the snapshot is parsed properly, it will be copied to
        cl.snap and saved in cl.snapshots[].  If the snapshot is invalid
        for any reason, no changes to the state will be made at all.
        ================
        */
        void ParseSnapshot(NetBuffer msg)
        {
            // read in the new snapshot to a temporary buffer
            // we will only copy to cl.snap if it is valid
            clSnapshot_t newsnap = new clSnapshot_t();
            clSnapshot_t old;

            // we will have read any new server commands in this
            // message before we got to svc_snapshot
            newsnap.ServerCommandNum = clc.serverCommandSequence;
            newsnap.serverTime = msg.ReadInt32();
            newsnap.messageNum = clc.serverMessageSequence;

            int deltaNum = msg.ReadByte();

            if (deltaNum <= 0)
            {
                newsnap.deltaNum = -1;
            }
            else
                newsnap.deltaNum = newsnap.messageNum - deltaNum;
            newsnap.snapFlags = msg.ReadByte();

            // If the frame is delta compressed from data that we
            // no longer have available, we must suck up the rest of
            // the frame, but not use it, then ask for a non-compressed
            // message 
            if (newsnap.deltaNum <= 0)
            {
                newsnap.valid = true;   // uncompressed frame
                old = null;
            }
            else
            {
                old = cl.snapshots[newsnap.deltaNum & 31];
                if (!old.valid)
                {
                    // should never happen
                    Common.Instance.WriteLine("ParseSnapshot: Delta from invalid frame (not supposed to happen!).");
                }
                else if (old.messageNum != newsnap.deltaNum)
                {
                    // The frame that the server did the delta from
                    // is too old, so we can't reconstruct it properly.
                    Common.Instance.WriteLine("ParseSnapshot: Delta frame too old.");
                }
                else if (cl.parseEntitiesNum - old.parseEntitiesNum > 2048 - 128)
                {
                    Common.Instance.WriteLine("ParseSnapshot: Delta parseEntitiesNum too old");
                }
                else
                    newsnap.valid = true;   // valid delta parse
            }

            // read areamask
            int len = msg.ReadByte();
            newsnap.areamask = msg.ReadBytes(32);
            // read playerinfo
            if (old != null)
            {
                Net.ReadDeltaPlayerstate(msg, old.ps, newsnap.ps);
            }
            else
                Net.ReadDeltaPlayerstate(msg, null, newsnap.ps);

            // read packet entities
            ParsePacketEntities(msg, old, newsnap);

            // if not valid, dump the entire thing now that it has
            // been properly read
            if (!newsnap.valid)
                return;

            // clear the valid flags of any snapshots between the last
            // received and this one, so if there was a dropped packet
            // it won't look like something valid to delta from next
            // time we wrap around in the buffer
            int oldMessageNum = cl.snap.messageNum + 1;
            if (newsnap.messageNum - oldMessageNum >= 32)
            {
                oldMessageNum = newsnap.messageNum - 31;
            }
            for (; oldMessageNum < newsnap.messageNum; oldMessageNum++ )
            {
                cl.snapshots[oldMessageNum & 31].valid = false;
            }

            // copy to the current good spot
            cl.snap = newsnap;
            cl.snap.ping = 999;
            // calculate ping time
            for (int i = 0; i < 32; i++)
            {
                int packetNum = (clc.netchan.outgoingSequence - 1 - i) & 31;
                if (cl.snap.ps.commandTime >= cl.outPackets[packetNum].p_serverTime)
                {
                    cl.snap.ping = realtime - cl.outPackets[packetNum].p_realtime;
                    break;
                }
            }

            // save the frame off in the backup array for later delta comparisons
            cl.snapshots[cl.snap.messageNum & 31] = cl.snap;

            //Common.Instance.WriteLine("   Snapshot:{0} delta:{1} ping:{2}", cl.snap.messageNum, cl.snap.deltaNum, cl.snap.ping);
            cl.newSnapshots = true;
        }

        void ParsePacketEntities(NetBuffer msg, clSnapshot_t oldframe, clSnapshot_t newframe) 
        {
            newframe.parseEntitiesNum = cl.parseEntitiesNum;
            newframe.numEntities = 0;

            // delta from the entities present in oldframe
            int oldindex = 0, oldnum = 0;
            Common.entityState_t oldstate = null;
            if (oldframe == null)
                oldnum = 99999;
            else
            {
                if (oldnum >= oldframe.numEntities)
                    oldnum = 99999;
                else
                {
                    oldstate = cl.parseEntities[(oldframe.parseEntitiesNum + oldindex) & 2047];
                    oldnum = oldstate.number;
                }
            }

            while (true)
            {
                // read the entity index number
                int newnum = msg.ReadInt32();

                if (newnum == 1023)
                    break;

                while (oldnum < newnum)
                {
                    // one or more entities from the old packet are unchanged
                    DeltaEntity(msg, newframe, oldnum, oldstate, true);
                    oldindex++;

                    if (oldindex >= oldframe.numEntities)
                        oldnum = 99999;
                    else
                    {
                        oldstate = cl.parseEntities[(oldframe.parseEntitiesNum + oldindex) & 2047];
                        oldnum = oldstate.number;
                    }
                }
                if (oldnum == newnum)
                {
                    // delta from previous state
                    DeltaEntity(msg, newframe, newnum, oldstate, false);

                    oldindex++;

                    if (oldindex >= oldframe.numEntities)
                        oldnum = 99999;
                    else
                    {
                        oldstate = cl.parseEntities[(oldframe.parseEntitiesNum + oldindex) & 2047];
                        oldnum = oldstate.number;
                    }
                    continue;
                }

                if (oldnum > newnum)
                {
                    // delta from baseline
                    DeltaEntity(msg, newframe, newnum, cl.entityBaselines[newnum], false);
                    continue;
                }
            }


            // any remaining entities in the old frame are copied over
            while (oldnum != 99999)
            {
                // one or more entities from the old packet are unchanged
                DeltaEntity(msg, newframe, oldnum, oldstate, true);

                oldindex++;

                if (oldindex >= oldframe.numEntities)
                    oldnum = 99999;
                else
                {
                    oldstate = cl.parseEntities[(oldframe.parseEntitiesNum + oldindex) & 2047];
                    oldnum = oldstate.number;
                }
            }
        }


        /*
        ==================
        CL_DeltaEntity

        Parses deltas from the given base and adds the resulting entity
        to the current frame
        ==================
        */
        void DeltaEntity(NetBuffer msg, clSnapshot_t frame, int newnum, Common.entityState_t old, bool unchanged)
        {
            // save the parsed entity state into the big circular buffer so
            // it can be used as the source for a later delta
            Common.entityState_t state = cl.parseEntities[cl.parseEntitiesNum & 2047];
            if (unchanged)
                state = old;
            else
                Net.Instance.MSG_ReadDeltaEntity(msg, ref old, ref state, newnum);
            
            cl.parseEntities[cl.parseEntitiesNum & 2047] = state;
            
            if (state.number == 1023)
                return; // entity was delta removed

            cl.parseEntitiesNum++;
            frame.numEntities++;
        }


        /*
        =====================
        CL_ParseCommandString

        Command strings are just saved off until cgame asks for them
        when it transitions a snapshot
        =====================
        */
        void ParseCommandString(NetBuffer buf)
        {
            int seq = buf.ReadInt32();
            string s = buf.ReadString();

            // see if we have already executed stored it off
            if (clc.serverCommandSequence >= seq)
                return;

            clc.serverCommandSequence = seq;
            int index = seq & 63;
            clc.serverCommands[index] = s;
        }

        void ParseServerMessage(Net.Packet packet)
        {
            // get the reliable sequence acknowledge number
            clc.reliableAcknowledge = packet.Buffer.ReadInt32();

            if (clc.reliableAcknowledge < clc.reliableSequence - 64)
            {
                clc.reliableAcknowledge = clc.reliableSequence;
            }
            //
            // parse the message
            //
            NetBuffer buf = packet.Buffer;
            while (true)
            {
                int cmd = buf.ReadByte();

                // See if this is an extension command after the EOF, which means we
                //  got data that a legacy client should ignore.
                if ((cmd == (int)svc_ops_e.svc_EOF) && (buf.PeekByte() == (int)clc_ops_e.clc_extension))
                {
                    //Common.Instance.WriteLine("NET: EXTENSION");
                    buf.ReadByte(); // throw the svc_extension byte away.
                    cmd = buf.ReadByte();   // something legacy clients can't do!
                    // sometimes you get a svc_extension at end of stream...dangling
                    //  bits in the huffman decoder giving a bogus value?
                    if (cmd == -1)
                        cmd = (int)svc_ops_e.svc_EOF;
                }

                if (cmd == (int)svc_ops_e.svc_EOF)
                {
                    // END OF MESSAGE
                    break;
                }

                switch (cmd)
                {
                    case (int)svc_ops_e.svc_serverCommand:
                        ParseCommandString(buf);
                        break;
                    case (int)svc_ops_e.svc_gamestate:
                        ParseGameState(buf);
                        break;
                    case (int)svc_ops_e.svc_snapshot:
                        ParseSnapshot(buf);
                        break;
                    case (int)svc_ops_e.svc_download:
                        //ParseDownload(buf);
                        break;
                    case (int)svc_ops_e.svc_nop:
                        break;
                    default:
                        Common.Instance.Error("Illegible server message");
                        break;
                }
            }
        }
    }
}
