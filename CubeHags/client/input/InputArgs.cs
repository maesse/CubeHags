using System;
using System.Collections.Generic;
 
using System.Text;

namespace CubeHags.client
{
    public struct InputArgs
    {
        public List<KeyEvent> args;

        public InputArgs(List<KeyEvent> args)
        {
            this.args = args;
        }
    }
}
