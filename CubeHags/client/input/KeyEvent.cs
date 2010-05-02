using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CubeHags.client
{
    public class KeyEvent
    {
        [Flags]
        public enum Modifiers
        {
            NONE        = 0,
            CAPSLOCK    = 1,
            SHIFT       = 2,
            CONTROL     = 4,
            ALT         = 8
        }

        public bool pressed { get; set; }
        public Keys key { get; set; }
        public Modifiers Mod { get; set; }

        public KeyEvent(bool pressed, Keys key, Modifiers mod)
        {
            this.Mod = mod;
            this.pressed = pressed;
            this.key = key;
        }

        override public string ToString()
        {
            return "KeyEvent: key={" + key + "} pressed=" + pressed;
        }
    }
}
