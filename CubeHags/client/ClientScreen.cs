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

            switch (state)
            {
                case CubeHags.common.ConnectState.CONNECTING:
                case CubeHags.common.ConnectState.CHALLENGING:
                case CubeHags.common.ConnectState.CONNECTED:
                    // connecting clients will only show the connection dialog
                    // refresh to update the time
                    WindowManager.Instance.connectGUI.DrawConnect();
                    WindowManager.Instance.connectGUI.Visible = true;
                    WindowManager.Instance.MoveToFront(WindowManager.Instance.connectGUI);
                    break;
                case CubeHags.common.ConnectState.LOADING:
                case CubeHags.common.ConnectState.PRIMED:
                    // draw the game information screen and loading progress
                    CGame.Instance.DrawActiveFrame(cl.serverTime);

                    // also draw the connection information, so it doesn't
                    // flash away too briefly on local or lan games
                    // refresh to update the time
                    WindowManager.Instance.connectGUI.DrawConnect();
                    WindowManager.Instance.MoveToFront(WindowManager.Instance.connectGUI);
                    break;
                case CubeHags.common.ConnectState.ACTIVE:
                    //WindowManager.Instance.connectGUI.DrawConnect();
                    //WindowManager.Instance.MoveToFront(WindowManager.Instance.connectGUI);
                    WindowManager.Instance.connectGUI.Visible = false;
                    CGame.Instance.DrawActiveFrame(cl.serverTime);
                    break;
                case ConnectState.CINEMATIC:

                    break;
            }
        }

        public void EndFrame()
        {
            Renderer.Instance.Render();
        }
    }
}
