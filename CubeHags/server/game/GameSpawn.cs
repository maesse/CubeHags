using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;

namespace CubeHags.server
{
    public sealed partial class Game
    {
        /*
        ==============
        G_SpawnEntitiesFromString

        Parses textual entity definitions out of an entstring and spawns gentities.
        ==============
        */
        void SpawnEntitiesFromString()
        {
            // allow calls to G_Spawn*()
            level.spawning = true;

            // the worldspawn is not an actual entity, but it still
            // has a "spawn" function to perform any global setup
            // needed by a level (setting configstrings or cvars, etc)
            if (!ParseSpawnVars())
            {
                Common.Instance.Error("SpawnEntities: no entities");
            }
            //Worldspawn();

            // parse ents
            while (ParseSpawnVars())
            {
                //SpawnGEntityFromSpawnVars();
            }

            level.spawning = false;
        }

        bool ParseSpawnVars()
        {
            return false;
        }
    }
}
