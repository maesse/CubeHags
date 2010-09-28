
using System.Collections.Generic;
using System;
using System.Text;
using SlimDX.Direct3D9;
using SlimDX;
using System.Drawing;
using CubeHags.client.gui;
using CubeHags.client.map.Source;
using CubeHags.client;
using System.Collections;
using SlimDX.Windows;
using CubeHags.client.render.Formats;
using CubeHags.client.render;
using CubeHags.client.common;
using CubeHags.client.input;
using System.Windows.Forms;
using System.Threading;
using CubeHags.client.cgame;


namespace CubeHags.client
{
    public delegate void RenderDelegate(Effect effect, Device device, bool setMaterial);
    public sealed class Renderer : IResettable
    {
        // Singleton stuff
        private static readonly Renderer _instance = new Renderer();

        // Device
        public Device device = null;
        private bool _is3D9Ex = false;
        public bool Is3D9Ex { get { return _is3D9Ex; } }
        private PresentParameters _pp;
        public bool _sizeChanged;
        public bool deviceLost = false;

        // Shader
        public Effect effect;
        private string technique = "TexturedLightmap"; // What shader technique to use
        public float MIDDLE_GRAY = 0.68f;
        public float LUM_WHITE = 0.47f;
        public float BRIGHT_THRESHOLD = 0.9f;
        public float bloomMulti = 0.4f;
        public Matrix worldMatrix, viewMatrix, projMatrix;

        // Gui for showing messageboxes...
        public RenderForm form;
        bool formIsResizing = false;

        // Text
        public Sprite sprite;

        // Buffers that contain stuff to be rendered
        public List<KeyValuePair<ulong, RenderDelegate>> drawCalls = new List<KeyValuePair<ulong, RenderDelegate>>();
        private List<KeyValuePair<ulong, RenderDelegate>> currentdrawCalls = new List<KeyValuePair<ulong, RenderDelegate>>();
        public Dictionary<ushort, HagsIndexBuffer> IndexBuffers = new Dictionary<ushort, HagsIndexBuffer>();
        public Dictionary<ushort, HagsVertexBuffer> VertexBuffers = new Dictionary<ushort, HagsVertexBuffer>();

        // Display options 
        public Size RenderSize = new Size(1280, 800);
        public MultisampleType MultiSampling = MultisampleType.FourSamples;
        private FillMode _fillMode = FillMode.Solid;
        public FillMode FillMode { get { return _fillMode; } set { _fillMode = value; device.SetRenderState<FillMode>(RenderState.FillMode, value); } }
        public Camera Camera;

        // Source map
        public SourceMap SourceMap = null;

        // Render To Texture stuff.
        public Surface RenderSurface;
        public Texture RenderTexture;
        public Surface RenderTextureSfc;
        public Surface RenderDepthSurface;
        public long LastFrameTime = 0;

        // Render controls
        Bloom bloom;
        ToneMap tonemap;
        public Texture AvgLum { get { return tonemap.AverageLum; } }
        public float DetailMultiplier = 2f;

        private int _nextRenderItemID = 1;
        public int NextRenderItemID { get { return _nextRenderItemID++; } }

        public Dictionary<string, SlimDX.Direct3D9.Font> Fonts = new Dictionary<string, SlimDX.Direct3D9.Font>();
        

        // Quake implementation
        public int visCount;    // incremented every time a new vis cluster is entered
        public int frameCount;  // incremented every frame
        public int sceneCount;  // incremented every scene
        public int viewCount;   // incremented every view (twice a scene if portaled)
                                // and every R_MarkFragments call
        public int frameSceneNum;// zeroed at RE_BeginFrame

        public bool worldMapLoaded = false;
        public ViewParams viewParams;
        public CubeHags.client.render.Orientation or;  // for current entity

        public int viewCluster;

        // Rendering
        SortItem.VPLayer currentVPLayer = SortItem.VPLayer.EFFECT;
        SortItem.Viewport currentViewport = SortItem.Viewport.EIGHT;
        SortItem.Translucency currentTrans = SortItem.Translucency.OPAQUE;
        ushort currentIB = 65535;
        ushort currentVB = 65535;
        uint lastMaterial = 0;
        bool setMaterial = true;
        bool[] setmaterialbatch = new bool[1024];

        public CVar r_vsync = CVars.Instance.Get("r_vsync", "1", CVarFlags.ARCHIVE);
        public CVar r_fs = CVars.Instance.Get("r_fs", "0", CVarFlags.ARCHIVE);
        public CVar r_resulution = CVars.Instance.Get("r_res", "1280x800", CVarFlags.ARCHIVE);
        public CVar r_showfps = CVars.Instance.Get("r_showfps", "1", CVarFlags.ARCHIVE);
        public CVar tone_saturate = CVars.Instance.Get("tone_saturate", "1", CVarFlags.TEMP);
        public CVar tone_luminance = CVars.Instance.Get("tone_luminance", "0.01", CVarFlags.TEMP);
        public CVar tone_white = CVars.Instance.Get("tone_white", "1.5", CVarFlags.TEMP);
        public CVar tone_grey = CVars.Instance.Get("tone_white", "0.72", CVarFlags.TEMP);
        public CVar tone_a = CVars.Instance.Get("tone_a", "0.18", CVarFlags.TEMP);
        public CVar tone_b = CVars.Instance.Get("tone_b", "0.37", CVarFlags.TEMP);
        public CVar tone_c = CVars.Instance.Get("tone_c", "0.08", CVarFlags.TEMP);
        public CVar tone_d = CVars.Instance.Get("tone_d", "0.3", CVarFlags.TEMP);
        public CVar tone_e = CVars.Instance.Get("tone_e", "0.02", CVarFlags.TEMP);
        public CVar tone_f = CVars.Instance.Get("tone_f", "0.3", CVarFlags.TEMP);
        public CVar tone_w = CVars.Instance.Get("tone_w", "11.2", CVarFlags.TEMP);

        public List<string> ValidResolutions = new List<string>();

        // Constructor
        Renderer()
        {
            
        }

        public void BeginFrame()
        {
            frameCount++;
            frameSceneNum = 0;
            device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.FromArgb(0, Color.Beige), 1.0f, 0);
        }

        /*
        ================
        R_RenderView

        A view may be either the actual camera view,
        or a mirror / remote location
        ================
        */
        void RenderView(ViewParams view) 
        {
            if (view.viewportWidth <= 0 || view.viewportHeight <= 0)
            {
                return;
            }
            view.SetupProjection(10000f, true);

            if (SourceMap != null)
                SourceMap.Render(device);

            // SetFarClip()

            view.SetupProjectionZ();
            Camera.RotateForViewer();
        }

        public void Render(ViewParams view)
        {
            view.Origin = new render.Orientation();
            view.Origin.axis[0] = view.viewaxis[0];
            view.Origin.axis[1] = view.viewaxis[1];
            view.Origin.axis[2] = view.viewaxis[2];
            view.Origin.origin = view.vieworg;
            view.PVSOrigin = view.vieworg;

            viewParams = view;
            RenderView(view);
        }

        // Make sure we have a device to send commands to!
        public bool PreFrame()
        {
            // Dont render while resizing
            if (formIsResizing)
                return false;

            // Only draw ~30fps when not in focus
            if (!form.Focused && !r_fs.Bool)
            {
                Thread.Sleep(30);
                return true;
            }

            try
            {
                Result coopResult = device.TestCooperativeLevel();
                // Check if we lost control over the device
                if (coopResult == ResultCode.DeviceLost)
                {
                    if (!deviceLost)
                        OnLostDevice();
                    deviceLost = true;
                    Thread.Sleep(10);
                    return false;
                }
                // device is ready to be reset
                else if (coopResult == ResultCode.DeviceNotReset)
                {
                    // Has lost device already been handled?
                    if (deviceLost)
                    {
                        device.Reset(_pp);
                        OnResetDevice();
                        deviceLost = false;
                    }
                    else
                    {
                        // handle lost device before resetting
                        OnLostDevice();

                        device.Reset(_pp);
                        OnResetDevice();
                    }
                }

                if (_sizeChanged)
                {
                    coopResult = device.TestCooperativeLevel();
                    if (coopResult == ResultCode.DeviceNotReset || coopResult == ResultCode.DeviceLost)
                        return false;   // Don't do anything fancy if the device is acting up

                    // Decided on a resolution
                    if (!CVars.Instance.FindVar("r_fs").Bool)
                    {
                        // Windowed
                        form.ClientSize = GetResolution();
                        RenderSize = form.ClientSize;
                        _pp.BackBufferWidth = RenderSize.Width;
                        _pp.BackBufferHeight = RenderSize.Height;
                        _pp.FullScreenRefreshRateInHertz = 0;
                    }
                    else
                    {
                        // FS
                        RenderSize = GetResolution();
                        _pp.BackBufferHeight = RenderSize.Height;
                        _pp.BackBufferWidth = RenderSize.Width;
                    }

                    _pp.SwapEffect = (_is3D9Ex ? (!r_fs.Bool ? SwapEffect.Discard : SwapEffect.Discard) : SwapEffect.Discard);
                    _pp.PresentationInterval = (r_vsync.Bool ? PresentInterval.One : PresentInterval.Immediate);
                    _pp.Windowed = !r_fs.Bool;
                    _sizeChanged = false;

                    // Clear the device
                    OnLostDevice();

                    // Reset it..
                    device.Reset(_pp);
                    OnResetDevice();
                    form.FormBorderStyle = (!r_fs.Bool ? System.Windows.Forms.FormBorderStyle.Sizable : System.Windows.Forms.FormBorderStyle.None);
                    form.TopMost = r_fs.Bool;
                    form.MaximizeBox = !r_fs.Bool;
                    form.Focus();
                    // Reset GUI windows
                    WindowManager.Instance.SetAllWindowPositions();
                    return false;
                }
            }
            catch
            {
                // If the device is complaining, don't try to render to it
                return false;
            }

            return true;
        }

        // Renders the scene. This is the core render method.
        public void Render()
        {
            if (!PreFrame())
                return; // Not ready to render

            SetupFrame();
            device.BeginScene();

            // Swich drawcall list
            FlushDrawCalls();
            int nDrawCalls = currentdrawCalls.Count;

            // Sort calls
            currentdrawCalls.Sort((firstPair, secondPair) =>
            {
                return secondPair.Key.CompareTo(firstPair.Key);
            });
            
            // Render drawcalls
            RenderDrawCallsNew();

            // Render UI
            WindowManager.Instance.Render();
            HagsConsole.Instance.Render();
            FlushDrawCalls();
            nDrawCalls += currentdrawCalls.Count;
            // Do unsorted UI render (Added to drawcalls list back to front)
            RenderDrawCallsNew();

            effect.Technique = technique;

            // Draw FPS
            MiscRender.DrawRenderStats(this);

            device.EndScene();
            device.Present();
            HighResolutionTimer.Instance.Set();
        }

        private void FlushDrawCalls()
        {
            // Switch over to new calllist
            List<KeyValuePair<ulong, RenderDelegate>> tempDrawCalls = currentdrawCalls;
            tempDrawCalls.Clear();
            currentdrawCalls = drawCalls;
            drawCalls = tempDrawCalls;
        }

        private void RenderDrawCallsNew()
        {
            // Reset stuff
            currentVPLayer = SortItem.VPLayer.EFFECT;
            currentViewport = SortItem.Viewport.EIGHT;
            currentTrans = SortItem.Translucency.OPAQUE;
            currentIB = 65535;
            currentVB = 65535;
            lastMaterial = 0;
            setMaterial = true;
            //Vector3 originalPosition = Camera.position;

            //nSameMaterial = 0;
            int drawOffset = 0;
            int nDrawGroup = 0;
            // Issue drawcalls from last frame
            ulong oldKey = 0;
            uint oldMaterial = 0;
            int endcase = currentdrawCalls.Count;
            ulong key = 0;
            for (int i = 0; i < currentdrawCalls.Count+1; i++)
            {
                if (i == endcase)
                    key = 0;
                else
                    key = currentdrawCalls[i].Key;

                // Check for statechange - ignore material/depth change
                if ((uint)(key>>32) != (uint)(oldKey>>32) || nDrawGroup == 1023)
                {
                    int numpasses = effect.Begin(FX.None);
                    for (int j = 0; j < numpasses; j++)
                    {
                        effect.BeginPass(j);
                        {
                            for (int h = 0; h < nDrawGroup; h++)
                            {
                                currentdrawCalls[drawOffset+h].Value.Invoke(effect, device, setmaterialbatch[h]);
                            }
                            
                        }
                        effect.EndPass();
                    }
                    effect.End();

                    // Draw grouped calls
                    HandleDrawCallChange(key);


                    // Reset key
                    oldKey = key;
                    setmaterialbatch[0] = true;
                    nDrawGroup = 1;
                    drawOffset = i;
                }
                else
                {
                    // Save result for material change
                    uint materialID = SortItem.GetMaterial(key);
                    setmaterialbatch[nDrawGroup] = materialID != oldMaterial;
                    oldMaterial = materialID;

                    // Store up calls
                    nDrawGroup++;
                }
                oldKey = key;
                
            }

            if (currentViewport == SortItem.Viewport.INSTANCED)
            {
                // Disable frequency instancing
                device.ResetStreamSourceFrequency(0);
                device.SetStreamSource(1, null, 0, 0);
                effect.Technique = technique;
            }
        }

        void HandleDrawCallChange(ulong key)
        {
            SortItem.VPLayer vpLayer = SortItem.GetVPLayer(key);
            SortItem.Viewport vp = SortItem.GetViewport(key);
            SortItem.Translucency trans = SortItem.GetTranslucency(key);
            
            ushort VB = SortItem.GetVBID(key);
            ushort IB = SortItem.GetIBID(key);

            if (vpLayer != currentVPLayer)
            {
                if (currentVPLayer == SortItem.VPLayer.SKYBOX3D)
                {
                    //Camera.position = originalPosition;
                    //Camera.PositionCamera();
                    Camera.RotateForViewer();
                    // Resetting will just be a call to rotateforviewer now
                    
                    Matrix worldview = device.GetTransform(TransformState.View) * Camera.World;
                    effect.SetValue("WorldViewProj", worldview * Camera.Projection);

                    //device.Clear(ClearFlags.ZBuffer, Color.FromArgb(0, Color.CornflowerBlue), 1.0f, 0);
                }
                else if (currentVPLayer == SortItem.VPLayer.SKYBOX)
                {
                    device.SetRenderState(RenderState.ZWriteEnable, true);
                    device.SetRenderState(RenderState.ZEnable, true);
                }
                else if (currentVPLayer == SortItem.VPLayer.HUD)
                {
                    device.SetRenderState(RenderState.ZEnable, true);
                }
                else if (currentVPLayer == SortItem.VPLayer.EFFECT)
                {
                    device.SetRenderState(RenderState.ZWriteEnable, true);
                }
                currentVPLayer = vpLayer;
                // Layer change.
                switch (vpLayer)
                {
                    case SortItem.VPLayer.SKYBOX3D:
                        if (effect.Technique != technique)
                            effect.Technique = "Sky3d";
                        if (SourceMap != null)
                        {
                            SourceMap.SetupVertexBuffer(device);
                            Vector3 oldPos = Camera.position;
                            Vector3 pos = SourceMap.skybox3d.GetSkyboxPosition(Camera.position);
                            Renderer.Instance.viewParams.Origin.origin = pos;
                            //Camera.position = pos;
                            Camera.RotateForViewer();
                            // Resetting will just be a call to rotateforviewer now
                            Renderer.Instance.viewParams.Origin.origin = oldPos;
                            Matrix worldview = device.GetTransform(TransformState.View) * Camera.World;
                            effect.SetValue("WorldViewProj", worldview * Camera.Projection);
                            //device.SetRenderState(RenderState.z
                        }
                        break;
                    case SortItem.VPLayer.SKYBOX:
                        effect.Technique = "Sky";
                        device.SetRenderState(RenderState.ZWriteEnable, false);
                        if (SourceMap != null)
                            SourceMap.skybox.SetupRender(device);
                        break;
                    case SortItem.VPLayer.HUD:
                        device.SetRenderState(RenderState.ZEnable, false);
                        if (trans != SortItem.Translucency.OPAQUE)
                            effect.Technique = "GUIAlpha";
                        else
                            effect.Technique = "FinalPass_RGBE8";
                        break;
                    case SortItem.VPLayer.WORLD:
                        if (effect.Technique != technique)
                            effect.Technique = technique;
                        if (SourceMap != null)
                            SourceMap.SetupVertexBuffer(device);
                        break;
                    case SortItem.VPLayer.EFFECT:
                        effect.Technique = "PositionColorAlpha";
                        device.SetRenderState(RenderState.ZWriteEnable, false);
                        break;
                }
            }

            // Handle viewport change
            if (vp != currentViewport)
            {
                if (currentViewport == SortItem.Viewport.INSTANCED)
                {
                    // Disable frequency instancing
                    device.ResetStreamSourceFrequency(0);
                    device.SetStreamSource(1, null, 0, 0);
                    //device.SetTexture(1, null);
                    effect.Technique = technique;
                }
                else if (currentViewport == SortItem.Viewport.FOUR)
                    effect.Technique = technique;
                currentViewport = vp;
                switch (vp)
                {
                    case SortItem.Viewport.INSTANCED:
                        Matrix worldview = device.GetTransform(TransformState.View) * Camera.World;
                        effect.SetValue("WorldViewProj", worldview * Camera.Projection);
                        effect.Technique = "TexturedInstaced";
                        //device.SetTexture(1, SourceMap.ambientLightTexture);
                        break;
                    case SortItem.Viewport.FOUR:
                        effect.Technique = "PositionTexturedColor";
                        break;
                }
            }

            // Handle transparency change
            if (trans != currentTrans)
            {
                if (currentTrans == SortItem.Translucency.NORMAL || currentTrans == SortItem.Translucency.ADDITIVE || currentTrans == SortItem.Translucency.SUBSTRACTIVE)
                {
                    device.SetRenderState(RenderState.AlphaBlendEnable, false);
                }
                currentTrans = trans;
                switch (trans)
                {
                    case SortItem.Translucency.OPAQUE:
                        if (vpLayer == SortItem.VPLayer.SKYBOX3D)
                            effect.Technique = "Sky3d";
                        else if (vpLayer != SortItem.VPLayer.HUD && currentViewport != SortItem.Viewport.INSTANCED && currentViewport != SortItem.Viewport.FOUR)
                            effect.Technique = technique;
                        else if (currentViewport != SortItem.Viewport.INSTANCED && currentViewport != SortItem.Viewport.FOUR)
                            effect.Technique = "FinalPass_RGBE8";
                        break;
                    case SortItem.Translucency.ADDITIVE:
                    case SortItem.Translucency.SUBSTRACTIVE:
                    case SortItem.Translucency.NORMAL:
                        if (vpLayer == SortItem.VPLayer.SKYBOX3D)
                        {
                            effect.Technique = "Sky3d";
                        }
                        else if (vpLayer != SortItem.VPLayer.HUD && currentViewport != SortItem.Viewport.INSTANCED && vpLayer != SortItem.VPLayer.EFFECT && currentViewport != SortItem.Viewport.FOUR)
                            effect.Technique = "TexturedLightmapAlpha";
                        else if (currentViewport != SortItem.Viewport.INSTANCED && vpLayer == SortItem.VPLayer.HUD)
                                effect.Technique = "GUIAlpha";
                        else if (vpLayer == SortItem.VPLayer.EFFECT)
                        {
                            effect.Technique = "PositionColorAlpha";
                        }
                        break;
                }
            }

            // Vertex buffer change
            if (VB != currentVB && VB != 0)
            {

                device.SetStreamSource(0, VertexBuffers[VB].VB, 0, D3DX.GetFVFVertexSize(VertexBuffers[VB].VF));
                device.VertexDeclaration = VertexBuffers[VB].VD;
                currentVB = VB;
            }

            // Index buffer change
            if (IB != currentIB && IB != 0)
            {
                device.Indices = IndexBuffers[IB].IB;
                currentIB = IB;
            }
        }

        // Inits shaders etc. when preparing to render a frame
        private void SetupFrame()
        {
            long ElapsedTime = HighResolutionTimer.Ticks - LastFrameTime;
            LastFrameTime = HighResolutionTimer.Ticks;
            // Update shader constants

            // Camera
            //Camera.PositionCamera();
            Matrix worldview = device.GetTransform(TransformState.View) * Camera.World;
            effect.SetValue("WorldViewProj", worldview * Camera.Projection);
            effect.SetValue("World", Matrix.Identity);
            effect.SetValue<Vector4>("Eye", new Vector4(Camera.position,0f));
            effect.SetValue("fTimeElap", (float)(((double)ElapsedTime) / (double)HighResolutionTimer.Frequency));
            effect.SetValue("MIDDLE_GRAY", MIDDLE_GRAY);
            effect.SetValue("LUM_WHITE", LUM_WHITE);
            effect.SetValue("BRIGHT_THRESHOLD", BRIGHT_THRESHOLD);
            effect.SetValue("bloomMulti", bloomMulti);
            device.SetPixelShaderConstant(7, new bool[] {false});
            effect.SetValue("g_OverbrightFactor", 1f);

            // Log Luminance
            float invLogLumRange = 1.0f / (tonemap.MaxLogLum + tonemap.MinLogLum);
            float logLumOffset = tonemap.MinLogLum * invLogLumRange;
            effect.SetValue("invLogLumRange", invLogLumRange);
            effect.SetValue("logLumOffset", logLumOffset);
            if (tonemap.ForceAvgLum)
                effect.SetValue("avgLogLum", tonemap.ForcedAvgLum);

            // Texture samplers
            device.SetSamplerState(0, SamplerState.MipFilter, (int)TextureFilter.Linear);
            device.SetSamplerState(0, SamplerState.MagFilter, (int)TextureFilter.Linear);
            device.SetSamplerState(0, SamplerState.MinFilter, (int)TextureFilter.Anisotropic);
            device.SetSamplerState(0, SamplerState.MaxAnisotropy, 4);
            
            device.SetRenderState(RenderState.SrgbWriteEnable, false);
            device.SetSamplerState(0, SamplerState.SrgbTexture, 0);

            device.SetSamplerState(1, SamplerState.MipFilter, (int)TextureFilter.Linear);
            device.SetSamplerState(1, SamplerState.MinFilter, (int)TextureFilter.Anisotropic);
            device.SetSamplerState(1, SamplerState.MagFilter, (int)TextureFilter.Linear);
            device.SetSamplerState(1, SamplerState.MaxAnisotropy, 4);

            effect.SetValue("detailMultiplier", DetailMultiplier);
            device.SetRenderState(RenderState.ZEnable, true);
            device.SetRenderState(RenderState.ScissorTestEnable, true);

            // Tone mapping
            effect.SetValue("saturationAmount", tone_saturate.Value);
            
            
            //double logLum = Math.Pow(10.0,tone_luminance.Value);
            effect.SetValue("ExposureBias", tone_luminance.Value);
            //effect.SetValue("avgLogLum", (float)logLum);
            effect.SetValue("MIDDLE_GRAY", tone_grey.Value);
            effect.SetValue("LUM_WHITE", tone_white.Value);
            effect.SetValue("A", tone_a.Value);
            effect.SetValue("B", tone_b.Value);
            effect.SetValue("C", tone_c.Value);
            effect.SetValue("D", tone_d.Value);
            effect.SetValue("E", tone_e.Value);
            effect.SetValue("F", tone_f.Value);
            effect.SetValue("W", tone_w.Value);

            if (SourceMap != null && SourceMap.fogController != null)
            {
                effect.SetValue<Color3>("FogColor", SourceMap.fogController.GetFogColor());

            }
        }

        private void FinalPass()
        {
            device.SetTexture(0, RenderTexture);
            effect.Technique = "FinalPass_RGBE8";

            DrawFullScreenQuad();

            device.SetTexture(0, null);
        }

        public void Init(RenderForm form)
        {
            this.form = form;
            
            form.ResizeBegin += new System.EventHandler((o, e) => { formIsResizing = true; });
            form.ResizeEnd += new EventHandler((o, e) => { formIsResizing = false; if(form.ClientSize != RenderSize) _sizeChanged = true; });
            form.Show();
            InitDevice();
            
            // Init input
            Commands.Instance.AddCommand("r_mode", ToggleFillMode);
            KeyHags.Instance.SetBind(Keys.F12, "r_mode");
        }

        private void InitDevice()
        {
            // Use D3DEx for Vista/Win7+
            _is3D9Ex = false;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major >= 6)
            {
                _is3D9Ex = true;
            }
            
            DeviceType devType = DeviceType.Hardware;
            int adapterOrdinal = 0;

            Direct3D d3d = null;
            Direct3DEx d3dex = null;

            
            if (_is3D9Ex)
            {
                try
                {
                    // Create Ex, fallback if it fails
                    d3dex = new Direct3DEx();
                }
                catch
                {
                    d3d = new Direct3D();
                    _is3D9Ex = false;
                }
            }
            else
                d3d = new Direct3D();
            
            // Look for PerfHUD
            AdapterCollection adapters = (_is3D9Ex ? d3dex : d3d).Adapters;
            foreach (AdapterInformation adap in adapters)
            {
                if (adap.Details.Description.Contains("PerfH"))
                {
                    adapterOrdinal = adap.Adapter;
                    devType = DeviceType.Reference;
                }
            }

            foreach (var item in adapters[adapterOrdinal].GetDisplayModes(Format.X8R8G8B8))
        	{
                string val = item.Width + "x" + item.Height;
                bool found = false;
                for (int i = 0; i < ValidResolutions.Count; i++)
                {
                    if (ValidResolutions[i].Equals(val))
                    {
                        found = true;
                        break;
                    }
                }
                if(!found)
                    ValidResolutions.Add(val);
        	}

            // Get resolution
            Size res = GetResolution();
            RenderSize = res;
            form.ClientSize = res;

            // Set present parameters
            _pp = new PresentParameters();
            _pp.Windowed = r_fs.Integer==0?true:false;
            _pp.SwapEffect = SwapEffect.Discard;
            _pp.EnableAutoDepthStencil = true;
            _pp.AutoDepthStencilFormat = Format.D24S8;
            _pp.PresentationInterval = (r_vsync.Integer==1? PresentInterval.One: PresentInterval.Immediate);
            _pp.Multisample = MultiSampling;
            _pp.BackBufferWidth = RenderSize.Width;
            _pp.BackBufferHeight = RenderSize.Height;
            _pp.BackBufferFormat = Format.X8R8G8B8;
            _pp.DeviceWindowHandle = form.Handle;
            
            // Handle Capabilities
            Capabilities caps =  adapters[adapterOrdinal].GetCaps(devType);
            CreateFlags createFlags = CreateFlags.SoftwareVertexProcessing;

            // Got hardare vertex?
            if ((caps.DeviceCaps & DeviceCaps.HWTransformAndLight) == DeviceCaps.HWTransformAndLight)
            {
                createFlags = CreateFlags.HardwareVertexProcessing;
                // Support pure device?
                if ((caps.DeviceCaps & DeviceCaps.PureDevice) == DeviceCaps.PureDevice)
                    createFlags |= CreateFlags.PureDevice;
            }
            
            createFlags |= CreateFlags.FpuPreserve;

            // Create d3d device + behemoth fallback
            try
            {
                if (_is3D9Ex)
                {
                    if (r_fs.Bool)
                    {
                        DisplayModeEx dsp = new DisplayModeEx();
                        dsp.Width = _pp.BackBufferWidth;
                        dsp.Height = _pp.BackBufferHeight;
                        dsp.Format = _pp.BackBufferFormat;
                        dsp.RefreshRate = 60;
                        _pp.FullScreenRefreshRateInHertz = 60;
                        //DisplayModeEx dispMode = d3dex.GetAdapterDisplayModeEx(adapterOrdinal);
                        //RenderSize = new Size(dispMode.Width, dispMode.Height);
                        //form.ClientSize = new Size(dispMode.Width, dispMode.Height);
                        //_pp.BackBufferFormat = dispMode.Format;
                        //_pp.BackBufferWidth = dispMode.Width;
                        //_pp.BackBufferHeight = dispMode.Height;
                        //_pp.FullScreenRefreshRateInHertz = dispMode.RefreshRate;
                        device = new DeviceEx(d3dex, adapterOrdinal, devType, form.Handle, createFlags, _pp, dsp);
                    }
                    else
                        device = new DeviceEx(d3dex, adapterOrdinal, devType, form.Handle, createFlags, _pp);
                }
                else
                    device = new Device(d3d, adapterOrdinal, devType, form.Handle, createFlags, _pp);

            }
            catch (Direct3D9Exception ex)
            {
                if (ex.ResultCode == ResultCode.NotAvailable)
                {
                    // Try again with different settings
                    RenderSize = new Size(800, 600);
                    form.ClientSize = new Size(800, 600);
                    _pp.BackBufferWidth = 800;
                    _pp.BackBufferHeight = 600;
                    _pp.BackBufferCount = 1;
                    _pp.SwapEffect = SwapEffect.Discard;
                    createFlags &= ~(CreateFlags.PureDevice | CreateFlags.HardwareVertexProcessing);
                    createFlags |= CreateFlags.SoftwareVertexProcessing;
                    try
                    {
                        if (_is3D9Ex)
                        {
                            if (r_fs.Bool)
                            {
                                DisplayModeEx dispMode = d3dex.GetAdapterDisplayModeEx(adapterOrdinal);
                                RenderSize = new Size(dispMode.Width, dispMode.Height);
                                form.ClientSize = new Size(dispMode.Width, dispMode.Height);
                                _pp.BackBufferFormat = dispMode.Format;
                                _pp.BackBufferWidth = dispMode.Width;
                                _pp.BackBufferHeight = dispMode.Height;
                                _pp.FullScreenRefreshRateInHertz = dispMode.RefreshRate;
                                device = new DeviceEx(d3dex, adapterOrdinal, devType, form.Handle, createFlags, _pp, dispMode);
                            }
                            else
                                device = new DeviceEx(d3dex, adapterOrdinal, devType, form.Handle, createFlags, _pp);
                        }
                        else
                            device = new Device(d3d, adapterOrdinal, devType, form.Handle, createFlags, _pp);
                    }
                    catch (Exception ex2)
                    {
                        if (_is3D9Ex)
                        {
                            // 3. fallback.. disable ex
                            _is3D9Ex = false;
                            Size ress = GetResolution();
                            RenderSize = ress;
                            form.ClientSize = ress;
                            _pp.BackBufferWidth = ress.Width;
                            _pp.BackBufferHeight = ress.Height;
                            _pp.BackBufferCount = 1;
                            _pp.SwapEffect = SwapEffect.Discard;
                            d3dex.Dispose();
                            device = new Device(new Direct3D(), adapterOrdinal, devType, form.Handle, createFlags, _pp);
                        }
                        else
                            throw ex2;
                    }
                }
                else
                    throw ex;
            }
            
            // Load main shader
            if (System.IO.File.Exists(System.Windows.Forms.Application.StartupPath+"/client/render/simple.fx"))
            {
                string shaderOutput = null;
                SlimDX.Configuration.ThrowOnError = false;
                effect = Effect.FromFile(device, System.Windows.Forms.Application.StartupPath + "/client/render/simple.fx", null, null, null, ShaderFlags.None, null, out shaderOutput);

                if (shaderOutput != null && shaderOutput != "" && effect == null)
                {
                    // Shader problem :..(
                    System.Windows.Forms.MessageBox.Show(shaderOutput, "Shader Compilation error :(", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    Shutdown();
                    return;
                }
                SlimDX.Configuration.ThrowOnError = true;
            }
            else
            {
                System.Console.WriteLine("Could not find shader..");
            }


            // Add fonts
            if (Fonts.Count == 0)
            {
                System.Drawing.Text.PrivateFontCollection col = new System.Drawing.Text.PrivateFontCollection();
                col.AddFontFile(System.Windows.Forms.Application.StartupPath + @"\data\gui\Candara.ttf");

                // UI Title
                System.Drawing.Font localFont = new System.Drawing.Font(col.Families[0], 10.5f, System.Drawing.FontStyle.Regular);
                SlimDX.Direct3D9.Font font = new SlimDX.Direct3D9.Font(Renderer.Instance.device, localFont);
                Fonts.Add("title", font);

                // FPS, etc.
                font = new SlimDX.Direct3D9.Font(device, new System.Drawing.Font("Lucidia Console", 10f, System.Drawing.FontStyle.Regular));
                Fonts.Add("diag", font);

                // Labels and UI elements
                localFont = new System.Drawing.Font(col.Families[0], 10f, System.Drawing.FontStyle.Regular);
                font = new SlimDX.Direct3D9.Font(Renderer.Instance.device, localFont);
                Fonts.Add("label", font);

                localFont = new System.Drawing.Font(col.Families[0], 15f, System.Drawing.FontStyle.Regular);
                font = new SlimDX.Direct3D9.Font(Renderer.Instance.device, localFont);
                Fonts.Add("biglabel", font);

                localFont = new System.Drawing.Font(col.Families[0], 23f, System.Drawing.FontStyle.Regular);
                font = new SlimDX.Direct3D9.Font(Renderer.Instance.device, localFont);
                Fonts.Add("biggerlabel", font);

                // Textbox
                col = new System.Drawing.Text.PrivateFontCollection();
                col.AddFontFile(System.Windows.Forms.Application.StartupPath + @"\data\gui\dina10px.ttf");
                localFont = new System.Drawing.Font(col.Families[0], 12f, FontStyle.Regular);
                font = new SlimDX.Direct3D9.Font(Renderer.Instance.device, localFont);
                Fonts.Add("textbox", font);
            }

            // Init backbuffers, etc.
            OnResetDevice();

            bloom = new Bloom(this);
            tonemap = new ToneMap(this);

            // Init windowing system
            WindowManager.Instance.Init(device);
        }

        Size GetResolution()
        {
            string cvarRes = r_resulution.String;
            int cvWidth = 0, cvHeight = 0;
            string[] cvTokens = cvarRes.Split('x');
            // No resolution in the cvar fallback
            bool fallback = false;
            if (cvTokens.Length < 2)
                fallback = true;
            else if (!int.TryParse(cvTokens[0], out cvWidth) || !int.TryParse(cvTokens[1], out cvHeight))
                fallback = true;

            if (fallback)
            {
                // Use desktop resolution
                cvWidth = form.DesktopBounds.Width;
                cvHeight = form.DesktopBounds.Height;
            }

            bool found = false;
            // Now, validate
            for (int i = 0; i < ValidResolutions.Count; i++)
            {
                string validRes = ValidResolutions[i];
                string[] tokens = validRes.Split('x');
                if (tokens.Length < 2)
                    continue;
                int width, height;
                if (!int.TryParse(tokens[0], out width) || !int.TryParse(tokens[1], out height))
                    continue;

                // Compare
                if (width == cvWidth && height == cvHeight)
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                return new Size(cvWidth, cvHeight);
            }

            return new Size(form.DesktopBounds.Width, form.DesktopBounds.Height);
        }

        public Result OnLostDevice()
        {
            foreach (HagsIndexBuffer ib in IndexBuffers.Values)
            {
                if (ib.IB != null && !ib.IB.Disposed && ib.IB.IsDefaultPool)
                {
                    ib.IB.Dispose();
                }
            }
            foreach (HagsVertexBuffer vb in VertexBuffers.Values)
            {
                if (vb.VB != null && !vb.VB.Disposed && vb.VB.IsDefaultPool)
                {
                    vb.VB.Dispose();
                }
            }
            WindowManager.Instance.OnLostDevice();
            //TextureManager.Instance.Dispose();
            effect.OnLostDevice();
            foreach (SlimDX.Direct3D9.Font font in Fonts.Values)
            {
                font.OnLostDevice();
            }
            sprite.OnLostDevice();
            bloom.OnLostDevice();
            tonemap.OnLostDevice();
            RenderTextureSfc.Dispose();
            RenderSurface.Dispose();
            RenderTexture.Dispose();
            RenderDepthSurface.Dispose();

            return new Result();
        }

        public Result OnResetDevice()
        {
            //if(WindowManager.Instance != null)
            //    WindowManager.Instance.OnResetDevice();
            effect.OnResetDevice();
            // Font init
            if (sprite == null)
                sprite = new Sprite(device);
            else
                sprite.OnResetDevice();
            foreach (SlimDX.Direct3D9.Font font in Fonts.Values)
            {
                font.OnResetDevice();
            }

            if (bloom != null)
                bloom.OnResetDevice();
            if (tonemap != null)
                tonemap.OnResetDevice();

            float FOV = (float)(Math.PI / 180f) * 65;
            // Set up view
            float aspect = (float)RenderSize.Width / (float)RenderSize.Height;
            projMatrix = Matrix.PerspectiveFovLH(FOV,
                aspect, 1.0f, 40000.0f);
            device.SetTransform(TransformState.Projection, projMatrix);
            viewMatrix = Matrix.LookAtLH(new Vector3(0, 0, 5.0f), new Vector3(), new Vector3(0, 1, 0));
            device.SetTransform(TransformState.View, viewMatrix);
            Camera = new Camera(device);

            RenderSurface = Surface.CreateRenderTarget(device, RenderSize.Width, RenderSize.Height, Format.A8R8G8B8, MultiSampling, 0, false);
            RenderTexture = new Texture(device, RenderSize.Width, RenderSize.Height, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
            RenderTextureSfc = RenderTexture.GetSurfaceLevel(0);
            RenderDepthSurface = Surface.CreateDepthStencil(device, RenderSize.Width, RenderSize.Height, Format.D16, MultisampleType.None, 0, true);

            form.FormBorderStyle = (r_fs.Integer==0 ? System.Windows.Forms.FormBorderStyle.Sizable: System.Windows.Forms.FormBorderStyle.None);
            form.TopMost = !(r_fs.Integer == 0);
            form.MaximizeBox = r_fs.Integer == 0;
            return new Result();
        }

        public void DrawFullScreenQuad()
        {
            int nPass = effect.Begin(FX.DoNotSaveState);
            for (int i = 0; i < nPass; i++)
            {
                effect.BeginPass(i);
                DrawFullScreenQuad(0.0f, 1.0f, 1.0f, 0.0f);
                effect.EndPass();
            }
            effect.End();
        }

        public void DrawFullScreenQuad(float fLeftU, float fTopV, float fRightU, float fBottomV)
        {
            Surface target = device.GetRenderTarget(0);
            SurfaceDescription rtDescr = target.Description;
            device.SetRenderState(RenderState.ZEnable, false);

            // Ensure that we're directly mapping texels to pixels by offset by 0.5
            // For more info see the doc page titled "Directly Mapping Texels to Pixels"
            float fWidth5 = (1 / (float)rtDescr.Width);// (float)rtDescr.Width - 1f;
            float fHeight5 = (-1 / (float)rtDescr.Height);//(float)rtDescr.Height -1f;

            VertexPositionNormalTextured[] quad = new VertexPositionNormalTextured[4];
            quad[0] = new VertexPositionNormalTextured(-1f - fWidth5, -1f - fHeight5, 0.5f, fLeftU, fTopV);
            quad[1] = new VertexPositionNormalTextured(1f - fWidth5, -1f - fHeight5, 0.5f, fRightU, fTopV);
            quad[2] = new VertexPositionNormalTextured(-1f - fWidth5, 1f - fHeight5, 0.5f, fLeftU, fBottomV);
            quad[3] = new VertexPositionNormalTextured(1f - fWidth5, 1f - fHeight5, 0.5f, fRightU, fBottomV);

            device.VertexFormat = VertexPositionNormalTextured.Format;
            device.DrawUserPrimitives(PrimitiveType.TriangleStrip, 2, quad);
            device.SetRenderState(RenderState.ZEnable, true);
        }

        void ToggleFillMode(string[] tokens)
        {
            // Toggle fillmode
            if (FillMode.Equals(FillMode.Solid))
                FillMode = FillMode.Wireframe;
            else if (FillMode.Equals(FillMode.Wireframe))
                FillMode = FillMode.Point;
            else if (FillMode.Equals(FillMode.Point))
                FillMode = FillMode.Solid;
        }

        private void HandleResize(object sender, EventArgs args)
        {
            if (form.WindowState == System.Windows.Forms.FormWindowState.Minimized)
                return;

            if (RenderSize.Equals(form.ClientSize))
                return;

            form.FormBorderStyle = (r_fs.Integer == 0 ? System.Windows.Forms.FormBorderStyle.Sizable : System.Windows.Forms.FormBorderStyle.None);
            form.TopMost = !(r_fs.Integer == 0);
            form.MaximizeBox = r_fs.Integer == 0;
            //form.Bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            //form.TopMost = !Windowed;
            //form.MaximizeBox = Windowed;
            

            OnLostDevice();
            lock (ObjectTable.SyncObject)
            {
                foreach (ComObject obj in ObjectTable.Objects)
                {

                    if (obj.IsDefaultPool)
                    {
                        string info = obj.ToString();

                        //if (info == "SlimDX.Direct3D9.Surface")
                        
                        System.Console.WriteLine(info);
                        obj.Dispose();
                    }
                }
            }

            RenderSize.Width = form.ClientSize.Width;
            RenderSize.Height = form.ClientSize.Height;


            _pp.BackBufferWidth = RenderSize.Width;
            _pp.BackBufferHeight = RenderSize.Height;

            device.Reset(_pp);

            OnResetDevice();
        }

        public void ChangeShaderTechnique(int index, int value, float value2, string value3)
        {
            string val = value3.Trim();
            technique = val;
        }

        public void Shutdown()
        {
            foreach (HagsIndexBuffer buf in IndexBuffers.Values)
            {
                buf.Dispose();
            }
            foreach (HagsVertexBuffer buf in VertexBuffers.Values)
            {
                buf.Dispose();
            }
            WindowManager.Instance.OnLostDevice();
            foreach(SlimDX.Direct3D9.Font font in Fonts.Values) 
                font.Dispose();
            if(sprite != null)
                sprite.Dispose();
            if (SourceMap != null)
            {
                SourceMap.Dispose();
            }
            if(RenderTexture != null)
                RenderTexture.Dispose();
            if(RenderSurface != null)
                RenderSurface.Dispose();
            if(RenderDepthSurface != null)
                RenderDepthSurface.Dispose();
            if(bloom != null)
                bloom.Dispose();
            if(tonemap != null)
                tonemap.Dispose();
            VertexPosNorInstance.vd.Dispose();
            TextureManager.Instance.Dispose();
            if (effect != null)
                effect.Dispose();
            if (device != null)
            {
                device.Direct3D.Dispose();
                device.Dispose();
            }
            Environment.Exit(0);
        }

        // Singleton public instance getter
        public static Renderer Instance
        {
            get { return _instance; }
        }

        
    }
}
