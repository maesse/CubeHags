using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CubeHags.common
{
    class Info
    {
        public static string SetValueForKey(string source, string key, string value)
        {
            char[] blacklist = { '\\', ';', '"' };
            for (int i = 0; i < blacklist.Length; i++)
            {
                if(value.Contains(blacklist[i]) || key.Contains(blacklist[i]))
                {
                    Common.Instance.WriteLine("SetValueForKey: Can't use keys or values with '{0}' - {1}={2}", blacklist[i], key, value);
                    return "";
                }
            }
            string cleaned = RemoveKey(source, key);
            if (cleaned.Length == 0)
                return "";

            string newPair = string.Format("\\{0}\\{1}", key, value);
            cleaned += newPair;

            return cleaned;
        }

        public static string RemoveKey(string source, string key)
        {
            if (key.Contains('\\'))
            {
                Common.Instance.WriteLine("RemoveKey: key contains invalid char '\\'");
                return source;
            }

            int index = source.IndexOf('\\' + key);
            if (index >= 0)
            {
                int keyEnd = source.IndexOf('\\', index + 1);
                // Got data after?
                if (keyEnd >= 0)
                {
                    int valueEnd = source.IndexOf('\\', keyEnd + 1);
                    if (valueEnd >= 0)
                    {
                        if (index == 0)
                        {
                            // Cut beginning
                            return source.Substring(valueEnd + 1);
                        }
                        else
                        {
                            // Key to remove is in middle of other values
                            return source.Substring(0, index) + source.Substring(valueEnd + 1);
                        }
                    }
                    else
                    {
                        // End of source
                        if (index != 0)
                            return source.Substring(0,index);
                        return "";
                    }
                }
            }
            return source;
        }

        /*
        ===============
        Info_ValueForKey

        Searches the string for the given
        key and returns the associated value, or an empty string.
        FIXME: overflow check?
        ===============
        */
        public static string ValueForKey(string s, string key)
        {
            if (s == null || s.Length == 0)
                return "";

            char[] blacklist = { '\\', ';', '"' };
            for (int i = 0; i < blacklist.Length; i++)
            {
                if (key.Contains(blacklist[i]))
                {
                    Common.Instance.WriteLine("ValueForKey: Can't get keys with '{0}'. ({1})", blacklist[i], key);
                    return null;
                }
            }

            string[] spl = s.Split('\\');
            bool keyindex = true;
            for (int i = 0; i < spl.Length; i++)
            {
                if (spl[i].Equals('\\'))
                    continue;

                if (spl[i].Length == 0)
                    continue;

                if (keyindex && key.Equals(spl[i]))
                {
                    if (spl.Length > i + 1)
                        return spl[i + 1];
                    else
                        return "";
                }

                keyindex = !keyindex;
            }
            return "";
        }

        public static List<KeyValuePair<string, string>> GetPairs(string info)
        {
            bool key = true;
            string[] tokens = info.Split('\\');
            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();
            string skey = "";
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i].Equals('\\') || tokens[i].Length == 0)
                    continue;

                if (key)
                    skey = tokens[i];
                else
                    pairs.Add(new KeyValuePair<string,string>(skey, tokens[i]));

                key = !key;
            }

            return pairs;
        }


        /*
        ==================
        Info_Validate

        Some characters are illegal in info strings because they
        can mess up the server's parsing
        ==================
        */
        public static bool Validate(string value)
        {
            if (value.Contains('\"'))
                return false;

            if (value.Contains(';'))
                return false;

            return true;
        }
    }
}
