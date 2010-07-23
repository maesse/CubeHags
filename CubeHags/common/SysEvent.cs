using System;
using System.Collections.Generic;
 
using System.Text;

namespace CubeHags.common
{
    public enum EventType {
    	// SE_NONE must be zero
    	NONE = 0,	// evTime is still valid
    	KEY,		// evValue is a key code, evValue2 is the down flag
    	CHAR,	// evValue is an ascii char
    	MOUSE,	// evValue and evValue2 are reletive signed x / y moves
    	JOYSTICK_AXIS,	// evValue is an axis number and evValue2 is the current state (-127 to 127)
    	CONSOLE,	// evPtr is a char*
    	PACKET	// evPtr is a netadr_t followed by data bytes to evPtrLength
    }

    public struct Event
    {
        public float evTime;
        public EventType evType;
        public int evValue, evValue2;
        public int dataSize;
        public object data;			// this must be manually freed if not NULL
    }
}
