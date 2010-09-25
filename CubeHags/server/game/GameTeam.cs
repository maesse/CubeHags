using System;
using System.Collections.Generic;
using System.Text;

namespace CubeHags.server
{
    public sealed partial class Game
    {
        bool OnSameTeam(gentity_t ent1, gentity_t ent2)
        {
            if (ent1.client == null || ent2.client == null)
                return false;

            if (ent2.client.sess.sessionTeam == ent1.client.sess.sessionTeam)
                return true;

            return false;
        }
    }
}
