using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;
using CubeHags.client.gui;
using CubeHags.client.cgame;

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
                    CGame.Instance.DrawActiveFrame(cl.serverTime);
                    break;
            }


        }

        

        public void EndFrame()
        {
            Renderer.Instance.Render();
        }
    }
}
