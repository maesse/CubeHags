using System;
using System.Collections.Generic;
 
using System.Text;

namespace CubeHags.client.common
{
    public delegate void CommandDelegate(string[] args);

    public sealed class Commands
    {
        public enum EXECTYPE
        {
            EXEC_NOW,
            EXEC_INSERT,
            EXEC_APPEND
        }

        private const int MAX_CMD_LINE = 1024;
        private const int MAX_CMD_BUFFER = 1024;

        private Dictionary<string, CommandDelegate> commands = new Dictionary<string, CommandDelegate>();
        
        private StringBuilder cmd_text = new StringBuilder(MAX_CMD_BUFFER);
        private int cursize = 0;
        private int wait = 0;
        

        Commands()
        {
            Init();
        }

        public void ExecuteText(EXECTYPE exec_when, string text)
        {
            switch (exec_when)
            {
                case EXECTYPE.EXEC_NOW:
                    if (text != null && text.Length > 0)
                    {
                        ExecuteString(text);
                    }
                    else
                    {
                        Execute();
                        // Printf
                    }
                    break;
                case EXECTYPE.EXEC_APPEND:
                    AddText(text);
                    break;
                case EXECTYPE.EXEC_INSERT:
                    InsertText(text);
                    break;

                default:
                    System.Console.WriteLine("Something is wrong");
                    break;
            }
        }

        // Adds command text at the end of the buffer, does NOT add a final \n
        public void AddText(string text)
        {   
            if (cursize + text.Length >= MAX_CMD_BUFFER)
            {
                System.Console.WriteLine("AddText: overflow");
                return;
            }
            // Debug
            //System.Console.WriteLine("CommandBuffer:AddText : \"{0}\"", text);

            cmd_text.Append(text);
            cursize += text.Length;
        }

        // Adds command text immediately after the current command
        // Adds a \n to the text
        public void InsertText(string text)
        {
            int len = text.Length + 1;
            if (len + cursize > MAX_CMD_BUFFER)
            {
                System.Console.WriteLine("InsertText: overflow");
                return;
            }

            text += '\n';
            cmd_text.Insert(0, text);
            cursize += len;
        }

        public void Execute()
        {
            string line;

            while (cursize > 0)
            {

                if (wait > 0)
                {
                    wait--;
                    break;
                }

                int quotes = 0;
                int i;
                for (i = 0; i < cursize; i++)
                {
                    if (cmd_text[i] == '"')
                        quotes++;
                    if((quotes&1)!=1 && cmd_text[i] == ';')
                        break; // dont break if inside a quoted string
                    if (cmd_text[i] == '\n' || cmd_text[i] == '\r')
                        break;
                }

                // Cap size
                if(i >= (MAX_CMD_LINE -1)) 
                {
                    i = MAX_CMD_LINE - 1;
                }

                // extract line from command buffer
                line = cmd_text.ToString(0, i);

                // delete the text from the command buffer and move remaining commands down
                // this is necessary because commands (exec) can insert data at the
                // beginning of the text buffer
                if (i == cursize)
                    cursize = 0;
                else
                {
                    i++;
                    cursize -= i;
                    // Remove line from commandbuffer
                    cmd_text.Remove(0, i); // = cmd_text.ToString(i,cursize);
                }

                // execute the command line
                ExecuteString(line);
            }
        }

        private void ExecuteString(string text)
        {
            // Parse string
            string[] tokens = TokenizeString(text);

            if (tokens.Length == 0)
                return;

            // Check command delegates
            if (commands.ContainsKey(tokens[0].ToLower()))
            {
                commands[tokens[0]](tokens);
                return;
            }

            // Check cvars
            if (CVars.Instance.HandleFromCommand(tokens))
                return;
        }

        public void AddCommand(string name, CommandDelegate function)
        {
            if (commands.ContainsKey(name.ToLower()))
            {
                System.Console.WriteLine("AddCommand: {0} is already defined", name);
            }
            else
            {
                commands.Add(name.ToLower(), function);
            }
        }

        public void RemoveCommand(string name)
        {
            if (commands.ContainsKey(name.ToLower()))
            {
                commands.Remove(name.ToLower());
            }
        }


        // Returns  a single string containing argv(1) to argv(argc()-1)
        public static string Args(string[] tokens)
        {
            StringBuilder str = new StringBuilder();
            for (int i = 1; i < tokens.Length; i++)
            {
                str.Append(tokens[i]);
                if (i < tokens.Length - 1)
                    str.Append(" ");
            }
            return str.ToString();
        }

        // Returns  a single string containing argv(arg) to argv(argc()-1)
        public static string ArgsFrom(string[] tokens, int arg)
        {
            StringBuilder str = new StringBuilder();
            for (int i = arg; i < tokens.Length; i++)
            {
                str.Append(tokens[i]);
                if (i < tokens.Length - 1)
                    str.Append(" ");
            }
            return str.ToString();
        }

        public static string[] TokenizeString(string text)
        {
            return TokenizeString2(text, false);
        }

        public static string[] TokenizeStringIgnoreQuotes(string text)
        {
            return TokenizeString2(text, true);
        }

        private static string[] TokenizeString2(string text_in, bool ignoreQuotes)
        {
            string text_out = "";
            List<string> tokens = new List<string>();

            if (text_in == null)
                return null;

            string text = text_in;
            int textoffset = 0;
            while (true)
            {
                if (tokens.Count == 1024)
                {
                    return tokens.ToArray(); // avoid problems.
                }
                text_out = "";
                while (true)
                {
                    
   
                    // skip whitespace
                    while (textoffset < text.Length && text[textoffset] <= ' ')
                    {
                        textoffset++;
                    }
                    if (textoffset >= text.Length)
                    {
                        return tokens.ToArray();			// all tokens parsed
                    }

                    // skip // comments
                    if (text[textoffset+0] == '/' && text[textoffset+1] == '/')
                    {
                        return tokens.ToArray(); ;			// all tokens parsed
                    }

                    // skip /* */ comments
                    if (text[textoffset+0] == '/' && text[textoffset+1] == '*')
                    {
                        while (textoffset < text.Length && (text[textoffset+0] != '*' || text[textoffset+1] != '/'))
                        {
                            textoffset++;
                        }
                        if (textoffset >= text.Length)
                        {
                            return tokens.ToArray(); ;		// all tokens parsed
                        }
                        textoffset += 2;
                    }
                    else
                    {
                        break;			// we are ready to parse a token
                    }
                }

                // Handle quoted string
                if (!ignoreQuotes && text[textoffset] == '"')
                {
                    textoffset++;
                    while (textoffset < text.Length && text[textoffset] != '"')
                    {
                        text_out += text[textoffset++];
                    }
                    tokens.Add(text_out);

                    if (textoffset >= text.Length)
                        return tokens.ToArray();

                    textoffset++;
                    continue;
                }

                // regular token
                //??

                // skip until whitespace, quote or command
                while (textoffset < text.Length && text[textoffset] > ' ')
                {
                    if (!ignoreQuotes && text[textoffset] == '"')
                    {
                        break;
                    }

                    if (text[textoffset] == '/' && text[textoffset+1] == '/')
                    {
                        break;
                    }

                    // Skip /**/ comments
                    if (text[textoffset] == '/' && text[textoffset+1] == '*')
                    {
                        break;
                    }

                    text_out += (char)text[textoffset];
                    textoffset++;
                }

                tokens.Add(text_out);

                if (textoffset >= text.Length)
                    return tokens.ToArray();
            }
        }

        // Echo command implementation
        void Cmd_Echo(string[] tokens)
        {
            System.Console.WriteLine(Args(tokens));
        }

        // Alias command implementation
        void Cmd_Alias(string[] tokens)
        {
            if (tokens.Length != 3)
            {
                System.Console.WriteLine("alias [+/-]<name> \"<command>\" : define an alias");
                return;
            }

            // TODO
        }

        // Wait command implementation
        void Cmd_Wait(string[] tokens)
        {
            // Accepts arguments for number of waits
            if (tokens.Length == 2)
            {
                try
                {
                    wait = int.Parse(tokens[1]);
                }
                catch
                {
                    wait = 0;
                }

                if (wait < 0)
                    wait = 1; // ignore the arguemnt

            }
            else
                wait = 1;
        }

        private void Init()
        {
            AddCommand("wait", new CommandDelegate(Cmd_Wait));
            AddCommand("echo", new CommandDelegate(Cmd_Echo));
            AddCommand("alias", new CommandDelegate(Cmd_Alias));
        }

        // Singleton
        private static readonly Commands _Instance = new Commands();
        public static Commands Instance { get { return _Instance; } }
    }
}
