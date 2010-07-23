using System;
using System.Collections.Generic;
 
using System.Text;

namespace CubeHags.client.map.Source
{
    public class Entity
    {
        public string Name = "";
        public string ClassName = "?";
        public Dictionary<string, string> Values = new Dictionary<string, string>();

        public Entity(Dictionary<string, string> Values)
        {
            this.Values = Values;

            foreach (string key in Values.Keys)
            {
                switch (key)
                {
                    case "classname":
                        ClassName = Values[key];
                        break;
                    case "targetname":
                        Name = Values[key];
                        break;
                }
            }
        }



        public static List<Entity> CreateEntities(string lumpString)
        {
            List<Entity> entities = new List<Entity>();
            string[] lines = lumpString.Split('\n');

            bool inEntity = false;
            StringBuilder currentEnt = new StringBuilder();
            // Get entity from {} markers
            foreach (string line in lines)
            {
                if (!inEntity)
                {
                    // Check for beginning entity
                    if (line == "{")
                    {
                        inEntity = true;
                        currentEnt.Length = 0;
                        continue;
                    }

                }
                else
                {
                    // Check for ending entity
                    if (line == "}")
                    {
                        inEntity = false;
                        entities.Add(ParseRawString(currentEnt.ToString()));
                    }
                    else
                    {
                        currentEnt.AppendLine(line);
                    }
                }
            }

            return entities;
        }

        static Entity ParseRawString(string rawString)
        {

            Dictionary<string, string> values = new Dictionary<string, string>();

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
                            //if (key.Length > 32)
                            //    Common.Instance.Error("ParseRawString: Entity key name exceeds 32 characters");
                        }
                        else
                        {
                            value = cleanLine;
                            //if (value.Length > 1024)
                            //    Common.Instance.Error("ParseRawString: Entity value name exceeds 1024 characters");

                            // FIXME: Overwriting values may not be the correct way to go about this
                            if (values.ContainsKey(key))
                                values[key] = value;
                            else
                                values.Add(key, value);
                            break;
                        }
                        expectKey = !expectKey;
                    }
                }
            }
            
            Entity ent = new Entity(values);
            return ent;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}:n:{3}", ClassName, Name, Values.Count);
        }
    }
}
