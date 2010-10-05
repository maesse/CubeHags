using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client.common;
using System.IO;
using System.Windows.Forms;
using CubeHags.common;

namespace CubeHags.client.input
{
    public sealed class KeyHags
    {
        private Dictionary<int, string> binds = new Dictionary<int, string>();

        KeyHags()
        {

        }

        // All binds are made out of CubeKeys
        public enum CubeKeys : int
        {
            TAB = 9,
            K_ENTER	=	13,
 	        K_ESCAPE	=27,
 	        K_SPACE	=	32,
            // normal keys should be passed as lowercased ascii,
 	        K_BACKSPACE	=127,
 	        K_UPARROW	=128,
 	        K_DOWNARROW	=129,
 	        K_LEFTARROW	=130,
 	        K_RIGHTARROW=131,
 	        K_ALT	=	132,
 	        K_CTRL	=	133,
 	        K_SHIFT	=	134,
 	        K_F1	=	135,
 	        K_F2	=	136,
 	        K_F3	=	137,
 	        K_F4	=	138,
 	        K_F5	=	139,
 	        K_F6	=	140,
 	        K_F7	=	141,
 	        K_F8	=	142,
 	        K_F9	=	143,
 	        K_F10	=	144,
 	        K_F11	=	145,
 	        K_F12	=	146,
 	        K_INS	=	147,
 	        K_DEL	=	148,
 	        K_PGDN	=	149,
 	        K_PGUP	=	150,
 	        K_HOME	=	151,
 	        K_END	=	152,
            K_KP_HOME	=160,
            K_KP_UPARROW=	161,
            K_KP_PGUP	=162,
 	        K_KP_LEFTARROW=	163,
            K_KP_5	=	164,
            K_KP_RIGHTARROW=	165,
            K_KP_END	=166,
            K_KP_DOWNARROW=	167,
            K_KP_PGDN	=168,
 	        K_KP_ENTER	=169,
            K_KP_INS   	=170,
 	        K_KP_DEL	=171,
            K_KP_SLASH	=172,
            K_KP_MINUS	=173,
            K_KP_PLUS	=174,
            K_CAPSLOCK	=175,
            K_CONSOLE = 126,
            //,
            // joystick buttons,
            //,
 	        K_JOY1	=	203,
 	        K_JOY2	=	204,
 	        K_JOY3	=	205,
 	        K_JOY4	=	206,
            //,
            // aux keys are for multi-buttoned joysticks to generate so they can use,
            // the normal binding process,
            //,
 	        K_AUX1	=	207,
 	        K_AUX2	=	208,
 	        K_AUX3	=	209,
 	        K_AUX4	=	210,
 	        K_AUX5	=	211,
 	        K_AUX6	=	212,
 	        K_AUX7	=	213,
 	        K_AUX8	=	214,
 	        K_AUX9	=	215,
 	        K_AUX10	=	216,
 	        K_AUX11	=	217,
 	        K_AUX12	=	218,
 	        K_AUX13	=	219,
 	        K_AUX14	=	220,
 	        K_AUX15	=	221,
 	        K_AUX16	=	222,
 	        K_AUX17	=	223,
	        K_AUX18	=	224,
 	        K_AUX19	=	225,
 	        K_AUX20	=	226,
 	        K_AUX21	=	227,
 	        K_AUX22	=	228,
 	        K_AUX23	=	229,
 	        K_AUX24	=	230,
 	        K_AUX25	=	231,
 	        K_AUX26	=	232,
 	        K_AUX27	=	233,
 	        K_AUX28	=	234,
 	        K_AUX29	=	235,
 	        K_AUX30	=	236,
 	        K_AUX31	=	237,
 	        K_AUX32	=	238,
            K_MWHEELDOWN	=239,
            K_MWHEELUP	=240,
            K_PAUSE	=	255,
            //,
            // mouse buttons generate virtual keys,
            //,
 	        K_MOUSE1	=241,
 	        K_MOUSE2	=242,
 	        K_MOUSE3	=243,
            K_MOUSE4	=244,
            K_MOUSE5	=245
    }

        private Dictionary<string, int> StrToCubeKey;
        private Dictionary<int, int> KeyToCubeKey;

        public void Init()
        {

            StrToCubeKey = new Dictionary<string, int>();
            KeyToCubeKey = new Dictionary<int, int>();

            // Add strToCubeKey
            StrToCubeKey.Add("TAB",(int) CubeKeys.TAB);
	        StrToCubeKey.Add("ENTER",(int) CubeKeys.K_ENTER);
	        StrToCubeKey.Add("ESCAPE",(int) CubeKeys.K_ESCAPE);
	        StrToCubeKey.Add("SPACE",(int) CubeKeys.K_SPACE);
	        StrToCubeKey.Add("BACKSPACE",(int) CubeKeys.K_BACKSPACE);

	        StrToCubeKey.Add("UPARROW",(int) CubeKeys.K_UPARROW);
	        StrToCubeKey.Add("DOWNARROW",(int) CubeKeys.K_DOWNARROW);
	        StrToCubeKey.Add("LEFTARROW",(int) CubeKeys.K_LEFTARROW);
	        StrToCubeKey.Add("RIGHTARROW",(int) CubeKeys.K_RIGHTARROW);

	        StrToCubeKey.Add("ALT",(int) CubeKeys.K_ALT);
	        StrToCubeKey.Add("CTRL",(int) CubeKeys.K_CTRL);
	        StrToCubeKey.Add("SHIFT",(int) CubeKeys.K_SHIFT);
	
	        StrToCubeKey.Add("F1",(int) CubeKeys.K_F1);
	        StrToCubeKey.Add("F2",(int) CubeKeys.K_F2);
	        StrToCubeKey.Add("F3",(int) CubeKeys.K_F3);
	        StrToCubeKey.Add("F4",(int) CubeKeys.K_F4);
	        StrToCubeKey.Add("F5",(int) CubeKeys.K_F5);
	        StrToCubeKey.Add("F6",(int) CubeKeys.K_F6);
	        StrToCubeKey.Add("F7",(int) CubeKeys.K_F7);
	        StrToCubeKey.Add("F8",(int) CubeKeys.K_F8);
	        StrToCubeKey.Add("F9",(int) CubeKeys.K_F9);
	        StrToCubeKey.Add("F10",(int) CubeKeys.K_F10);
	        StrToCubeKey.Add("F11",(int) CubeKeys.K_F11);
	        StrToCubeKey.Add("F12",(int) CubeKeys.K_F12);

	        StrToCubeKey.Add("INS",(int) CubeKeys.K_INS);
	        StrToCubeKey.Add("DEL",(int) CubeKeys.K_DEL);
	        StrToCubeKey.Add("PGDN",(int) CubeKeys.K_PGDN);
	        StrToCubeKey.Add("PGUP",(int) CubeKeys.K_PGUP);
	        StrToCubeKey.Add("HOME",(int) CubeKeys.K_HOME);
	        StrToCubeKey.Add("END",(int) CubeKeys.K_END);

	        StrToCubeKey.Add("MOUSE1",(int) CubeKeys.K_MOUSE1);
	        StrToCubeKey.Add("MOUSE2",(int) CubeKeys.K_MOUSE2);
	        StrToCubeKey.Add("MOUSE3",(int) CubeKeys.K_MOUSE3);
	        StrToCubeKey.Add("MOUSE4",(int) CubeKeys.K_MOUSE4);
	        StrToCubeKey.Add("MOUSE5",(int) CubeKeys.K_MOUSE5);

	        StrToCubeKey.Add("JOY1",(int) CubeKeys.K_JOY1);
	        StrToCubeKey.Add("JOY2",(int) CubeKeys.K_JOY2);
	        StrToCubeKey.Add("JOY3",(int) CubeKeys.K_JOY3);
	        StrToCubeKey.Add("JOY4",(int) CubeKeys.K_JOY4);

	        StrToCubeKey.Add("AUX1",(int) CubeKeys.K_AUX1);
	        StrToCubeKey.Add("AUX2",(int) CubeKeys.K_AUX2);
	        StrToCubeKey.Add("AUX3",(int) CubeKeys.K_AUX3);
	        StrToCubeKey.Add("AUX4",(int) CubeKeys.K_AUX4);
	        StrToCubeKey.Add("AUX5",(int) CubeKeys.K_AUX5);
	        StrToCubeKey.Add("AUX6",(int) CubeKeys.K_AUX6);
	        StrToCubeKey.Add("AUX7",(int) CubeKeys.K_AUX7);
	        StrToCubeKey.Add("AUX8",(int) CubeKeys.K_AUX8);
	        StrToCubeKey.Add("AUX9",(int) CubeKeys.K_AUX9);
	        StrToCubeKey.Add("AUX10",(int) CubeKeys.K_AUX10);
	        StrToCubeKey.Add("AUX11",(int) CubeKeys.K_AUX11);
	        StrToCubeKey.Add("AUX12",(int) CubeKeys.K_AUX12);
	        StrToCubeKey.Add("AUX13",(int) CubeKeys.K_AUX13);
	        StrToCubeKey.Add("AUX14",(int) CubeKeys.K_AUX14);
	        StrToCubeKey.Add("AUX15",(int) CubeKeys.K_AUX15);
	        StrToCubeKey.Add("AUX16",(int) CubeKeys.K_AUX16);
	        StrToCubeKey.Add("AUX17",(int) CubeKeys.K_AUX17);
	        StrToCubeKey.Add("AUX18",(int) CubeKeys.K_AUX18);
	        StrToCubeKey.Add("AUX19",(int) CubeKeys.K_AUX19);
	        StrToCubeKey.Add("AUX20",(int) CubeKeys.K_AUX20);
	        StrToCubeKey.Add("AUX21",(int) CubeKeys.K_AUX21);
	        StrToCubeKey.Add("AUX22",(int) CubeKeys.K_AUX22);
	        StrToCubeKey.Add("AUX23",(int) CubeKeys.K_AUX23);
	        StrToCubeKey.Add("AUX24",(int) CubeKeys.K_AUX24);
	        StrToCubeKey.Add("AUX25",(int) CubeKeys.K_AUX25);
	        StrToCubeKey.Add("AUX26",(int) CubeKeys.K_AUX26);
	        StrToCubeKey.Add("AUX27",(int) CubeKeys.K_AUX27);
	        StrToCubeKey.Add("AUX28",(int) CubeKeys.K_AUX28);
	        StrToCubeKey.Add("AUX29",(int) CubeKeys.K_AUX29);
	        StrToCubeKey.Add("AUX30",(int) CubeKeys.K_AUX30);
	        StrToCubeKey.Add("AUX31",(int) CubeKeys.K_AUX31);
	        StrToCubeKey.Add("AUX32",(int) CubeKeys.K_AUX32);

	        StrToCubeKey.Add("KP_HOME",(int)			CubeKeys.K_KP_HOME );
	        StrToCubeKey.Add("KP_UPARROW",(int)		CubeKeys.K_KP_UPARROW );
	        StrToCubeKey.Add("KP_PGUP",(int)			CubeKeys.K_KP_PGUP );
	        StrToCubeKey.Add("KP_LEFTARROW",(int)	CubeKeys.K_KP_LEFTARROW );
	        StrToCubeKey.Add("KP_5",(int)			CubeKeys.K_KP_5 );
	        StrToCubeKey.Add("KP_RIGHTARROW",(int)	CubeKeys.K_KP_RIGHTARROW );
	        StrToCubeKey.Add("KP_END",(int)			CubeKeys.K_KP_END );
	        StrToCubeKey.Add("KP_DOWNARROW",(int)	CubeKeys.K_KP_DOWNARROW );
	        StrToCubeKey.Add("KP_PGDN",(int)			CubeKeys.K_KP_PGDN );
	        StrToCubeKey.Add("KP_ENT-ER",(int)		CubeKeys.K_KP_ENTER );
	        StrToCubeKey.Add("KP_INS",(int)			CubeKeys.K_KP_INS );
	        StrToCubeKey.Add("KP_DEL",(int)			CubeKeys.K_KP_DEL );

	        StrToCubeKey.Add("KP_SLASH",(int)		CubeKeys.K_KP_SLASH );
	        StrToCubeKey.Add("KP_MINUS",(int)		CubeKeys.K_KP_MINUS );
	        StrToCubeKey.Add("KP_PLUS",(int)			CubeKeys.K_KP_PLUS );

	        StrToCubeKey.Add("CAPSLOCK",(int)		CubeKeys.K_CAPSLOCK );

	        StrToCubeKey.Add("MWHEELUP",(int) CubeKeys.K_MWHEELUP );
	        StrToCubeKey.Add("MWHEELDOWN",(int) CubeKeys.K_MWHEELDOWN );

	        StrToCubeKey.Add("PAUSE",(int) CubeKeys.K_PAUSE);
            StrToCubeKey.Add("~", (int)CubeKeys.K_CONSOLE);

	        StrToCubeKey.Add("SEMICOLON", ';');	// because a raw semicolon seperates commands

            KeyToCubeKey.Add(8, (int)CubeKeys.K_BACKSPACE);
            KeyToCubeKey.Add(9, (int)CubeKeys.TAB);
            KeyToCubeKey.Add(19, (int)CubeKeys.K_PAUSE);
            KeyToCubeKey.Add(13, (int)CubeKeys.K_ENTER);
            KeyToCubeKey.Add(27, (int)CubeKeys.K_ESCAPE);
            KeyToCubeKey.Add(32, (int)CubeKeys.K_SPACE);

            KeyToCubeKey.Add(96, (int)CubeKeys.K_KP_INS);
            KeyToCubeKey.Add(97, (int)CubeKeys.K_KP_END);
            KeyToCubeKey.Add(98, (int)CubeKeys.K_KP_DOWNARROW);
            KeyToCubeKey.Add(99, (int)CubeKeys.K_KP_PGDN);
            KeyToCubeKey.Add(100, (int)CubeKeys.K_KP_LEFTARROW);
            KeyToCubeKey.Add(101, (int)CubeKeys.K_KP_5);
            KeyToCubeKey.Add(102, (int)CubeKeys.K_KP_RIGHTARROW);
            KeyToCubeKey.Add(103, (int)CubeKeys.K_KP_HOME);
            KeyToCubeKey.Add(104, (int)CubeKeys.K_KP_UPARROW);
            KeyToCubeKey.Add(105, (int)CubeKeys.K_KP_PGUP);
            
            KeyToCubeKey.Add(107, (int)CubeKeys.K_KP_PLUS);
            KeyToCubeKey.Add(108, (int)CubeKeys.K_KP_ENTER); // separator?
            KeyToCubeKey.Add(109, (int)CubeKeys.K_KP_MINUS);
            KeyToCubeKey.Add(110, (int)CubeKeys.K_KP_DEL);
            KeyToCubeKey.Add(111, (int)CubeKeys.K_KP_SLASH);

            KeyToCubeKey.Add(112, (int)CubeKeys.K_F1);
            KeyToCubeKey.Add(113, (int)CubeKeys.K_F2);
            KeyToCubeKey.Add(114, (int)CubeKeys.K_F3);
            KeyToCubeKey.Add(115, (int)CubeKeys.K_F4);
            KeyToCubeKey.Add(116, (int)CubeKeys.K_F5);
            KeyToCubeKey.Add(117, (int)CubeKeys.K_F6);
            KeyToCubeKey.Add(118, (int)CubeKeys.K_F7);
            KeyToCubeKey.Add(119, (int)CubeKeys.K_F8);
            KeyToCubeKey.Add(120, (int)CubeKeys.K_F9);
            KeyToCubeKey.Add(121, (int)CubeKeys.K_F10);
            KeyToCubeKey.Add(122, (int)CubeKeys.K_F11);
            KeyToCubeKey.Add(123, (int)CubeKeys.K_F12);


            KeyToCubeKey.Add(16, (int)CubeKeys.K_SHIFT);
            KeyToCubeKey.Add(17, (int)CubeKeys.K_CTRL);
            KeyToCubeKey.Add(18, (int)CubeKeys.K_ALT);

            //KeyToCubeKey.Add(160, (int)CubeKeys.K_SHIFT);
            //KeyToCubeKey.Add(161, (int)CubeKeys.K_SHIFT);
            //KeyToCubeKey.Add(162, (int)CubeKeys.K_CTRL);
            //KeyToCubeKey.Add(163, (int)CubeKeys.K_CTRL);
            
            KeyToCubeKey.Add(186, (int)';');
            KeyToCubeKey.Add(187, (int)'+');
            KeyToCubeKey.Add(188, (int)',');
            KeyToCubeKey.Add(189, (int)'-');
            KeyToCubeKey.Add(190, (int)'.');
            KeyToCubeKey.Add(192, (int)'~');
            KeyToCubeKey.Add(220, (int)'~');

            KeyToCubeKey.Add(33, (int)CubeKeys.K_PGUP);
            KeyToCubeKey.Add(34, (int)CubeKeys.K_PGDN);
            KeyToCubeKey.Add(35, (int)CubeKeys.K_END);
            KeyToCubeKey.Add(36, (int)CubeKeys.K_HOME);
            KeyToCubeKey.Add(45, (int)CubeKeys.K_INS);
            KeyToCubeKey.Add(46, (int)CubeKeys.K_DEL);

            KeyToCubeKey.Add(37, (int)CubeKeys.K_LEFTARROW);
            KeyToCubeKey.Add(38, (int)CubeKeys.K_UPARROW);
            KeyToCubeKey.Add(39, (int)CubeKeys.K_RIGHTARROW);
            KeyToCubeKey.Add(40, (int)CubeKeys.K_DOWNARROW);
            
            Commands.Instance.AddCommand("bind", new CommandDelegate(Cmd_Bind));
            Commands.Instance.AddCommand("unbind", new CommandDelegate(Cmd_Unbind));
            Commands.Instance.AddCommand("unbindall", new CommandDelegate(Cmd_Unbindall));
            Commands.Instance.AddCommand("bindlist", new CommandDelegate(Cmd_Bindlist));

            InitBinds();
        }

        public string PrintCubeKey(int key) 
        {
            string name = Enum.GetName(typeof(CubeKeys), key);
            if(name == null)
                return ""+(char)key;
            return name;
        }

        public int GetCubeKey(string str)
        {
            if (str == null)
                return 0;
            str = str.ToUpper();
            if (!StrToCubeKey.ContainsKey(str))
            {
                str = str.Trim();
                // a-z || 0-9 passes as a key always..
                if (str.Length == 1 && (str[0] >= 'A' && str[0] <= 'Z') || (str[0] >= '0' && str[0] <= '9'))
                    return (int)str[0];
                return 0;
            }

            return StrToCubeKey[str];
        }

        public int GetCubeKey(int keyVal)
        {
            if (!KeyToCubeKey.ContainsKey(keyVal))
            {
                if(!((keyVal >= 'A' && keyVal <= 'Z') || (keyVal >= '0' && keyVal <= '9')))
                    Common.Instance.WriteLine("Check val: {0} {1}", keyVal, (char)keyVal);
                return keyVal;
            }
            else
                return KeyToCubeKey[keyVal];
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
            //SetBind("TILDE", "toggleconsole");
        }

        // Bind command implementation
        public void Cmd_Bind(string[] tokens)
        {
            if (tokens.Length < 2)
            {
                Common.Instance.WriteLine("bind <key> [command] : attach a command to a key");
                return;
            }
            int key = GetCubeKey(tokens[1]);
            if (key == 0)
            {
                Common.Instance.WriteLine("\"{0}\" isn't an valid key.", tokens[1]);
                return;
            }

            // Display bind
            if (tokens.Length == 2)
            {
                if (binds.ContainsKey(key))
                    Common.Instance.WriteLine("\"{0}\" = \"{1}\"", tokens[1], binds[key]);
                else
                    Common.Instance.WriteLine("\"{0}\" is not bound", tokens[1]);
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
                Common.Instance.WriteLine("unbind <key> : remove commands from a key");
                return;
            }

            int key = GetCubeKey(tokens[1]);
            if (key == 0)
            {
                Common.Instance.WriteLine("\"{0}\" isn't a valid key", tokens[1]);
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
            foreach (int key in binds.Keys)
            {
                string bind = binds[key];
                if (binds.Count > 0)
                    writer.WriteLine("bind {0} \"{1}\"", PrintCubeKey(key), bind);
            }
        }

        // Prints bindslist
        private void Cmd_Bindlist(string[] tokens)
        {
            foreach (int key in binds.Keys)
            {
                string bind = binds[key];
                if (binds.Count > 0)
                    Common.Instance.WriteLine("{0} \"{1}\"", PrintCubeKey(key), bind);
            }
        }
        public void ParseBinding(Keys key, bool down, long time)
        {
            ParseBinding(GetCubeKey((int)key), down, time);
        }

        public void ParseBinding(Keys key, bool down)
        {
            ParseBinding(GetCubeKey((int)key), down, 0);
        }

        // Execute the commands in the bind string
        public void ParseBinding(int key, bool down, long time)
        {
            if (!binds.ContainsKey(key) || binds[key] == "")
            {
                if(down)Common.Instance.WriteLine("Key '{0}' is not bound.", PrintCubeKey(key));
                return;
            }
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
                    string cmd = string.Format("{0}{1} {2} {3}\n", (down) ? '+' : '-', bind.Substring(i + 1), key, time);
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
            int keyv = GetCubeKey(key);
            if (keyv == 0)
            {
                Common.Instance.WriteLine("Unable to bind/parse key \"{0}\"", key);
                return;
            }
            SetBind(keyv, binding);
        }

        // Bind a key
        public void SetBind(CubeKeys key, string binding)
        {
            SetBind((int)key, binding);
        }

        public void SetBind(int key, string binding) 
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
            foreach (int key in binds.Keys)
            {
                string bind = binds[key];
                if (bind == null || bind.Length == 0)
                    continue;
                writer.WriteLine("bind {0} \"{1}\"", PrintCubeKey(key), bind);
            }
        }
        



        // Singleton
        private static readonly KeyHags _Instance = new KeyHags();
        public static KeyHags Instance { get { return _Instance; } }

    }
}
