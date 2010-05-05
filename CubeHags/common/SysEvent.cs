using System;
using System.Collections.Generic;
 
using System.Text;

namespace CubeHags.common
{
    public enum sysEventType_t {
    	// SE_NONE must be zero
    	SE_NONE = 0,	// evTime is still valid
    	SE_KEY,		// evValue is a key code, evValue2 is the down flag
    	SE_CHAR,	// evValue is an ascii char
    	SE_MOUSE,	// evValue and evValue2 are reletive signed x / y moves
    	SE_JOYSTICK_AXIS,	// evValue is an axis number and evValue2 is the current state (-127 to 127)
    	SE_CONSOLE,	// evPtr is a char*
    	SE_PACKET	// evPtr is a netadr_t followed by data bytes to evPtrLength
    }

    public struct sysEvent_t
    {
        public int evTime;
        public sysEventType_t evType;
        public int evValue, evValue2;
        public int evPtrLength;	// bytes of data pointed to by evPtr, for journaling
        //public void* evPtr;			// this must be manually freed if not NULL
    };
}
