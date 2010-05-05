using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client.common;
using System.IO;
using System.Windows.Forms;

namespace CubeHags.client.input
{
    public sealed class KeyHags
    {
        private Dictionary<Keys, string> binds = new Dictionary<Keys, string>();

        KeyHags()
        {

        }

        public void Init()
        {
            Commands.Instance.AddCommand("bind", new CommandDelegate(Cmd_Bind));
            Commands.Instance.AddCommand("unbind", new CommandDelegate(Cmd_Unbind));
            Commands.Instance.AddCommand("unbindall", new CommandDelegate(Cmd_Unbindall));
            Commands.Instance.AddCommand("bindlist", new CommandDelegate(Cmd_Bindlist));

            InitBinds();
        }

        public void test()
        {
            System.Console.WriteLine("\n WinForms keys");
            foreach (Keys key in Enum.GetValues(typeof(Keys)))
            {
                System.Console.WriteLine("{0} {1}", (int)key, Enum.GetName(typeof(Keys), key));
            }

        }

        // Defindes some default binds
        private void InitBinds()
        {
            SetBind("TILDE", "toggleconsole");
        }

        // Bind command implementation
        public void Cmd_Bind(string[] tokens)
        {
            if (tokens.Length < 2)
            {
                System.Console.WriteLine("bind <key> [command] : attach a command to a key");
                return;
            }
            Keys key = GetKeyFromString(tokens[1]);
            if (key == Keys.None)
            {
                System.Console.WriteLine("\"{0}\" isn't an valid key.", tokens[1]);
                return;
            }

            // Display bind
            if (tokens.Length == 2)
            {
                if (binds.ContainsKey(key))
                    System.Console.WriteLine("\"{0}\" = \"{1}\"", tokens[1], binds[key]);
                else
                    System.Console.WriteLine("\"{0}\" is not bound", tokens[1]);
                return;
            }

            string cmd = Commands.ArgsFrom(tokens, 2);
            SetBind(key, cmd);
        }

        // Unbind implementation
        void Cmd_Unbind(string[] tokens)
        {
            if (tokens.Length != 2)
            {
                System.Console.WriteLine("unbind <key> : remove commands from a key");
                return;
            }

            Keys key = GetKeyFromString(tokens[1]);
            if (key == Keys.None)
            {
                System.Console.WriteLine("\"{0}\" isn't a valid key", tokens[1]);
                return;
            }

            // Remove if present in binds
            if(binds.ContainsKey(key))
                binds.Remove(key);
        }

        void Cmd_Unbindall(string[] tokens)
        {
            binds.Clear();
        }

        // Writes binds to a stream
        private void WriteBindings(StreamWriter writer)
        {
            writer.WriteLine("unbindall");
            foreach (Keys key in binds.Keys)
            {
                string bind = binds[key];
                if (binds.Count > 0)
                    writer.WriteLine("bind {0} \"{1}\"", GetStringFromKey(key, false), bind);
            }
        }

        // Prints bindslist
        private void Cmd_Bindlist(string[] tokens)
        {
            foreach (Keys key in binds.Keys)
            {
                string bind = binds[key];
                if (binds.Count > 0)
                    System.Console.WriteLine("{0} \"{1}\"", GetStringFromKey(key, false), bind);
            }
        }

        // Execute the commands in the bind string
        public void ParseBinding(Keys key, bool down, long time)
        {
            if (!binds.ContainsKey(key) || binds[key] == "")
                return;
            int i = 0;
            // Get binding from key
            string bind = binds[key];

            // Parse...
            while (true)
            {
                // Skip whitespace
                while (char.IsWhiteSpace(bind[i]))
                    i++;
                // locate end of command
                int end = bind.IndexOf(';');
                if (bind[i] == '+')
                {
                    // button commands add keynum and time as parameters
                    // so that multiple sources can be discriminated and
                    // subframe corrected
                    string cmd = string.Format("{0}{1} {2} {3}\n", (down) ? '+' : '-', bind.Substring(i + 1), (int)key, time);
                    Commands.Instance.AddText(cmd);
                }
                else if (down)
                {
                    Commands.Instance.AddText(bind.Substring(i));
                    Commands.Instance.AddText("\n");
                }

                if (end < 0)
                    break;
                i = end + 1;
            }
        }

        // Bind a key defined by a string
        public void SetBind(string key, string binding)
        {
            Keys keyv = GetKeyFromString(key);
            if (keyv == Keys.None)
            {
                System.Console.WriteLine("Unable to bind/parse key \"{0}\"", key);
                return;
            }
            SetBind(keyv, binding);
        }

        // Bind a key
        public void SetBind(Keys key, string binding)
        {
            if (binds.ContainsKey(key))
                binds[key] = binding;
            else
                binds.Add(key, binding);

            // consider this like modifying an archived cvar, so the
            // file write will be triggered at the next oportunity
            CVars.Instance.modifiedFlags |= CVarFlags.ARCHIVE;
        }

        // Writes lines containing "bind key value"
        public void WriteBinds(StreamWriter writer)
        {
            writer.WriteLine("unbindall");
            foreach (Keys key in binds.Keys)
            {
                string bind = binds[key];
                if (bind == null || bind.Length == 0)
                    continue;
                writer.WriteLine("bind {0} \"{1}\"", GetStringFromKey(key, false), bind);
            }
        }

        // Translate string to Keys
        public static Keys GetKeyFromString(string str)
        {
            str = str.ToUpper();

            switch (str)
            {
                case "0":
                    return Keys.D0;
                case "1":
                    return Keys.D1;
                case "2":
                    return Keys.D2;
                case "3":
                    return Keys.D3;
                case "4":
                    return Keys.D4;
                case "5":
                    return Keys.D5;
                case "6":
                    return Keys.D6;
                case "7":
                    return Keys.D7;
                case "8":
                    return Keys.D8;
                case "9":
                    return Keys.D9;
                case "TILDE":
                    return Keys.Oem5;
                default:
                    {
                        Keys key;
                        // Try direct parse
                        try
                        {
                            key = (Keys)Enum.Parse(typeof(Keys), str);
                            return key;
                        }
                        catch { }
                        //if (Enum.TryParse<Keys>(str, out key))
                        //    return key;
                        //else
                        {
                            // Else try looping through all keys and do case-insensitive compares
                            foreach (string keyStr in Enum.GetNames(typeof(Keys)))
                            {
                                if (keyStr.ToUpper().Equals(str))
                                    return (Keys)Enum.Parse(typeof(Keys), str);
                            }
                        }

                        // Fallback
                        System.Console.WriteLine("KeyHags: Could not translate key: {0}", str);
                        return Keys.None;
                    }
            }

        }

        // Translate key to string
        public static string GetStringFromKey(Keys key, bool returnInteger)
        {
            switch (key)
            {
                case Keys.D0:
                    return "0";
                case Keys.D1:
                    return "1";
                case Keys.D2:
                    return "2";
                case Keys.D3:
                    return "3";
                case Keys.D4:
                    return "4";
                case Keys.D5:
                    return "5";
                case Keys.D6:
                    return "6";
                case Keys.D7:
                    return "7";
                case Keys.D8:
                    return "8";
                case Keys.D9:
                    return "9";
                case Keys.Oem5:
                    return "TILDE";
                default:
                    if (returnInteger)
                        return key.ToString() + " : " + (int)key;
                    return key.ToString();
            }
        }

        // Singleton
        private static readonly KeyHags _Instance = new KeyHags();
        public static KeyHags Instance { get { return _Instance; } }
    }
}
