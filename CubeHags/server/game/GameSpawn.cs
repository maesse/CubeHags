using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;
using SlimDX;
using CubeHags.client.common;

namespace CubeHags.server
{
    public sealed partial class Game
    {
        gentity_t SelectSpectatorSpawnPoint(ref Vector3 origin, ref Vector3 angles)
        {
            FindIntermissionPoint();
            origin = level.intermission_origin;
            //origin[2] += 36;
            angles = level.intermission_angle;
            return null;
        }

        public delegate void SpawnDelegate(gentity_t ent);
        public struct spawn_t
        {
            public string Name;
            public SpawnDelegate Spawn;
        }
        public spawn_t[] spawns;

        void SP_info_player_start(gentity_t ent) 
        {
            //ent.classname = "info_player_deathmatch";
            //SP_info_player_deathmatch(ent);
        }



        /*
        ===========
        SelectInitialSpawnPoint

        Try to find a spawn point marked 'initial', otherwise
        use normal spawn selection.
        ============
        */
        gentity_t SelectInitialSpawnPoint(ref Vector3 origin, ref Vector3 angles)
        {
            gentity_t spot = null;
            int sc = -1;
            while ((spot = Find(ref sc, "classname", "info_player_start")) != null)
            {
                if((spot.spawnflags & 0x01) == 0x01) 
                {
                    break;
                }
            }

            if (spot == null)
                return SelectRandomFurthestSpawnPoint(Vector3.Zero, ref origin, ref angles);

            origin = spot.s.origin;
            origin[2] += 9-Common.playerMins[2];
            angles = spot.s.angles;

            return spot;
        }

        /*
        ==================
        FindIntermissionPoint

        This is also used for spectator spawns
        ==================
        */
        void FindIntermissionPoint()
        {
            // find the intermission spot
            int i = -1;
            gentity_t ent = Find(ref i, "classname", "info_player_start");
            if (ent == null)    // the map creator forgot to put in an intermission point...
                SelectRandomFurthestSpawnPoint(Vector3.Zero, ref level.intermission_origin, ref  level.intermission_angle);
            else
            {   
                level.intermission_origin = ent.s.origin;
                level.intermission_origin[2] += 9 - Common.playerMins[2];
                level.intermission_angle = ent.s.angles;
                // if it has a target, look towards it
                if (ent.target != null)
                {   
                    gentity_t target = PickTarget(ent.target);
                    if (target != null)
                    {
                        Vector3 dir = Vector3.Subtract(target.s.origin, level.intermission_origin);
                        vectoangles(dir, ref level.intermission_angle);
                    }
                }
            }
        }

        /*
        ===========
        SelectRandomFurthestSpawnPoint

        Chooses a player start, deathmatch start, etc
        ============
        */
        gentity_t SelectRandomFurthestSpawnPoint(Vector3 avoid, ref Vector3 origin, ref Vector3 angles)
        {
            gentity_t spot;
            int numspots = 0;
            int i= 0,j=0;
            Vector3 delta;
            float dist;
            float[] list_dist = new float[128];
            gentity_t[] list_spot = new gentity_t[128];
            string[] names = new string[] { "info_player_terrorist", "info_player_counterterrorist", "info_player_start" };
            for (int nid = 0; nid < names.Length; nid++)
            {
                string name = names[nid];
                i = -1;
                while ((spot = Find(ref i, "classname", name)) != null)
                {
                    //if (SpotWouldTelefrag())
                    //    continue;

                    delta = Vector3.Subtract(spot.s.origin, avoid);
                    dist = delta.Length();

                    for (j = 0; j < numspots; j++)
                    {
                        if (dist > list_dist[j])
                        {
                            if (numspots >= 128)
                                numspots = 128 - 1;

                            for (int h = numspots; h >j; h--)
                            {
                                list_dist[h] = list_dist[h - 1];
                                list_spot[h] = list_spot[h - 1];
                            }

                            list_dist[j] = dist;
                            list_spot[j] = spot;

                            numspots++;
                            break;
                        }
                    }

                    if (j >= numspots && numspots < 128)
                    {
                        list_dist[numspots] = dist;
                        list_spot[numspots] = spot;
                        numspots++;
                    }
                }
            }

            if (numspots == 0)
            {
                int starti = -1;
                spot = Find(ref starti, "classname", "info_player_terrorist");

                if (spot == null)
                    Common.Instance.Error("Couldn't find a spawn point");

                origin = spot.s.origin;
                origin[2] += 9 - Common.playerMins[2];
                angles = spot.s.angles;
                return spot;
            }
            

            // select a random spot from the spawn points furthest away
            int random = Common.Rand.Next(0,numspots);
            origin = list_spot[random].s.origin;
            origin[2] += 9 - Common.playerMins[2];
            angles = list_spot[random].s.angles;

            return list_spot[random];
        }


        Vector3 ParseVector(string value)
        {
            value = value.Replace('.', ','); // Needed for proper C# float parsing
            string[] values = value.Split(' ', '\t');
            if (values.Length == 3)
            {
                Vector3 position = Vector3.Zero;
                position.X = float.Parse(values[0]);
                position.Y = float.Parse(values[1]);
                position.Z = float.Parse(values[2]);
                return position;
            }
            Common.Instance.Error("ParseVector: Couldn't parse vector");
            return Vector3.Zero;
        }

        void ParseField(string key, string value, gentity_t ent)
        {
            switch (key)
            {
                case "classname":
                    ent.classname = value;
                    break;
                case "origin":
                    ent.s.origin = ParseVector(value);
                    break;
                case "model":
                    ent.model = value;
                    break;
                case "model2":
                    ent.model2 = value;
                    break;
                case "spawnflags":
                    ent.spawnflags = int.Parse(value);
                    break;
                case "speed":
                    ent.speed = float.Parse(value);
                    break;
                case "target":
                    ent.target = value;
                    break;
                case "targetname":
                    ent.targetname = value;
                    break;
                case "message":
                    ent.message = value;
                    break;
                case "team":
                    ent.team = value;
                    break;
                case "wait":
                    ent.wait = float.Parse(value);
                    break;
                case "random":
                    ent.random = float.Parse(value);
                    break;
                case "count":
                    ent.count = int.Parse(value);
                    break;
                case "health":
                    ent.health = int.Parse(value);
                    break;
                case "dmg":
                    ent.damage = int.Parse(value);
                    break;
                case "angles":
                    ent.s.angles = ParseVector(value);
                    break;
                case "angle":
                    ent.s.angles = ParseAngleHack(value);
                    break;
                //case "":

                //    break;

            }
        }

        Vector3 ParseAngleHack(string str)
        {
            Vector3 res = Vector3.Zero;
            res[1] = float.Parse(str);
            return res;
        }

        /*
        =============
        G_PickTarget

        Selects a random entity from among the targets
        =============
        */
        static int MAXCHOICES = 32;
        gentity_t PickTarget(string name)
        {
            if (name == null || name.Length == 0)
            {
                Common.Instance.WriteLine("PickTarget: called with NULL parameter");
                return null;
            }

            gentity_t ent;
            int i = -1, numchoices = 0;
            gentity_t[] choice = new gentity_t[MAXCHOICES];
            while (true)
            {
                ent = Find(ref i, "targetname", name);
                if (ent == null)
                    break;
                choice[numchoices++] = ent;
                if (numchoices == MAXCHOICES)
                    break;
            }

            if (numchoices == 0)
            {
                Common.Instance.WriteLine("PickTarget: target {0} not found.", name);
                return null;
            }

            return choice[Common.Rand.Next() % numchoices];

        }

        void vectoangles(Vector3 value1, ref Vector3 angles)
        {
        	float	forward;
        	float	yaw, pitch;
        	
        	if ( value1[1] == 0 && value1[0] == 0 ) {
        		yaw = 0;
        		if ( value1[2] > 0 ) {
        			pitch = 90;
        		}
        		else {
        			pitch = 270;
        		}
        	}
        	else {
        		if ( value1[0] != 0f ) {
        			yaw = (float)( Math.Atan2 ( value1[1], value1[0] ) * 180 / Math.PI );
        		}
        		else if ( value1[1] > 0 ) {
        			yaw = 90;
        		}
        		else {
        			yaw = 270;
        		}
        		if ( yaw < 0 ) {
        			yaw += 360;
        		}

        		forward = (float)Math.Sqrt ( value1[0]*value1[0] + value1[1]*value1[1] );
                pitch = (float)(Math.Atan2(value1[2], forward) * 180 / Math.PI);
        		if ( pitch < 0 ) {
        			pitch += 360;
        		}
        	}

        	angles[0] = -pitch;
        	angles[1] = yaw;
        	angles[2] = 0;
        }


        /*
        =============
        G_Find

        Searches all active entities for the next one that holds
        the matching string at fieldofs (use the FOFS() macro) in the structure.

        Searches beginning at the entity after from, or the beginning if NULL
        NULL will be returned if the end of the list is reached.

        =============
        */
        gentity_t Find(ref int from, string field, string match)
        {
            if (from == -1)
                from = 0;
            else
                from++;

            gentity_t ent;
            for (; from < g_entities.Length; from++)
            {
                ent = g_entities[from];
                if (!ent.inuse)
                    continue;

                switch (field)
                {
                    case "classname":
                        if (ent.classname == null || ent.classname.Length == 0)
                            continue;
                        if (ent.classname.Equals(match))
                            return ent;
                        break;
                    case "targetname":
                        if (ent.targetname == null || ent.targetname.Length == 0)
                            continue;
                        if (ent.targetname.Equals(match))
                            return ent;
                        break;
                    default:
                        Common.Instance.WriteLine("Find: FIX ME");
                        break;
                }
            }

            return null;
        }

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
            WorldSpawn();

            // parse ents
            while (ParseSpawnVars())
            {
                SpawnGEntityFromSpawnVars();
            }

            level.spawning = false;
        }

        void WorldSpawn()
        {
            string s = "";
            SpawnString("classname", "", ref s);
            if (!s.Equals("worldspawn"))
            {
                Common.Instance.Error("WorldSpawn: First entity is not worldspawn");
            }

            Server.Instance.SetConfigString((int)ConfigString.CS_LEVEL_START_TIME, ""+level.startTime);
            CVars.Instance.Set("sv_gravity", "800");

            g_entities[1022].s.number = 1022;
            g_entities[1022].classname = "worldspawn";


        }

        /*
        ===================
        G_SpawnGEntityFromSpawnVars

        Spawn an entity and fill in all of the level fields from
        level.spawnVars[], then call the class specfic spawn function
        ===================
        */
        void SpawnGEntityFromSpawnVars()
        {
            // get the next free entity
            gentity_t ent = Spawn();

            KeyValuePair<string, string> val;
            int i;
            for (i = 0; i < level.spawnVars.Count; i++)
            {
                val = level.spawnVars[i];
                ParseField(val.Key, val.Value, ent);
            }

            SpawnInt("notfree", "0", ref i);
            if (i != 0)
            {
                FreeEntity(ent);
                return;
            }

            // move editor origin to pos
            ent.s.pos.trBase = ent.s.origin;
            ent.r.currentOrigin = ent.s.origin;

            // if we didn't get a classname, don't bother spawning anything
            if(!CallSpawn(ent)) 
            {
                //FreeEntity(ent);
            }
        }

        bool CallSpawn(gentity_t ent)
        {
            if (ent.classname == null || ent.classname.Length == 0)
            {
                Common.Instance.WriteLine("CallSpawn: NULL classname");
                return false;
            }



            // check normal spawn functions
            for (int i = 0; i < spawns.Length; i++)
            {
                if (ent.classname.Equals(spawns[i].Name))
                {
                    // found it
                    spawns[i].Spawn(ent);
                    return true;
                }
            }
            return false;
        }

        bool SpawnString(string key, string defaultString, ref string s)
        {
            foreach (KeyValuePair<string, string> pair in level.spawnVars)
            {
                if (key.Equals(pair.Key))
                {
                    s = pair.Value;
                    return true;
                }
            }

            s = defaultString;
            return false;
        }

        bool SpawnInt(string key, string defaultString, ref int i)
        {
            string s = "";
            bool present = SpawnString(key, defaultString, ref s);
            i = int.Parse(s);
            return present;
        }

        bool SpawnVector(string key, string defaultString, ref Vector3 vec)
        {
            string s = "";
            bool present = SpawnString(key, defaultString, ref s);
            vec = ParseVector(s);
            return present;
        }

        bool SpawnFloat(string key, string defaultString, ref float f)
        {
            string s = "";
            bool present = SpawnString(key, defaultString, ref s);
            f = float.Parse(s);
            return present;
        }

        void ParseRawString(string rawString)
        {
            List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>();

            string[] lines = rawString.Split('\n');
            bool expectKey;
            string key = "", value = "";
            foreach (string line in lines)
            {
                expectKey = true;
                string[] splitted = line.Split('"');
                foreach (string linepart in splitted)
                {
                    string cleanLine = linepart.Replace("\"", "").Trim();
                    if (cleanLine.Length > 0)
                    {
                        if (expectKey)
                        {
                            key = cleanLine;
                            if (key.Length > 32)
                                Common.Instance.Error("ParseRawString: Entity key name exceeds 32 characters");
                        }
                        else
                        {
                            value = cleanLine;
                            if(value.Length > 1024)
                                Common.Instance.Error("ParseRawString: Entity value name exceeds 1024 characters");
                            values.Add(new KeyValuePair<string,string>(key, value));
                            break;
                        }
                        expectKey = !expectKey;
                    }
                }
            }

            level.spawnVars = values;
        }

        bool ParseSpawnVars()
        {
            bool inEntity = false;
            StringBuilder currentEnt = new StringBuilder();
            // Get entity from {} markers
            string line;
            while (Server.Instance.sv.entityParsePoint < Server.Instance.sv.entityParseString.Length)
            {
                line = Server.Instance.sv.entityParseString[Server.Instance.sv.entityParsePoint++];
                if (!inEntity)
                {
                    // Check for beginning entity
                    if (line == "{")
                    {
                        inEntity = true;
                        currentEnt.Length = 0;
                        //level.spawnVars.Clear();
                        continue;
                    }

                }
                else
                {
                    // Check for ending entity
                    if (line == "}")
                    {
                        inEntity = false;
                        ParseRawString(currentEnt.ToString());
                        return true;
                    }
                    else
                    {
                        currentEnt.AppendLine(line);
                    }
                }
            }

            return false;
        }
    }
}
