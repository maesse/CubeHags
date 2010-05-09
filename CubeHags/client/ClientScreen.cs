using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;
using CubeHags.client.gui;
using CubeHags.client.cgame;
using SlimDX;

namespace CubeHags.client
{
    public sealed partial class Client
    {

        /*
        ==================
        SCR_UpdateScreen

        This is called every frame, and can also be called explicitly to flush
        text to the screen.
        ==================
        */
        public void UpdateScreen()
        {
            Renderer.Instance.BeginFrame();

            switch (cls.state)
            {
                case CubeHags.common.connstate_t.CONNECTING:
                case CubeHags.common.connstate_t.CHALLENGING:
                case CubeHags.common.connstate_t.CONNECTED:
                    // connecting clients will only show the connection dialog
                    // refresh to update the time
                    WindowManager.Instance.connectGUI.DrawConnect();
                    break;
                case CubeHags.common.connstate_t.LOADING:
                case CubeHags.common.connstate_t.PRIMED:
                    // draw the game information screen and loading progress
                    CGame.Instance.DrawActiveFrame(cl.serverTime);

                    // also draw the connection information, so it doesn't
                    // flash away too briefly on local or lan games
                    // refresh to update the time
                    WindowManager.Instance.connectGUI.DrawConnect();
                    break;
                case CubeHags.common.connstate_t.ACTIVE:
                    WindowManager.Instance.connectGUI.DrawConnect();

                    ////{X:20,79979 Y:-37,60673 Z:-63,16185}
                    //Vector3 start = new Vector3(20.79979f, -37.60673f, -63.16185f);
                    ////{X:22,03559 Y:-37,33163 Z:-63,875}
                    //Vector3 end = new Vector3(22.03559f, -37.33163f, -63.875f);
                    //Vector3 playerMins = new Vector3( -15, -15, -24 );
                    //Vector3 playerMaxs = new Vector3(15, 15, 32);
                    //pmove_t move = new pmove_t();
                    //trace_t trace = move.DoTrace(end, playerMins, playerMaxs, start, 0, 1);

                    //trace_t trace2 = move.DoTrace(start, playerMins, playerMaxs, end, 0, 1);
                    CGame.Instance.DrawActiveFrame(cl.serverTime);
                    break;
                case connstate_t.CINEMATIC:

                    break;
            }


        }

        

        public void EndFrame()
        {
            Renderer.Instance.Render();
        }
    }
}
