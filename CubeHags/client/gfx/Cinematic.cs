using System;
using System.Collections.Generic;
using System.Text;
using DirectShowLib;
using System.Windows.Forms;
using CubeHags.client.common;
using System.Runtime.InteropServices;

namespace CubeHags.client.gfx
{
    public class Cinematic
    {
        IGraphBuilder graphBuilder;
        IMediaEventEx mediaEventEx;
        IVideoWindow videoWindow;
        IBasicVideo basicVideo;
        IMediaControl mediaControl;
        bool playing = false;
        public bool AlterGameState = false; // If true, will trigget nextmap command when cinematic is finished

        void PlayCinematic_f(string[] tokens)
        {
            if (Client.Instance.cls.state == CubeHags.common.connstate_t.CINEMATIC)
            {
                StopCinematic();
            }

            string arg = tokens[1];
            string s = tokens[2];

            PlayCinematic(arg, 0, 0, Renderer.Instance.RenderSize.Width, Renderer.Instance.RenderSize.Height);
        }

        public void StopCinematic()
        {
            if (playing && mediaControl != null)
            {
                mediaControl.Stop();
                int hr = this.videoWindow.put_Visible(OABool.False);
                DsError.ThrowExceptionForHR(hr);
                hr = this.videoWindow.put_Owner(IntPtr.Zero);
                DsError.ThrowExceptionForHR(hr);
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
            if (playing && AlterGameState)
            {
                Client.Instance.cls.state = CubeHags.common.connstate_t.DISCONNECTED;
                string s = CVars.Instance.VariableString("nextmap");
                if (s != null && s.Length > 0)
                {
                    Commands.Instance.ExecuteText(Commands.EXECTYPE.EXEC_APPEND, s+"\n");
                    CVars.Instance.Set("nextmap", "");
                }
                AlterGameState = false;
            }
            playing = false;
        }

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

        public void PlayCinematic(string file, int x, int y, int w, int h)
        {
            this.graphBuilder = (IGraphBuilder)new FilterGraph();
            // Have the graph builder construct its the appropriate graph automatically
            if (FileCache.Instance.Contains(file))
                file = FileCache.Instance.GetFile(file).FullName;
            else
                return;

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

            // Read the default video size
            int lWidth, lHeight;
            hr = this.basicVideo.GetVideoSize(out lWidth, out lHeight);
            hr = this.videoWindow.SetWindowPosition(x, y, w, h);
            DsError.ThrowExceptionForHR(hr);

            // Run the graph to play the media file
            hr = this.mediaControl.Run();
            DsError.ThrowExceptionForHR(hr);
            playing = true;
            if (AlterGameState)
                Client.Instance.cls.state = CubeHags.common.connstate_t.CINEMATIC;
        }
    }
}
