using System;
using System.Collections.Generic;
 
using System.Text;
using System.IO;
using CubeHags.common;

namespace CubeHags.client.common
{
    [Flags]
    public enum CVarFlags : int
    {
        NONE = 0,
        ARCHIVE = 1, // will save to file
        INIT = 2, // No change from console
        LATCH = 4, // Value will not have effect before cvar is used in the game again
        ROM = 8, // Read-only by user
        USER_CREATED = 16, // Created by user
        SYSTEM_INFO = 32,
        SERVER_CREATED = 64,
        NONEXISTANT = 128,
        SERVER_INFO = 256,
        TEMP = 512,
        USER_INFO = 1024,
        CHEAT = 2048
    }

    public class CVar
    {
        public string Name;
        public string String;
        public string ResetString;
        public string LatchedString;
        public CVarFlags Flags = CVarFlags.NONE;
        public bool Modified;
        public int ModificationCount;
        public float Value;
        public int Integer;
        public bool Validate; // CVar has been changed, and needs to be validated
        public bool Integral; // True if value is integer
        public float min, max; // Min/Max range set with CheckRange()
        // If value is an integer and == 1, return true.
        public bool Bool { get { if (!Integral) return false; return (Integer == 1); } } 
    }

    sealed class CVars
    {
        Dictionary<string, CVar> vars = new Dictionary<string, CVar>();
        public CVarFlags modifiedFlags = CVarFlags.NONE;

        CVars()
        {
        }

        public CVar FindVar(string name)
        {
            if (vars.ContainsKey(name))
                return vars[name];

            return null;
        }

        // Returns float value of cvar
        public float VariableValue(string name)
        {
            CVar var = FindVar(name);
            if (var == null)
                return 0f;

            return var.Value;
        }

        // Returns integer value of cvar
        public int VariableIntegerValue(string name)
        {
            CVar var = FindVar(name);
            if (var == null)
                return 0;

            return var.Integer;
        }

        // Returns string value of cvar
        public string VariableString(string name)
        {
            CVar var = FindVar(name);
            if (var == null)
                return string.Empty;

            return var.String;
        }

        // Returns flags of cvar
        public CVarFlags Flags(string name)
        {
            CVar var = FindVar(name);
            if (var == null)
                return CVarFlags.NONEXISTANT;
            return var.Flags;
        }

        // Applies a (permanent until changed) range-check on a cvar.
        public void CheckRange(CVar var, float min, float max, bool integral)
        {
            var.Validate = true;
            var.min = min;
            var.max = max;
            var.Integral = integral;

            // Force initial range check
            Set(var.Name, var.String);
        }

        // Create an infostring with cvars the fulfill the cVarFlags attribute
        public string InfoString(CVarFlags cVarFlags)
        {
            StringBuilder str = new StringBuilder();

            foreach (CVar var in vars.Values)
            {
                if (var.Name != null && var.Name.Length > 0 && (var.Flags & cVarFlags) == cVarFlags)
                {
                    str.Append(string.Format("\\{0}\\{1}", var.Name, var.String));
                }
            }
            return str.ToString();
        }

        // Handles variable inspection and changing from the console
        public bool HandleFromCommand(string[] tokens)
        {
            // Check variables
            CVar nvar = FindVar(tokens[0]);
            if (nvar == null)
                return false;

            // Perform a varable print
            if (tokens.Length == 1)
            {
                // Print TODO
                Print(nvar);
                return true;
            }

            // Build args
            StringBuilder args = new StringBuilder();
            for (int i = 1; i < tokens.Length; i++)
			{
                args.Append(tokens[i]);
                args.Append(" ");
			}
            // set the value if forcing isn't required
            Set2(nvar.Name, args.ToString(), false);
            return true;
        }

        public static string Validate(CVar var, string value, bool warn)
        {
            if (!var.Validate)
                return value;

            if (value.Equals("") || value == null)
                return value;

            float valuef;
            bool changed = false;
            if(float.TryParse(value, out valuef)) 
            {
                if (var.Integral)
                {
                    if (valuef != (int)valuef)
                    {
                        if (warn)
                            System.Console.Write("WARNING: cvar '{0}' must be integral", var.Name);

                        valuef = (int)valuef;
                        changed = true;
                    }
                }
            }
            else
            {
                if (warn)
                    System.Console.Write("WARNING: cvar '{0}' must be numeric", var.Name);

                valuef = float.Parse(var.ResetString);
                changed = true;
            }

            if (valuef < var.min)
            {
                if (warn)
                {
                    if (changed)
                        System.Console.Write(" and is");
                    else
                        System.Console.Write("WARNING: cvar '{0}'", var.Name);

                    System.Console.Write(" out of range (min {0})", var.min);
                }
                valuef = var.min;
                changed = true;
            }
            else if (valuef > var.max)
            {
                if (warn)
                {
                    if (changed)
                        System.Console.Write(" and is");
                    else
                        System.Console.Write("WARNING: cvar '{0}'", var.Name);

                    System.Console.Write(" out of range (max {0})", var.max);
                }
                valuef = var.max;
                changed = true;
            }

            if (changed)
            {
                if (warn)
                    System.Console.WriteLine(", setting to {0}", value);

                return "" + valuef;
            }

            return value;
        }

        // Gets a cvar. If it doesn't exist, it will be created
        public CVar Get(string name, string value, CVarFlags flags)
        {
            if (name == null || value == null || name.Equals(""))
            {
                System.Console.WriteLine("FATAL: CVar Get w/ null arguments");
                return null;
            }

            if (!ValidateString(name))
            {
                System.Console.WriteLine("Invalid cvar name string: {0}", name);
                name = "BADNAME";
            }

            CVar nvar = FindVar(name);
            
            if (nvar != null)
            {
                CVar var = nvar;
                value = Validate(var, value, false);

                // if the C code is now specifying a variable that the user already
                // set a value for, take the new value as the reset value
                if((var.Flags & CVarFlags.USER_CREATED) == CVarFlags.USER_CREATED)
                {
                    var.Flags &= ~CVarFlags.USER_CREATED;
                    var.ResetString = value;

                    if ((flags & CVarFlags.ROM) == CVarFlags.ROM)
                    {
                        // this variable was set by the user,
                        // so force it to value given by the engine.

                        var.LatchedString = value;
                    }
                }

                var.Flags = flags;

                // only allow one non-empty reset string without a warning
                if (var.ResetString == null || var.ResetString.Equals(""))
                    var.ResetString = value;
                else if (!value.Equals("") && value.Equals(var.ResetString))
                {

                }

                // if we have a latched string, take that value now
                if (var.LatchedString != null)
                {
                    string s = var.LatchedString;
                    var.LatchedString = null;
                    Set(name, s);
                }

                // ZOID--needs to be set so that cvars the game sets as 
                // SERVERINFO get sent to clients
                modifiedFlags |= flags;

                return var;
            }

            //
            // allocate a new cvar
            //

            CVar cvar = new CVar();
            cvar.Name = name;
            cvar.String = value;
            cvar.Modified = true;
            cvar.ModificationCount = 1;
            float val;
            int val2;
            if(float.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                cvar.Value = val;
            if (int.TryParse(value, out val2))
            {
                cvar.Integer = val2;
                cvar.Integral = true;
            }
            cvar.ResetString = value;
            cvar.Validate = false;
            cvar.Flags = flags;
            // note what types of cvars have been modified (userinfo, archive, serverinfo, systeminfo)
            modifiedFlags |= cvar.Flags;

            vars.Add(name, cvar);

            return cvar;
        }

        // Sets value of a cvar
        private CVar Set2(string name, string value, bool force)
        {
            if (!ValidateString(name))
            {
                System.Console.WriteLine("WARNING: invalid cvar name string {0}", name);
                name = "BADNAME";
            }

            CVar nvar = FindVar(name);
            if (nvar == null)
            {
                if (value == null)
                    return null;

                // create it
                if (!force)
                    return Get(name, value, CVarFlags.USER_CREATED);
                else
                    return Get(name, value, CVarFlags.NONE);
            }

            CVar var = nvar;

            if (value == null || value.Equals(""))
                value = var.ResetString;

            value = Validate(var, value, true);

            if ((var.Flags & CVarFlags.LATCH) == CVarFlags.LATCH && var.LatchedString != null)
            {
                if (value.Equals(var.String))
                {
                    var.LatchedString = null;
                    return var;
                }

                if (value.Equals(var.LatchedString))
                    return var;
            }
            else if (value.Equals(var.String))
                return var;

            // note what types of cvars have been modified (userinfo, archive, serverinfo, systeminfo)
            modifiedFlags |= var.Flags;

            if (!force)
            {
                if ((var.Flags & CVarFlags.ROM) == CVarFlags.ROM)
                {
                    System.Console.WriteLine("{0} is read only.", name);
                    return var;
                }

                if ((var.Flags & CVarFlags.INIT) == CVarFlags.INIT)
                {
                    System.Console.WriteLine("{0} is write protected.", name);
                    return var;
                }

                if ((var.Flags & CVarFlags.LATCH) == CVarFlags.LATCH)
                {
                    if (var.LatchedString != null)
                    {
                        if (value.Equals(var.LatchedString))
                            return var;
                        var.LatchedString = null;
                    }
                    else
                    {
                        if (value.Equals(var.String))
                            return var;
                    }

                    System.Console.WriteLine("{0} will be changed upon restarting", name);
                    var.LatchedString = value;
                    var.Modified = true;
                    var.ModificationCount++;
                    return var;
                }
            }
            else
            {
                if (var.LatchedString != null)
                    var.LatchedString = null;
            }

            if (value.Equals(var.String))
                return var; // not changed

            var.Modified = true;
            var.ModificationCount++;

            var.String = value;
            float val;
            int val2;
            if (float.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                var.Value = val;
            if (int.TryParse(value, out val2))
            {
                var.Integer = val2;
                var.Integral = true;
            }
            else
                var.Integral = false;

            return var;
        }

        // Append lines containing "set variable value" for all variables
        // with the archive flag set to true
        public void WriteVariables(StreamWriter writer)
        {
            foreach (CVar cvar in vars.Values)
            {
                if ((cvar.Flags & CVarFlags.ARCHIVE) != CVarFlags.ARCHIVE)
                    continue;

                // write the latched value, even if it hasn't taken effect yet
                if (cvar.LatchedString != null)
                    writer.WriteLine("seta {0} \"{1}\"", cvar.Name, cvar.LatchedString);
                else
                    writer.WriteLine("seta {0} \"{1}\"", cvar.Name, cvar.String);
            }
        }

        public CVar Set(string name, string value)
        {
            return Set2(name, value, true);
        }

        public static bool ValidateString(string s)
        {
            if (s == null)
                return false;
            if (s.Contains("\\"))
                return false;
            if (s.Contains("\""))
                return false;
            if (s.Contains(";"))
                return false;

            return true;
        }

        public void Reset(string name)
        {
            Set2(name, null, false);
        }

        public void ForceReset(string name)
        {
            Set2(name, null, true);
        }

        void Print_f(string[] tokens)
        {
            if (tokens.Length != 2)
            {
                Common.Instance.WriteLine("usage: print <variable>");
                return;
            }

            string name = tokens[1];
            CVar cv = FindVar(name);
            if (cv != null)
                Print(cv);
            else
                Common.Instance.WriteLine("Cvar {0} doesn't exist.", name);
        }

        /*
        ============
        Cvar_Print

        Prints the value, default, and latched string of the given variable
        ============
        */
        public void Print(CVar var)
        {
            Common.Instance.Write("\"{0}\" is \"{1}\"^7", var.Name, var.String);
            if ((var.Flags & CVarFlags.ROM) != CVarFlags.ROM)
            {
                if (var.String.Equals(var.ResetString))
                    Common.Instance.Write(", the default");
                else
                    Common.Instance.Write(" default: \"{0}\"^7", var.ResetString);
            }

            Common.Instance.Write("\n");
            if (var.LatchedString != null)
                Common.Instance.WriteLine("latched: \"{0}\"^7", var.LatchedString);
        }

        /*
        ============
        Cvar_Toggle_f

        Toggles a cvar for easy single key binding, optionally through a list of
        given values
        ============
        */
        void Toggle_f(string[] tokens)
        {
            if (tokens.Length < 2)
            {
                Common.Instance.WriteLine("usage: toggle <variable> [value1, value2, ...]");
                return;
            }

            if (tokens.Length == 2)
            {
                Set2(tokens[1], "" + VariableValue(tokens[1]), false);
                return;
            }

            if (tokens.Length == 3)
            {
                Common.Instance.WriteLine("toggle: nothing to toggle to.");
                return;
            }

            string curval = VariableString(tokens[1]);
            // don't bother checking the last arg for a match since the desired
            // behaviour is the same as no match (set to the first argument)
            int c = tokens.Length;
            for (int i = 2; i + 1 < c; i++)
            {
                if (curval.Equals(tokens[i]))
                {
                    Set2(tokens[1], tokens[i + 1], false);
                    return;
                }
            }

            // fallback
            Set2(tokens[1], tokens[2], false);
        }


        /*
        ============
        Cvar_Set_f

        Allows setting and defining of arbitrary cvars from console, even if they
        weren't declared in C code.
        ============
        */
        void Set_f(string[] tokens)
        {
            string cmd = tokens[0];

            if (tokens.Length < 2)
            {
                Common.Instance.WriteLine("usage: {0} <variable> <value>", cmd);
                return;
            }

            if (tokens.Length == 2)
            {
                Print_f(tokens);
                return;
            }

            CVar v = Set2(tokens[1], Commands.ArgsFrom(tokens, 2), false);
            if (v == null)
                return;

            if (cmd.Length < 4)
                return;

            switch (cmd[3])
            {
                case 'a':
                    if ((v.Flags & CVarFlags.ARCHIVE) != CVarFlags.ARCHIVE)
                    {
                        v.Flags |= CVarFlags.ARCHIVE;
                        modifiedFlags |= CVarFlags.ARCHIVE;
                    }
                    break;
                case 'u':
                    if ((v.Flags & CVarFlags.USER_INFO) != CVarFlags.USER_INFO)
                    {
                        v.Flags |= CVarFlags.USER_INFO;
                        modifiedFlags |= CVarFlags.USER_INFO;
                    }
                    break;
                case 's':
                    if ((v.Flags & CVarFlags.SERVER_INFO) != CVarFlags.SERVER_INFO)
                    {
                        v.Flags |= CVarFlags.SERVER_INFO;
                        modifiedFlags |= CVarFlags.SERVER_INFO;
                    }
                    break;
            }
        }

        void Reset_f(string[] tokens)
        {
            if (tokens.Length != 2)
            {
                Common.Instance.WriteLine("usage: reset <variable>");
                return;
            }

            Reset(tokens[1]);
        }

        void Unset_f(string[] tokens)
        {
            if (tokens.Length != 2)
            {
                Common.Instance.WriteLine("usage: unset <variable>");
                return;
            }

            CVar cv = FindVar(tokens[1]);
            if (cv == null)
                return;

            if ((cv.Flags & CVarFlags.USER_CREATED) == CVarFlags.USER_CREATED)
            {
                Unset(cv);
            }
            else
                Common.Instance.WriteLine("Error: {0}: Variable {1} is not user created.", tokens[0], cv.Name);
        }

        public void Unset(CVar var)
        {
            // remove from dictionary
            if (vars.ContainsKey(var.Name))
                vars.Remove(var.Name);
        }

        public void Init()
        {
            Commands.Instance.AddCommand("print", new CommandDelegate(Print_f));
            Commands.Instance.AddCommand("toggle", new CommandDelegate(Toggle_f));
            Commands.Instance.AddCommand("set", new CommandDelegate(Set_f));
            Commands.Instance.AddCommand("seta", new CommandDelegate(Set_f));
            Commands.Instance.AddCommand("setu", new CommandDelegate(Set_f));
            Commands.Instance.AddCommand("sets", new CommandDelegate(Set_f));
            Commands.Instance.AddCommand("reset", new CommandDelegate(Reset_f));
            Commands.Instance.AddCommand("unset", new CommandDelegate(Unset_f));
        }

        // Singleton implementation
        private static readonly CVars _Instance = new CVars();
        public static CVars Instance
        {
            get
            {
                return _Instance;
            }
        }

        
    }
}
