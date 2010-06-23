using System;
using System.Collections.Generic;
using System.Text;
using DirectShowLib;
using System.Windows.Forms;
using CubeHags.client.common;
using System.Runtime.InteropServices;
using CubeHags.common;

namespace CubeHags.client.gfx
{
    public class Cinematic
    {
        IGraphBuilder graphBuilder;
        IMediaEventEx mediaEventEx;
        IVideoWindow videoWindow;
        IBasicVideo basicVideo;
        IMediaControl mediaControl;

        public bool playing = false;
        public bool AlterGameState = false; // If true, will trigget nextmap command when cinematic is finished

        // cinematic command implementation
        public void PlayCinematic_f(string[] tokens)
        {
            if (Client.Instance.cls.state == CubeHags.common.connstate_t.CINEMATIC)
            {
                StopCinematic();
            }

            string arg = tokens[1];
            //string s = tokens[2];

            PlayCinematic(arg, 0, 0, Renderer.Instance.RenderSize.Width, Renderer.Instance.RenderSize.Height);
        }

        // Stops a running cinematic
        public void StopCinematic()
        {
            if (!playing)
                return;

            Common.Instance.WriteLine("Stopping cinematic...");

            // Stop DirectShow
            if (mediaControl != null)
            {
                // Stop playback
                mediaControl.Stop();
                // Remove overlay
                videoWindow.put_FullScreenMode(OABool.False);
                int hr = this.videoWindow.put_Visible(OABool.False);
                DsError.ThrowExceptionForHR(hr);
                hr = this.videoWindow.put_Owner(IntPtr.Zero);
                DsError.ThrowExceptionForHR(hr);
                // Clean up
                if (this.mediaEventEx != null)
                    this.mediaEventEx = null;
                if (this.mediaControl != null)
                    this.mediaControl = null;
                if (this.basicVideo != null)
                    this.basicVideo = null;
                if (this.videoWindow != null)
                    this.videoWindow = null;
                Marshal.ReleaseComObject(this.graphBuilder); this.graphBuilder = null;
            }

            // If cinematic alters gamestate, go ahead and do that..
            if (AlterGameState)
            {
                Client.Instance.cls.state = CubeHags.common.connstate_t.DISCONNECTED;
                string s = CVars.Instance.VariableString("nextmap"); // next gamestate contained in nextmap
                if (s.Length > 0)
                {
                    Commands.Instance.ExecuteText(Commands.EXECTYPE.EXEC_APPEND, s+"\n");
                    CVars.Instance.Set("nextmap", "");
                }
                AlterGameState = false;
            }

            playing = false;
        }

        // Cinematic eventloop, checks if the video has ended
        public void RunCinematic()
        {
            if (!playing)
                return;
            int hr = 0;
            EventCode evCode;
            IntPtr evParam1, evParam2;

            // Make sure that we don't access the media event interface
            // after it has already been released.
            if (this.mediaEventEx == null)
                return;

            // Process all queued events
            while (this.mediaEventEx.GetEvent(out evCode, out evParam1, out evParam2, 0) == 0)
            {
                // Free memory associated with callback, since we're not using it
                hr = this.mediaEventEx.FreeEventParams(evCode, evParam1, evParam2);

                // If this is the end of the clip, reset to beginning
                if (evCode == EventCode.Complete)
                {
                    StopCinematic();
                    break;
                }
            }
        }

        // Starts playing a new cinematic
        public void PlayCinematic(string file, int x, int y, int w, int h)
        {
            // Lame bugfix: DirectShow and Fullscreen doesnt like eachother
            if (CVars.Instance.Get("r_fs", "0", CVarFlags.ARCHIVE).Integer == 1)
            {
                if (AlterGameState)
                {
                    playing = true;
                    StopCinematic();
                }
                return;
            }

            // Check if file exists
            if (FileCache.Instance.Contains(file))
                file = FileCache.Instance.GetFile(file).FullName;
            else
            {
                if (AlterGameState)
                {
                    playing = true;
                    StopCinematic();
                }
                Common.Instance.WriteLine("PlayCinematic: Could not find video: {0}", file);
                return;
            }

            // Have the graph builder construct its the appropriate graph automatically
            this.graphBuilder = (IGraphBuilder)new FilterGraph();
            int hr = graphBuilder.RenderFile(file, null);
            DsError.ThrowExceptionForHR(hr);

            mediaControl = (IMediaControl)this.graphBuilder;
            mediaEventEx = (IMediaEventEx)this.graphBuilder;
            videoWindow = this.graphBuilder as IVideoWindow;
            basicVideo = this.graphBuilder as IBasicVideo;

            // Setup the video window
            hr = this.videoWindow.put_Owner(Renderer.Instance.form.Handle);
            DsError.ThrowExceptionForHR(hr);
            hr = this.videoWindow.put_WindowStyle(WindowStyle.Child | WindowStyle.ClipSiblings | WindowStyle.ClipChildren);
            DsError.ThrowExceptionForHR(hr);

            // Set the video size
            //int lWidth, lHeight;
            //hr = this.basicVideo.GetVideoSize(out lWidth, out lHeight);
            hr = this.videoWindow.SetWindowPosition(x, y, w, h);
            videoWindow.put_FullScreenMode((CVars.Instance.Get("r_fs", "0", CVarFlags.ARCHIVE).Integer == 1) ? OABool.True : OABool.False);
            DsError.ThrowExceptionForHR(hr);

            // Run the graph to play the media file
            hr = this.mediaControl.Run();
            DsError.ThrowExceptionForHR(hr);
            playing = true;
            if (AlterGameState)
                Client.Instance.cls.state = CubeHags.common.connstate_t.CINEMATIC;
            Common.Instance.WriteLine("Playing cinematic: {0}", file);

            EventCode code;
        }
    }
}
