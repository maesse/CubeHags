using System;
using System.Collections.Generic;
 
using System.Text;
using Lidgren.Network;
using CubeHags.common;
using CubeHags.client;
using CubeHags.client.common;
using SlimDX;

namespace CubeHags.server
{
    public sealed partial class Server
    {
        void ExecuteClientMessage(client_t cl, NetBuffer buf)
        {
            int serverId = buf.ReadInt32();
            cl.messageAcknowledge = buf.ReadInt32();
            if (cl.messageAcknowledge < 0)
            {
                // usually only hackers create messages like this
                // it is more annoying for them to let them hanging
                DropClient(cl, "Illegible client message");
                return;
            }

            cl.reliableAcknowledge = buf.ReadInt32();
            // NOTE: when the client message is fux0red the acknowledgement numbers
            // can be out of range, this could cause the server to send thousands of server
            // commands which the server thinks are not yet acknowledged in SV_UpdateServerCommandsToClient
            if (cl.reliableAcknowledge < cl.reliableSequence - 64)
            {
                DropClient(cl, "Illegible client message");
                cl.reliableAcknowledge = cl.reliableSequence;
                return;
            }

            // if this is a usercmd from a previous gamestate,
            // ignore it or retransmit the current gamestate
            // 
            if (serverId != sv.serverId && !cl.lastClientCommandString.Equals("nextdl"))
            {
                if (serverId >= sv.restartedServerId && serverId < sv.serverId)
                {
                    // they just haven't caught the map_restart yet
                    Common.Instance.WriteLine("{0} : ignoring pre map_restart / outdated client message", cl.name);
                    return;
                }

                // if we can tell that the client has dropped the last
                // gamestate we sent them, resend it
                if (cl.messageAcknowledge > cl.gamestateMessageNum)
                {
                    Common.Instance.WriteLine("{0} : dropped gamestate, resending", cl.name);
                    SendClientGameState(cl);
                }
                return;
            }

            // this client has acknowledged the new gamestate so it's
            // safe to start sending it the real time again
            if (cl.oldServerTime > 0 && serverId == sv.serverId)
            {
                Common.Instance.WriteLine("{0} acknowledged gamestate", cl.name);
                cl.oldServerTime = 0;
            }
            
            // read optional clientCommand strings
            int c;
            do
            {
                c = buf.ReadByte();

                if (c == (int)clc_ops_e.clc_EOF)
                    break;

                if (c != (int)clc_ops_e.clc_clientCommand)
                    break;

                if (!ClientCommand(cl, buf))
                    break;  // we couldn't execute it because of the flood protection

                if (cl.state == clientState_t.CS_ZOMBIE)
                    return; // disconnect command
            } while (true);

            // read the usercmd_t
            if (c == (int)clc_ops_e.clc_move)
            {
                UserMove(cl, buf, true);
            }
            else if (c == (int)clc_ops_e.clc_moveNoDelta)
            {
                UserMove(cl, buf, false);
            }
            else if (c != (int)clc_ops_e.clc_EOF)
            {
                Common.Instance.WriteLine("WARNING: bad command byte for client {0}", cl.name);
            }
        }

        bool ClientCommand(client_t cl, NetBuffer msg)
        {
            int seq = msg.ReadInt32();
            string s = msg.ReadString();

            // see if we have already executed it
            if (cl.lastClientCommand >= seq)
                return true;

            Common.Instance.WriteLine("ClientCommand: {0}[s{1}]: {2}", cl.name, seq, s);

            // drop the connection if we have somehow lost commands
            if (seq > cl.lastClientCommand + 1)
            {
                Common.Instance.WriteLine("Client {0} lost {1} clientCommands", cl.name, seq-cl.lastClientCommand+1);
                DropClient(cl, "Lost reliable commands");
                return false;
            }

            // don't allow another command for one second
            cl.nextReliableTime = (int)time + 1000;

            ExecuteClientCommand(cl, s);
            cl.lastClientCommand = seq;
            cl.lastClientCommandString = s;
            return true;    // continue procesing
        }

        void ExecuteClientCommand(client_t cl, string cmd)
        {
            string[] tokens = Commands.TokenizeString(cmd);

            // see if it is a server level command
            Common.Instance.WriteLine("ExecuteClientCommand: {0}", tokens[0]);
            switch (tokens[0])
            {
                case "userinfo":
                    UpdateUserInfo(cl, tokens);
                    break;
                case "disconnect":
                    DropClient(cl, "disconnected");
                    break;
                default:
                    if (sv.state == serverState_t.SS_GAME)
                        Game.Instance.Client_Command(cl.id, tokens);
                    break;

            }
            //string[] ucmds = new string[] {"userinfo", "disconnect", "cp", "vdr", "download", "nextdl", "stopdl", "donedl" };
        }

        void UpdateUserInfo(client_t cl, string[] tokens)
        {
            cl.userinfo = tokens[1];
            UserInfoChanged(cl);
            Game.Instance.ClientUserInfoChanged(cl.id);
        }

        /*
        ================
        SV_SendClientGameState

        Sends the first message from the server to a connected client.
        This will be sent on the initial connection and upon each new map load.

        It will be resent if the client acknowledges a later message but has
        the wrong gamestate.
        ================
        */
        void SendClientGameState(client_t cl)
        {
            Common.Instance.WriteLine("SendClientGameState for {0}", cl.name);
            Common.Instance.WriteLine("Going from CONNECTED to PRIMED for {0}", cl.name);
            cl.state = clientState_t.CS_PRIMED;
            cl.pureAuthentic = 0;
            cl.gotCP = false;

            // when we receive the first packet from the client, we will
            // notice that it is from a different serverid and that the
            // gamestate message was not just sent, forcing a retransmit
            cl.gamestateMessageNum = cl.netchan.outgoingSequence;

            NetBuffer msg = new NetBuffer();
            // NOTE, MRE: all server->client messages now acknowledge
            // let the client know which reliable clientCommands we have received
            msg.Write(cl.lastClientCommand);

            // send any server commands waiting to be sent first.
            // we have to do this cause we send the client->reliableSequence
            // with a gamestate and it sets the clc.serverCommandSequence at
            // the client side
            UpdateServerCommandsToClient(cl, msg);

            // send the gamestate
            msg.Write((byte)svc_ops_e.svc_gamestate);
            msg.Write(cl.reliableSequence);

            // write the configstrings
            foreach (int i in sv.configstrings.Keys)
            {
                msg.Write((byte)svc_ops_e.svc_configstring);
                msg.Write((short)i);
                msg.Write(sv.configstrings[i]);
            }

            // write the baselines
            Common.entityState_t nullstate = new Common.entityState_t();
            for (int i = 0; i < 1024; i++)
            {
                Common.entityState_t bases = sv.svEntities[i].baseline;
                if (bases == null || bases.number <= 0)
                    continue;

                msg.Write((byte)svc_ops_e.svc_baseline);
                Net.Instance.MSG_WriteDeltaEntity(msg, ref nullstate, ref bases, true);
            }

            msg.Write((byte)svc_ops_e.svc_EOF);
            msg.Write(cl.id);
            SendMessageToClient(msg, cl);
        }


        /*
        ==================
        SV_UserMove

        The message usually contains all the movement commands 
        that were in the last three packets, so that the information
        in dropped packets can be recovered.

        On very fast clients, there may be multiple usercmd packed into
        each of the backup packets.
        ==================
        */
        void UserMove(client_t cl, NetBuffer buf, bool deltaCompressed)
        {
            if (deltaCompressed)
                cl.deltaMessage = cl.messageAcknowledge;
            else
                cl.deltaMessage = -1;

            int cmdCount = buf.ReadByte();

            if (cmdCount < 1)
            {
                Common.Instance.WriteLine("cmdCount < 1");
                return;
            }

            if (cmdCount > 32)
            {
                Common.Instance.WriteLine("cmdCount > 32");
                return;
            }

            List<Input.UserCommand> cmds = new List<Input.UserCommand>();
            Input.UserCommand oldcmd = new Input.UserCommand();
            for (int i = 0; i < cmdCount; i++)
            {
                Input.UserCommand cmd = Input.Instance.MSG_ReadDeltaUsercmdKey(buf, ref oldcmd);
                cmds.Add(cmd);
                oldcmd = cmd;
            }

            // save time for ping calculation
            cl.frames[cl.messageAcknowledge & 31].messageAcked = (int)time;

            // if this is the first usercmd we have received
            // this gamestate, put the client into the world
            if (cl.state == clientState_t.CS_PRIMED)
            {
                ClientEnterWorld(cl, cmds[cmds.Count-1]);
                // the moves can be processed normaly
            }

            if (cl.state != clientState_t.CS_ACTIVE)
            {
                cl.deltaMessage = -1;
                return;
            }

            // usually, the first couple commands will be duplicates
            // of ones we have previously received, but the servertimes
            // in the commands will cause them to be immediately discarded
            for (int i = 0; i < cmdCount; i++)
            {
                // if this is a cmd from before a map_restart ignore it
                if (cmds[i].serverTime > cmds[cmdCount - 1].serverTime)
                {
                    continue;
                }

                // don't execute if this is an old cmd which is already executed
                // these old cmds are included when cl_packetdup > 0
                if (cmds[i].serverTime <= cl.lastUsercmd.serverTime)
                {
                    continue;
                }

                ClientThink(cl, cmds[i]);
            }
        }

        void ClientThink(client_t cl, Input.UserCommand cmd)
        {
            cl.lastUsercmd = cmd;

            if (cl.state != clientState_t.CS_ACTIVE)
                return; // may have been kicked during the last usercmd

            Game.Instance.Client_Think(cl.id);
        }

        void ClientEnterWorld(client_t cl, Input.UserCommand cmd)
        {
            Common.Instance.WriteLine("Going from PRIMED to ACTIVE for {0}", cl.name);
            cl.state = clientState_t.CS_ACTIVE;

            // resend all configstrings using the cs commands since these are
            // no longer sent when the client is CS_PRIMED
            UpdateConfigStrings(cl);

            // set up the entity for the client
            int clientNum = cl.id;
            sharedEntity ent = sv.gentities[clientNum];
            ent.s.number = clientNum;
            cl.gentity = ent;

            cl.deltaMessage = -1;
            cl.nextSnapshotTime = (int)time; // generate a snapshot immediately
            cl.lastUsercmd = cmd;

            Game.Instance.Client_Begin(clientNum);
        }

        

        //public trace_t Trace(Vector3 start, Vector3 mins, Vector3 maxs, Vector3 end, int passEntityNum, int contentmask)
        //{
        //    //results.fraction = 1f;
        //    //trace_t t  = new trace_t();
        //    return ClipMap.Instance.Box_Trace( start, end, mins, maxs, 0, contentmask, 0);
        //}

        public void PointContents(Vector3 p, int passEntityNum)
        {

        }

    }
}
