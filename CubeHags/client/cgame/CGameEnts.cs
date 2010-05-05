using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;
using CubeHags.common;

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

            centity_t cent = Client.Instance.cg_entities[moverNum];
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
    }
}
