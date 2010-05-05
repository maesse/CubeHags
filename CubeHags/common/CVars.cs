using System;
using System.Collections.Generic;
 
using System.Text;

namespace CubeHags.client.common
{
    [Flags]
    public enum CVarFlags : int
    {
        NONE = 0,
        ARCHIVE, // will save to file
        INIT, // No change from console
        LATCH, // Value will not have effect before cvar is used in the game again
        ROM, // Read-only by user
        USER_CREATED, // Created by user
        SYSTEM_INFO,
        SERVER_CREATED,
        NONEXISTANT,
        SERVER_INFO,
        TEMP,
        USER_INFO
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
        public bool Validate;
        public bool Integral;
        public float min, max;
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

        public float VariableValue(string name)
        {
            CVar var = FindVar(name);
            if (var == null)
                return 0f;

            return var.Value;
        }

        public int VariableIntegerValue(string name)
        {
            CVar var = FindVar(name);
            if (var == null)
                return 0;

            return var.Integer;
        }

        public string VariableString(string name)
        {
            CVar var = FindVar(name);
            if (var == null)
                return string.Empty;

            return var.String;
        }

        public CVarFlags Flags(string name)
        {
            CVar var = FindVar(name);
            if (var == null)
                return CVarFlags.NONEXISTANT;
            return var.Flags;
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
            if(float.TryParse(value, out val))
                cvar.Value = val;
            if(int.TryParse(value, out val2))
                cvar.Integer = val2;
            cvar.ResetString = value;
            cvar.Validate = false;
            cvar.Flags = flags;

            vars.Add(name, cvar);

            return cvar;
        }

        public CVar Set2(string name, string value, bool force)
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
            if (float.TryParse(value, out val))
                var.Value = val;
            if (int.TryParse(value, out val2))
                var.Integer = val2;

            return var;
        }

        public CVar Set(string name, string value)
        {
            return Set2(name, value, true);
        }

        public static bool ValidateString(string s)
        {
            if (s == null)
                return false;
            if (s.Contains(""+'\\'))
                return false;
            if (s.Contains("" + '\"'))
                return false;
            if (s.Contains("" + ';'))
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



        // Singleton implementation
        private static readonly CVars _Instance = new CVars();
        public static CVars Instance
        {
            get
            {
                return _Instance;
            }
        }

        public string InfoString(CVarFlags cVarFlags)
        {
            StringBuilder str = new StringBuilder();

            foreach (CVar var in vars.Values)
            {
                if (var.Name != null && var.Name != "" && (var.Flags & cVarFlags) == cVarFlags)
                {
                    str.Append(string.Format("\\{0}\\{1}", var.Name, var.String));
                }
            }
            return str.ToString();
        }

        
    }
}
