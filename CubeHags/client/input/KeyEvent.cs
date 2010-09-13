using System;
using System.Collections.Generic;
 
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

        public bool IsUpDownEvent = true;

        // KeyPress event
        public char Character { get; set; }

        // Key Up/Down events
        public bool pressed { get; set; }
        public Keys key { get; set; }
        public Modifiers Mod { get; set; }        

        public KeyEvent(char key)
        {
            IsUpDownEvent = false;
            Character = key;
        }

        public KeyEvent(bool pressed, Keys key, Modifiers mod)
        {
            this.Mod = mod;
            this.pressed = pressed;
            this.key = key;
        }

        override public string ToString()
        {
            if(IsUpDownEvent)
                return "KeyEvent: key={" + key + "} pressed=" + pressed;
            return "KeyEvent: key={" + Character + "} KeyPress";
        }
    }
}
