using System;
using System.Collections.Generic;
using System.Linq;
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

        public static Entity ParseRawString(string rawString)
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
                        }
                        else
                        {
                            value = cleanLine;
                            if(!values.ContainsKey(key))
                                values.Add(key, value);
                            break;
                        }
                        expectKey = !expectKey;
                    }
                }
            }

            return new Entity(values);
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
                        currentEnt = new StringBuilder();
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

        public override string ToString()
        {
            return string.Format("{0}:{1}:n:{3}", ClassName, Name, Values.Count);
        }
    }
}
