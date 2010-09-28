using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;
using CubeHags.common;
using CubeHags.client.render;
using CubeHags.client.render.Formats;

namespace CubeHags.client.cgame
{
    public sealed partial class CGame
    {
        /*
        =========================
        CG_AdjustPositionForMover

        Also called by client movement prediction code
        =========================
        */
        void AdjustPositionForMover(Vector3 inv, int moverNum, int fromTime, int toTime, out Vector3 outv)
        {
            if (moverNum <= 0 || moverNum >= 1022)
            {
                outv = inv;
                return;
            }

            centity_t cent = Entities[moverNum];
            if (cent.currentState.eType != 4)
            {
                outv = inv;
                return;
            }

            Vector3	oldOrigin, origin, deltaOrigin;
	        Vector3	oldAngles, angles, deltaAngles;
            Common.Instance.EvaluateTrajectory(cent.currentState.pos, fromTime, out oldOrigin);
            Common.Instance.EvaluateTrajectory(cent.currentState.apos, fromTime, out oldAngles);

            Common.Instance.EvaluateTrajectory(cent.currentState.pos, toTime, out origin);
            Common.Instance.EvaluateTrajectory(cent.currentState.apos, toTime, out angles);

            deltaOrigin = origin - oldOrigin;
            deltaAngles = angles - oldAngles;

            outv = inv + deltaOrigin;
        }

        void AddPacketEntities()
        {
            // set cg.frameInterpolation
            if (cg.nextSnap != null)
            {
                int delta = cg.nextSnap.serverTime - cg.snap.serverTime;
                if (delta == 0)
                {
                    cg.frameInterpolation = 0;
                }
                else
                    cg.frameInterpolation = (float)(cg.time - cg.snap.serverTime) / delta;
            }
            else
                cg.frameInterpolation = 0;  // actually, it should never be used, because 
                                            // no entities should be marked as interpolating

            // generate and add the entity from the playerstate
            Common.PlayerState ps = cg.predictedPlayerState;
            PlayerStateToEntityState(ps, cg.predictedPlayerEntity.currentState, false);
            AddCEntity(cg.predictedPlayerEntity);

            // lerp the non-predicted value for lightning gun origins
            CalcEntityLerpPositions(Entities[cg.snap.ps.clientNum]);

            // add each entity sent over by the server
            for (int num = 0; num < cg.snap.numEntities; num++)
            {
                centity_t cent = Entities[cg.snap.entities[num].number];
                AddCEntity(cent);
            }
        }

        void InterpolateEntityPosition(centity_t ent)
        {
            // it would be an internal error to find an entity that interpolates without
            // a snapshot ahead of the current one
            if (cg.nextSnap == null)
            {
                Common.Instance.Error("InterpolateEntityPosition: cg.nextSnap == null");
            }

            float f = cg.frameInterpolation;

            // this will linearize a sine or parabolic curve, but it is important
            // to not extrapolate player positions if more recent data is available
            Vector3 current, next;
            Common.Instance.EvaluateTrajectory(ent.currentState.pos, cg.snap.serverTime, out current);
            Common.Instance.EvaluateTrajectory(ent.nextState.pos, cg.nextSnap.serverTime, out next);

            ent.lerpOrigin[0] = current[0] + f * (next[0] - current[0]);
            ent.lerpOrigin[1] = current[1] + f * (next[1] - current[1]);
            ent.lerpOrigin[2] = current[2] + f * (next[2] - current[2]);

            Common.Instance.EvaluateTrajectory(ent.currentState.apos, cg.snap.serverTime, out current);
            Common.Instance.EvaluateTrajectory(ent.nextState.apos, cg.nextSnap.serverTime, out next);

            ent.lerpAngles[0] = current[0] + f * (next[0] - current[0]);
            ent.lerpAngles[1] = current[1] + f * (next[1] - current[1]);
            ent.lerpAngles[2] = current[2] + f * (next[2] - current[2]);
        }

        void CalcEntityLerpPositions(centity_t ent)
        {
            // if this player does not want to see extrapolated players
            // make sure the clients use TR_INTERPOLATE
            if (ent.currentState.number < 64)
            {
                ent.currentState.pos.trType = Common.trType_t.TR_INTERPOLATE;
                ent.nextState.pos.trType = Common.trType_t.TR_INTERPOLATE;
            }

            if (ent.interpolate && ent.currentState.pos.trType == Common.trType_t.TR_INTERPOLATE)
            {
                InterpolateEntityPosition(ent);
                return;
            }

            // first see if we can interpolate between two snaps for
            // linear extrapolated clients
            if (ent.interpolate && ent.currentState.pos.trType == Common.trType_t.TR_LINEAR_STOP &&
                                                        ent.currentState.number < 64)
            {
                InterpolateEntityPosition(ent);
                return;
            }

            // just use the current frame and evaluate as best we can
            Common.Instance.EvaluateTrajectory(ent.currentState.pos, cg.time, out ent.lerpOrigin);
            Common.Instance.EvaluateTrajectory(ent.currentState.apos, cg.time, out ent.lerpAngles);

            // adjust for riding a mover if it wasn't rolled into the predicted
            // player state
            if (ent != cg.predictedPlayerEntity)
            {
                AdjustPositionForMover(ent.lerpOrigin, ent.currentState.groundEntityNum, cg.snap.serverTime, cg.time, out ent.lerpOrigin);
            }
        }

        void Player(centity_t ent)
        {
            // the client number is stored in clientNum.  It can't be derived
            // from the entity number, because a single client may have
            // multiple corpses on the level using the same clientinfo
            int clientNum = ent.currentState.clientNum;
            if (clientNum < 0 || clientNum >= 64)
            {
                Common.Instance.Error("Bad clientNum on player entity");
            }
            clientInfo_t ci = cgs.clientinfo[clientNum];

            // it is possible to see corpses from disconnected players that may
            // not have valid clientinfo
            //if (!ci.infoValid)
            //    return;

            if (clientNum == Client.Instance.cl.snap.ps.clientNum)
                return;

            VertexPositionColor[] vecs = MiscRender.CreateBox(Common.playerMins, Common.playerMaxs, new Color4(System.Drawing.Color.Red) { Alpha = 0.5f });
            float pitch, roll, yaw;
            pitch = ent.lerpAngles.Z * (float)(Math.PI / 180f);
            roll = ent.lerpAngles.Y * (float)(Math.PI / 180f);
            yaw = ent.lerpAngles.X * (float)(Math.PI / 180f);
            //Common.Instance.WriteLine("{0}, {1}, {2}", yaw, pitch, roll);

            Matrix posmat = Matrix.RotationX(yaw) * Matrix.RotationZ(roll) * Matrix.Translation(ent.lerpOrigin);
            for (int i = 0; i < vecs.Length; i++)
            {
                Vector4 vec = Vector3.Transform(vecs[i].Position, posmat);
                vecs[i].Position.X = vec.X;
                vecs[i].Position.Y = vec.Y;
                vecs[i].Position.Z = vec.Z;
            }

            Renderer.Instance.SourceMap.bboxVerts.AddRange(vecs);
        }

        void AddCEntity(centity_t ent)
        {
            // event-only entities will have been dealt with already
            if (ent.currentState.eType >= 13)
                return;

            // calculate the current origin
            CalcEntityLerpPositions(ent);

            switch (ent.currentState.eType)
            {
                case 1:
                    Player(ent);
                    break;
            }
        }
    }
}
