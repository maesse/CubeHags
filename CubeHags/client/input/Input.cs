using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using SlimDX;
using System.Threading;
using CubeHags.client.input;
using SlimDX.Windows;
using System.Drawing;
using CubeHags.client.gui;
using SlimDX.Direct3D9;
using SlimDX.RawInput;
using CubeHags.client.common;
using System.IO;
using System.Windows.Forms;
using CubeHags.common;
using Lidgren.Network;


namespace CubeHags.client
{
    public delegate void InputHandler(object sender, InputArgs e);
    public delegate void InputMouseHandler(object sender, MouseEvent e);
    struct KeyEventPacked
    {
        public System.Windows.Forms.KeyEventArgs Args;
        public long time;
        public bool Pressed;
    }
    
    public enum MouseState
    {
        GAME,
        GUI,
        UNFOCUSED
    }
    public sealed class Input
    {
        // User input command pr. frame
        public class UserCommand
        {
            public int serverTime;
            public int anglex; // mouse
            public int angley; 
            public int anglez;
            public int buttons;
            public byte weapon;
            public sbyte forwardmove, rightmove, upmove; // keyboard movement
            public int DX, DY;
        }

        public struct Button
        {
            public int down0; // key nums holding it down
            public int down1; // key nums holding it down
            public long downtime; // timestamp
            public float deltatime; // msec down this frame if both a down and up happened
            public bool active; // current state
        }

        Button in_forward = new Button(), in_back = new Button(), in_moveleft = new Button();
        Button in_moveright = new Button(), in_up = new Button(), in_down = new Button();

        // Singleton private instance
        private static readonly Input _instance = new Input();

        // Event Dispatching
        public event InputHandler Event;
        private event InputHandler LockedEvent;

        // Keyboard
        private bool _KeyLock = false;
        public bool KeyLock { get { return _KeyLock; } }
        public KeyEvent.Modifiers KeyModifiers = KeyEvent.Modifiers.NONE;

        // Mouse settings saved for when form loses focus
        private bool hasFocus = true;
        private MouseState lostFocusMouseState = MouseState.GAME;
        private Point HiddenMousePosition = Point.Empty;
        private bool HiddenMouseSwitched = false;

        // New Mouse code
        public MouseEvent GetMouseEvent { get { return CurrentMouseEvent; } }
        public int MouseX { get { return mouseX; } }
        public int MouseY { get { return mouseY; } }
        public int MouseDX { get { return mousedx; } }
        public int MouseDY { get { return mousedy; } }
        public MouseState MouseState { get { return _mouseState; } set { lastMouseState = _mouseState; _mouseState = value; ApplyMouseState(value, lastMouseState); } }
        private MouseState lastMouseState;
        public bool MouseRawInput = true;

        // Current state
        private MouseState _mouseState = MouseState.GAME;
        private int mouseX, mouseY;
        private int mousedx, mousedy;
        private bool[] mousebuttons = new bool[3];
        private MouseEvent CurrentMouseEvent; // Current mouseevent = lastmouseargs delta currentargs
        private MouseInputEventArgs CurrentRawMouse;
        private MouseInputEventArgs NextRawMouse;
        private List<MouseInputEventArgs> NextRawMouseList = new List<MouseInputEventArgs>();

        private List<System.Windows.Forms.MouseEventArgs> compatLastMouseArgs = new List<System.Windows.Forms.MouseEventArgs>(); // MouseArgs used last frame
        private List<System.Windows.Forms.MouseEventArgs> compatCurrentMouseArgs = new List<System.Windows.Forms.MouseEventArgs>(); // Mouseargs for use this frame
        private List<System.Windows.Forms.MouseEventArgs> compatNextMouseArgs = new List<System.Windows.Forms.MouseEventArgs>(); // mouseargs for use next frame
        
        private MouseEvent LastMouseEvent;
        // Windows cursor hiding
        private bool _MouseHidden = true;
        private bool MouseHidden {
            get { return _MouseHidden; }
            set { if (value != _MouseHidden) { _MouseHidden = value; SetMouseHidden(value); } } }
        private bool MouseCentered = true;
        private Point MouseDeltaAfterCenter;

        // New Keyboard code
        private List<KeyEventArgs> newKeyEvents = new List<KeyEventArgs>(); // to be added to pressedkeys
        private List<KeyEventPacked> compatNewKeyEvents = new List<KeyEventPacked>(); // to be added to pressedkeys (compatability)
        private List<Keys> pressedKeys = new List<Keys>(); // updated on each updateKeyboard()
        public bool KeyboardRepeat = false; // Enables or disabled repeat of keyevents
        public UserCommand UserCmd = new UserCommand();
        float frame_msec;
        private float oldFrameTime;
        private int buttons;

        List<char> keysPressed = new List<char>();

        Input()
        {
            SlimDX.RawInput.Device.RegisterDevice(SlimDX.Multimedia.UsagePage.Generic, SlimDX.Multimedia.UsageId.Mouse, DeviceFlags.None);
            SlimDX.RawInput.Device.MouseInput += new EventHandler<MouseInputEventArgs>(RawInputEvent);
            MouseState = client.MouseState.GUI;
            Commands.Instance.ExecuteText(Commands.EXECTYPE.EXEC_NOW, "toggleui");
        }

        // Implement +-movement commands
        void IN_UpDown(string[] tokens) { IN_KeyDown(tokens, ref in_up); }
        void IN_UpUp(string[] tokens) { IN_KeyUp(tokens, ref in_up); }
        void IN_DownDown(string[] tokens) { IN_KeyDown(tokens, ref in_down); }
        void IN_DownUp(string[] tokens) { IN_KeyUp(tokens, ref in_down); }
        void IN_MoveleftDown(string[] tokens) { IN_KeyDown(tokens, ref in_moveleft); }
        void IN_MoveleftUp(string[] tokens) { IN_KeyUp(tokens, ref in_moveleft); }
        void IN_MoverightDown(string[] tokens) { IN_KeyDown(tokens, ref in_moveright); }
        void IN_MoverightUp(string[] tokens) { IN_KeyUp(tokens, ref in_moveright); }
        void IN_ForwardDown(string[] tokens) { IN_KeyDown(tokens, ref in_forward); }
        void IN_ForwardUp(string[] tokens) { IN_KeyUp(tokens, ref in_forward); }
        void IN_BackDown(string[] tokens) { IN_KeyDown(tokens, ref  in_back); }
        void IN_BackUp(string[] tokens) { IN_KeyUp(tokens, ref in_back); }

        // Called at the beggining of each frame (only one time pr. frame)
        public void Update()
        {
            if (MouseCentered)
                ResetMousePosition();
            UpdateMouse();
            UpdateKeyboard();
        }

        // Update mouse input
        private void UpdateMouse()
        {
            UpdateMouseSlimDX();
        }

        private void UpdateKeyboard()
        {
            UpdateKeyboardSlimDX();
        }

        public bool IsKeyDown(Keys key)
        {
            return pressedKeys.Contains(key);
        }

        // Called every frame to builds and sends a command packet to the server.
        public void SendCmd()
        {
            // don't send any message if not connected
            if (Client.Instance.state < ConnectState.CONNECTED || !Net.Instance.IsClientConnected)
                return;

            Input.Instance.CreateNewCommands();

            // don't send a packet if the last packet was sent too recently
            if (!ReadyToSendPacket())
                return;

            WritePacket();
        }

        public void WritePacket()
        {
            if (Client.Instance.state == ConnectState.CINEMATIC)
                return;

            NetBuffer buffer = new NetBuffer();

            // write the current serverId so the server
            // can tell if this is from the current gameState
            buffer.Write(Client.Instance.cl.serverId);

            // write the last message we received, which can
            // be used for delta compression, and is also used
            // to tell if we dropped a gamestate
            buffer.Write(Client.Instance.clc.serverMessageSequence);

            // write the last reliable message we received
            buffer.Write(Client.Instance.clc.serverCommandSequence);
            // write any unacknowledged clientCommands
            for (int i = Client.Instance.clc.reliableAcknowledge + 1; i <= Client.Instance.clc.reliableSequence; i++)
            {
                buffer.Write((byte)clc_ops_e.clc_clientCommand);
                buffer.Write(i);
                buffer.Write(Client.Instance.clc.reliableCommands[i & 63]);
            }

            // we want to send all the usercmds that were generated in the last
            // few packet, so even if a couple packets are dropped in a row,
            // all the cmds will make it to the server
            if (Client.Instance.cl_packetdup.Integer < 0)
                CVars.Instance.Set("cl_cmdbackup", "0");
            else if (Client.Instance.cl_packetdup.Integer > 5)
                CVars.Instance.Set("cl_cmdbackup", "5");


            int oldPacketNum = (Client.Instance.clc.netchan.outgoingSequence - 1 - Client.Instance.cl_packetdup.Integer) & 31;
            int count = Client.Instance.cl.cmdNumber - Client.Instance.cl.outPackets[oldPacketNum].p_cmdNumber;
            if (count > 32)
            {
                count = 32;
                Common.Instance.WriteLine("MAX_PACKET_USERCMDS");
            }
            int oldcmd = -1;
            if (count >= 1)
            {
                // begin a client move command
                if (!Client.Instance.cl.snap.valid || Client.Instance.clc.serverMessageSequence != Client.Instance.cl.snap.messageNum 
                    || Client.Instance.cl_nodelta.Integer > 0)
                {
                    buffer.Write((byte)clc_ops_e.clc_moveNoDelta);
                } else
                    buffer.Write((byte)clc_ops_e.clc_move);

                // write the command count
                buffer.Write((byte)count);

                // write all the commands, including the predicted command
                UserCommand newcmd = new UserCommand();
                for (int i = 0; i < count; i++)
                {
                    int j = (Client.Instance.cl.cmdNumber - count + i +1) & 63;

                    if(i == 0)
                        WriteDeltaUsercmdKey(buffer, ref newcmd, ref Client.Instance.cl.cmds[j]);
                    else
                        WriteDeltaUsercmdKey(buffer, ref Client.Instance.cl.cmds[oldcmd], ref Client.Instance.cl.cmds[j]);
                    oldcmd = j;
                }
            }

            //
            // deliver the message
            //
            int packetNum = Client.Instance.clc.netchan.outgoingSequence & 31;
            Client.Instance.cl.outPackets[packetNum].p_realtime = (int)Client.Instance.realtime;
            Client.Instance.cl.outPackets[packetNum].p_serverTime = ((oldcmd == -1) ? 0 : Client.Instance.cl.cmds[oldcmd].serverTime);
            Client.Instance.cl.outPackets[packetNum].p_cmdNumber = Client.Instance.cl.cmdNumber;
            Client.Instance.clc.lastPacketSentTime = (int)Client.Instance.realtime;

            // Bam, send...
            Client.Instance.NetChan_Transmit(Client.Instance.clc.netchan, buffer);
        }

        private void WriteDeltaUsercmdKey(NetBuffer msg, ref UserCommand from, ref UserCommand to)
        {
            // Can delta time fit in one byte?
            if (to.serverTime - from.serverTime < 256)
            {
                msg.Write(true);
                msg.Write((uint)(to.serverTime - from.serverTime), 8);
            }
            else
            {
                msg.Write(false);
                msg.Write(to.serverTime, 32);
            }
            if (from.anglex == to.anglex &&
                from.angley == to.angley &&
                from.anglez == to.anglez &&
                from.buttons == to.buttons &&
                from.forwardmove == to.forwardmove &&
                from.rightmove == to.rightmove &&
                from.upmove == to.upmove &&
                from.weapon == to.weapon)
            {
                msg.Write(false); // no change
                return;
            }
            msg.Write(true);
            MSG_WriteDeltaKey(msg, (uint)from.anglex, (uint)to.anglex, 16);
            MSG_WriteDeltaKey(msg, (uint)from.angley, (uint)to.angley, 16);
            MSG_WriteDeltaKey(msg, (uint)from.anglez, (uint)to.anglez, 16);
            MSG_WriteDeltaKey(msg,  from.forwardmove, to.forwardmove, 8);
            MSG_WriteDeltaKey(msg,  from.rightmove, to.rightmove, 8);
            MSG_WriteDeltaKey(msg,  from.upmove, to.upmove, 8);
            MSG_WriteDeltaKey(msg,from.buttons, to.buttons, 16);
            MSG_WriteDeltaKey(msg, from.weapon, to.weapon, 8);

        }

        public UserCommand MSG_ReadDeltaUsercmdKey(NetBuffer msg, ref  UserCommand from)
        {
            UserCommand to = new UserCommand();   
            if (msg.ReadBoolean())
                to.serverTime = from.serverTime + (int)msg.ReadUInt32(8);
            else
                to.serverTime = msg.ReadInt32();
            if (msg.ReadBoolean())
            {
                to.anglex = (int)MSG_ReadDeltaKey(msg, (uint)from.anglex, 16);
                to.angley = (int)MSG_ReadDeltaKey(msg, (uint)from.angley, 16);
                to.anglez = (int)MSG_ReadDeltaKey(msg, (uint)from.anglez, 16);
                to.forwardmove = (sbyte)MSG_ReadDeltaKey(msg,  from.forwardmove, 8);
                to.rightmove = (sbyte)MSG_ReadDeltaKey(msg, from.rightmove, 8);
                to.upmove = (sbyte)MSG_ReadDeltaKey(msg, from.upmove, 8);
                to.buttons = MSG_ReadDeltaKey(msg,  from.buttons, 16);
                to.weapon = (byte)MSG_ReadDeltaKey(msg, from.weapon, 8);
            }
            else
            {
                to.anglex = from.anglex;
                to.angley = from.angley;
                to.anglez = from.anglez;
                to.forwardmove = from.forwardmove;
                to.rightmove = from.rightmove;
                to.upmove = from.upmove;
                to.buttons = from.buttons;
                to.weapon = from.weapon;
            }

            return to;
        }

        void MSG_WriteDeltaKey(NetBuffer msg, int oldV, int newV, int bits)
        {
            if (oldV == newV)
            {
                msg.Write(false);
                return;
            }
            msg.Write(true);
            msg.Write(newV, bits);
        }

        void MSG_WriteDeltaKey(NetBuffer msg, uint oldV, uint newV, int bits)
        {
            if (oldV == newV)
            {
                msg.Write(false);
                return;
            }
            msg.Write(true);
            msg.Write(newV, bits);
        }

        uint MSG_ReadDeltaKey(NetBuffer msg, uint oldV, int bits)
        {
            if (msg.ReadBoolean())
            {
                return (msg.ReadUInt32(bits));
            }
            return oldV;
        }

        int MSG_ReadDeltaKey(NetBuffer msg, int oldV, int bits)
        {
            if (msg.ReadBoolean())
            {
                return (msg.ReadInt32(bits));
            }
            return oldV;
        }

        bool ReadyToSendPacket()
        {
            if (Client.Instance.state == ConnectState.CINEMATIC)
                return false;

            // if we don't have a valid gamestate yet, only send
            // one packet a second
            if (Client.Instance.state != ConnectState.ACTIVE &&
                Client.Instance.state != ConnectState.PRIMED &&
                Client.Instance.realtime - Client.Instance.clc.lastPacketSentTime < 1000)
                return false;

            // send every frame for LAN
            if(Net.Instance.IsLanAddress(Client.Instance.clc.netchan.remoteAddress.Address))
                return true;

            if (Client.Instance.cl_maxpackets.Integer < 15)
                CVars.Instance.Set("cl_cmdrate", "15");
            else if (Client.Instance.cl_maxpackets.Integer > 125)
                CVars.Instance.Set("cl_cmdrate", "125");

            int oldpacketNum = (Client.Instance.clc.netchan.outgoingSequence - 1) & 31;
            int delta = Client.Instance.realtime - Client.Instance.cl.outPackets[oldpacketNum].p_realtime;

            // the accumulated commands will go out in the next packet
            if (delta < 1000 / Client.Instance.cl_maxpackets.Integer)
                return false;

            return true;
        }

        // Create a new usercmd_t structure for this frame
        void CreateNewCommands()
        {
            // no need to create usercmds until we have a gamestate
            if (Client.Instance.state < ConnectState.PRIMED)
                return;

            frame_msec = Common.Instance.frameTime - oldFrameTime;

            // if running less than 5fps, truncate the extra time to prevent
            // unexpected moves after a hitch
            if (frame_msec > 200)
                frame_msec = 200;
            oldFrameTime = Common.Instance.frameTime;

            Client.Instance.cl.cmdNumber++;
            int cmdNum = Client.Instance.cl.cmdNumber & 63;
            Client.Instance.cl.cmds[cmdNum] = CreateCmd();
        }

        public UserCommand CreateCmd()
        {
            Vector3 oldAngles = Client.Instance.cl.viewAngles;
            UserCommand cmd = new UserCommand();
            CmdButtons(ref cmd);
            KeyMove(ref cmd);
            MouseMove(ref cmd);
            
            // check to make sure the angles haven't wrapped
            if (Client.Instance.cl.viewAngles[0] - oldAngles[0] > 90)
            {
                Client.Instance.cl.viewAngles[0] = oldAngles[0] + 90;
            }
            else if (oldAngles[0] - Client.Instance.cl.viewAngles[0] > 90)
            {
                Client.Instance.cl.viewAngles[0] = oldAngles[0] - 90;
            }



            FinishMove(ref cmd);

            UserCmd = cmd;

            return cmd;
        }

        private void FinishMove(ref UserCommand cmd)
        {
            // copy the state that the cgame is currently sending
            cmd.weapon = (byte)Client.Instance.cl.cgameUserCmdValue;

            // send the current server time so the amount of movement
            // can be determined without allowing cheating
            cmd.serverTime = Client.Instance.cl.serverTime;

            cmd.anglex = ((int)(Client.Instance.cl.viewAngles[0] * 65536 / 360) & 65535);
            cmd.angley = ((int)(Client.Instance.cl.viewAngles[1] * 65536 / 360) & 65535);
            cmd.anglez = ((int)(Client.Instance.cl.viewAngles[2] * 65536 / 360) & 65535);
            cmd.DX = mousedx;
            cmd.DY = mousedy;
        }

        private void CmdButtons(ref UserCommand cmd)
        {
            cmd.buttons = buttons;
            //for (int i = 0; i < 15; i++)
            //{
            //    // Check mouse buttons
                
            //}
        }

        void MouseMove(ref UserCommand cmd)
        {
            Client.Instance.cl.viewAngles[1] -= mousedx * Client.Instance.sensitivity.Value * 0.05f;
            Client.Instance.cl.viewAngles[0] += mousedy * Client.Instance.sensitivity.Value * 0.05f;
        }

        void KeyMove(ref UserCommand cmd)
        {
            int forward = 0, side = 0, up = 0;
            int movespeed = 127;

            side += (int)(movespeed * KeyState(ref in_moveright));
            side -= (int)(movespeed * KeyState(ref in_moveleft));

            up += (int)(movespeed * KeyState(ref in_up));
            up -= (int)(movespeed * KeyState(ref in_down));

            forward += (int)(movespeed * KeyState(ref in_forward));
            forward -= (int)(movespeed * KeyState(ref in_back));

            cmd.forwardmove = ClampSByte(forward);
            cmd.rightmove = ClampSByte(side);
            cmd.upmove = ClampSByte(up);
            //if(cmd.forwardmove != 0 || cmd.rightmove != 0)
            //    System.Console.WriteLine("{0} {1} {2}", cmd.forwardmove, cmd.rightmove, cmd.upmove);
        }

        // Returns a float for fraction of frame the key has been held down
        private float KeyState(ref Button b)
        {
            float msec = b.deltatime;
            b.deltatime = 0;
            if (b.active)
            {
                // still down
                if (b.downtime == 0)
                    msec = frame_msec/2f; // FIX
                else
                    msec += ((float)(HighResolutionTimer.Ticks - b.downtime) / HighResolutionTimer.Frequency * 1000f);
                b.downtime = HighResolutionTimer.Ticks;
            }

            float val = (float)msec / frame_msec;
            if (val < 0)
                val = 0;
            else if (val > 1)
                val = 1;
            return val;
        }

        void IN_KeyDown(string[] tokens, ref Button b)
        {
            // k is (int)Keys
            int k = -1;

            if (tokens.Length > 2)
                k = int.Parse(tokens[1]);

            if (k == b.down0 || k == b.down1)
            {
                return; // repeating key
            }

            if (b.down0 == 0)
                b.down0 = k;
            else if (b.down1 == 0)
                b.down1 = k;
            else
            {
                System.Console.WriteLine("Tree keys down for a button!");
                return;
            }

            if (b.active)
            {
                return; // still down
            }

            b.downtime = long.Parse(tokens[2]);
            b.active = true;
        }

        void IN_KeyUp(string[] tokens, ref Button b)
        {
            int k = -1;
            if (tokens.Length > 2)
                k = int.Parse(tokens[1]);
            else
            {
                // typed manually at the console, assume for unsticking, so clear all
                b.down0 = b.down1 = 0;
                b.active = false;
            }

            if (b.down0 == k)
                b.down0 = 0;
            else if (b.down1 == k)
                b.down1 = 0;
            else
                return;

            if (b.down0 != 0 || b.down1 != 0)
                return; // some other key is holding it down

            b.active = false;
            long uptime = long.Parse(tokens[2]);
            b.deltatime = ((float)(uptime - b.downtime) / (float)HighResolutionTimer.Frequency * 1000f);
        }

        // Resets mouse position to middle of picture so it doesnt fly off screen
        private void ResetMousePosition()
        {
            // Winpos + (size/2)
            MouseDeltaAfterCenter = System.Windows.Forms.Cursor.Position;
            Viewport vp = Renderer.Instance.device.Viewport;
            Point windowPos = new Point();
            windowPos = new Point((int)Renderer.Instance.form.Left, (int)Renderer.Instance.form.Top);
            Point newPoint = new Point((int)(windowPos.X + vp.X + vp.Width / 2), (int)(windowPos.Y + vp.Y + vp.Height / 2));
            System.Windows.Forms.Cursor.Position = newPoint;
            MouseDeltaAfterCenter = new Point(newPoint.X - MouseDeltaAfterCenter.X, newPoint.Y - MouseDeltaAfterCenter.Y);
        }

        // Hides mouse from the screen
        private void SetMouseHidden(bool value)
        {
            if (value)
            {
                HiddenMousePosition = System.Windows.Forms.Cursor.Position;
                System.Windows.Forms.Cursor.Hide();
            }
            else if (MouseState != client.MouseState.UNFOCUSED)
            {
                System.Windows.Forms.Cursor.Position = HiddenMousePosition;
            }
        }

        // Sets current mouse state (In GUI, Game or in another application)
        private void ApplyMouseState(MouseState value, MouseState lastValue)
        {
            if (value == client.MouseState.GAME)
            {
                if (lastValue == client.MouseState.GUI)
                {
                    HiddenMousePosition = System.Windows.Forms.Cursor.Position;
                    System.Windows.Forms.Cursor.Hide();
                }
                MouseHidden = true;
                MouseCentered = true;
            }
            else if (value == client.MouseState.GUI)
            {
                if (lastValue == client.MouseState.GAME)
                {
                    if (HiddenMousePosition.X != 0 && HiddenMousePosition.Y != 0)
                        System.Windows.Forms.Cursor.Position = HiddenMousePosition;
                }
                MouseHidden = true;
                MouseCentered = false;
            }
            else if (value == client.MouseState.UNFOCUSED)
            {
                MouseHidden = false;
                MouseCentered = false;
            }
        }

        // Window recieved focus
        void GotFocus()
        {
            if (hasFocus && MouseState != client.MouseState.UNFOCUSED)
            {
                return;
            }
            Renderer.Instance.form.FormBorderStyle = (!Renderer.Instance.r_fs.Bool ? System.Windows.Forms.FormBorderStyle.Sizable : System.Windows.Forms.FormBorderStyle.None);
            Renderer.Instance.form.TopMost = Renderer.Instance.r_fs.Bool;
            Renderer.Instance.form.MaximizeBox = !Renderer.Instance.r_fs.Bool;
            Renderer.Instance.form.Focus();
            // Retrieve focus info
            MouseState = lostFocusMouseState;
            hasFocus = true;
        }

        // Window lost focus
        void LostFocus()
        {
            if (Renderer.Instance.r_fs.Bool)
                return;
            if (MouseState == client.MouseState.UNFOCUSED)
                return;

            // Save focus info
            lostFocusMouseState = MouseState;
            MouseState = client.MouseState.UNFOCUSED;
            hasFocus = false;
        }

        public void ToggleUI()
        {
            if (MouseState == client.MouseState.UNFOCUSED)
                return;

            // Toggle between gui and game
            if (MouseState == client.MouseState.GAME)
                MouseState = client.MouseState.GUI;
            else if (MouseState == client.MouseState.GUI)
                MouseState = client.MouseState.GAME;
            WindowManager.Instance.ToggleUI(null);
        }

        // RawInput
        void RawInputEvent(object sender, MouseInputEventArgs e)
        {
            NextRawMouseList.Add(e);
            NextRawMouse = e;
        }

        // SLIMDX Input
        void renderForm_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (hasFocus)
                compatNextMouseArgs.Add(e);
        }

        void renderForm_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (hasFocus)
                compatNextMouseArgs.Add(e);
        }

        void renderForm_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (hasFocus)
            {
                if ((int)e.KeyChar == 27)
                {
                    compatNewKeyEvents.Add(new KeyEventPacked() { time = HighResolutionTimer.Ticks, Pressed = true, Args = new KeyEventArgs(Keys.Enter) });
                    compatNewKeyEvents.Add(new KeyEventPacked() { time = HighResolutionTimer.Ticks+10, Pressed = false, Args = new KeyEventArgs(Keys.Enter) });
                    return;
                }
                keysPressed.Add(e.KeyChar);
            }
        }
        

        void renderForm_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (hasFocus)
            {
                
                // Recieved a Keys.None press
                if (e.KeyCode == 0)
                    return;
                // Avoid repeat events
                if (!KeyboardRepeat && IsKeyDown(e.KeyCode))
                    return;
                KeyEventPacked evt = new KeyEventPacked();
                evt.Args = e;
                evt.Pressed = true;
                evt.time = HighResolutionTimer.Ticks;
                compatNewKeyEvents.Add(evt);
                e.Handled = true;
            }
        }

        void renderForm_KeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            //if (hasFocus)
            {
                KeyEventPacked evt = new KeyEventPacked();
                evt.Args = e;
                evt.Pressed = false;
                evt.time = HighResolutionTimer.Ticks;
                compatNewKeyEvents.Add(evt);
            }
        }

        private void UpdateKeyboardSlimDX()
        {
            // Iterate over new keyevents
            List<KeyEvent> events = new List<KeyEvent>();
            foreach (KeyEventPacked evt in compatNewKeyEvents)
            {
                bool doAdd = true;
                //System.Console.WriteLine(KeyCodeToKey((System.Windows.Forms.Keys)evt.Args.KeyValue));
                // Update internal pressedKeyList
                if (evt.Pressed)
                {
                    // Only add once
                    Keys key = evt.Args.KeyCode;
                    if (!pressedKeys.Contains(key))
                    {
                        pressedKeys.Add(key);
                        // Test
                        if (Client.Instance.state == ConnectState.CINEMATIC)
                            {
                                // Exit cinematic
                                Client.Instance.cin.StopCinematic();
                            }
                        else if (key == System.Windows.Forms.Keys.Escape)
                        {
                            
                            ToggleUI();
                            
                        }
                        else if (key == Keys.F4 && evt.Args.Alt)
                        {
                            System.Console.WriteLine("Shutting down..");
                            Renderer.Instance.Shutdown();
                            return;
                        }
                        else if (key == Keys.Enter && evt.Args.Alt)
                        {
                            Renderer.Instance.r_fs = CVars.Instance.Set("r_fs", Renderer.Instance.r_fs.Integer == 1 ? "0" : "1");
                            Renderer.Instance._sizeChanged = true;
                            return;
                        }
                        else if (key == Keys.Oem5)
                        {
                            HagsConsole.Instance.ToggleConsole();
                        }

                        // Handle binds
                        if (HagsConsole.Instance.IsVisible)
                        {
                            
                        }

                        // Let game handle key
                        if (!HagsConsole.Instance.IsVisible && MouseState == client.MouseState.GAME)
                            KeyHags.Instance.ParseBinding(key, evt.Pressed, evt.time);
                    }
                    else
                        doAdd = false; // Dont add to events, because key is already pressed
                }
                else
                {
                    // Handle binds
                    KeyHags.Instance.ParseBinding(evt.Args.KeyCode, evt.Pressed, evt.time);
                    pressedKeys.Remove(evt.Args.KeyCode);
                }

                // Add up events
                if (doAdd)
                {
                    KeyEvent instantEvent = new KeyEvent(evt.Pressed, evt.Args.KeyCode, getModifiers(evt.Args));
                    events.Add(instantEvent);
                }
            }
            newKeyEvents.Clear();
            compatNewKeyEvents.Clear();

            if (HagsConsole.Instance.IsVisible)
            {
                for (int i = 0; i < keysPressed.Count; i++)
                {
                    KeyEvent evt = new KeyEvent(keysPressed[i]);
                    events.Add(evt);
                }
            }
            keysPressed.Clear();

            // Fire events
            FireEvent(new InputArgs(events));
        }
        

        private void UpdateMouseSlimDX()
        {
            // Do Non-WPF input handling
            // Switch to next MouseArgs
            // Check delta
            mousedx = 0; mousedy = 0;
            int deltaX = 0, deltaY = 0;
            // RawInput only for delta x/y
            if (MouseState == client.MouseState.GAME && MouseRawInput)
            {
                if (NextRawMouse != null)
                {
                    // Use raw input
                    for (int i = 0; i < NextRawMouseList.Count; i++)
                    {
                        deltaX += NextRawMouseList[i].X;
                        deltaY += NextRawMouseList[i].Y;
                        if ((NextRawMouseList[i].ButtonFlags & MouseButtonFlags.LeftDown) == MouseButtonFlags.LeftDown)
                            buttons = 1;
                        
                        if ((NextRawMouseList[i].ButtonFlags & MouseButtonFlags.LeftUp) == MouseButtonFlags.LeftUp)
                            buttons = 0;

                        if ((NextRawMouseList[i].ButtonFlags & MouseButtonFlags.MouseWheel) == MouseButtonFlags.MouseWheel)
                        {
                            if (NextRawMouse.WheelDelta != 0)
                            {
                                //if (Common.Instance.PlanetGame != null)
                                //    Common.Instance.PlanetGame.HandleScroll(NextRawMouse.WheelDelta);
                            }
                        }

                    }
                    NextRawMouseList.Clear();
                    CurrentRawMouse = NextRawMouse;
                    NextRawMouse = null;
                }
            }
            if (compatNextMouseArgs.Count > 0)
            {
                compatLastMouseArgs = compatCurrentMouseArgs;
                compatCurrentMouseArgs = compatNextMouseArgs;
                compatLastMouseArgs.Clear();
                compatNextMouseArgs = compatLastMouseArgs;

                // Avoid cyclic dependencies
                if (LastMouseEvent != null && CurrentMouseEvent != null)
                {
                    CurrentMouseEvent.lastEvent = null;
                    LastMouseEvent = CurrentMouseEvent;
                }

                // Get latest event
                System.Windows.Forms.MouseEventArgs evnt = null;
                if (compatCurrentMouseArgs.Count > 1)
                {
                    evnt = compatCurrentMouseArgs[compatCurrentMouseArgs.Count - 1];
                }
                else
                    evnt = compatCurrentMouseArgs[0];

                // Screen-space Position
                Point pos = evnt.Location;

                // Forms Input
                if ((MouseState != client.MouseState.GAME || !MouseRawInput) && (evnt.X != mouseX || evnt.Y != mouseY))
                {
                    deltaX = (int)mouseX - evnt.X;
                    deltaY = (int)mouseY - evnt.Y;
                }

                // Mouse centering will create an event with the delta movement, that we have to substract here
                if (MouseCentered && (MouseState != client.MouseState.GAME || MouseRawInput))
                {
                    deltaX -= MouseDeltaAfterCenter.X;
                    deltaY -= MouseDeltaAfterCenter.Y;
                }

                // Button states
                bool[] buttonState = new bool[3];
                buttonState[0] = (evnt.Button & System.Windows.Forms.MouseButtons.Left) == System.Windows.Forms.MouseButtons.Left ? true : false;
                buttonState[1] = (evnt.Button & System.Windows.Forms.MouseButtons.Middle) == System.Windows.Forms.MouseButtons.Middle ? true : false;
                buttonState[2] = (evnt.Button & System.Windows.Forms.MouseButtons.Right) == System.Windows.Forms.MouseButtons.Right ? true : false;

                MouseEvent.EventType type = MouseEvent.EventType.NONE;
                if (deltaX != 0 || deltaY != 0)
                    type |= MouseEvent.EventType.MOVE;

                // Compare buttons state changes
                if (buttonState[0] != mousebuttons[0]) type |= MouseEvent.EventType.BUTTON0CHANGED;
                if (buttonState[1] != mousebuttons[1]) type |= MouseEvent.EventType.BUTTON1CHANGED;
                if (buttonState[2] != mousebuttons[2]) type |= MouseEvent.EventType.BUTTON2CHANGED;

                for (int i = 0; i < 3; i++)
                {
                    // Have one button + movement? Drag.
                    if (buttonState[i] == true && (type & MouseEvent.EventType.MOVE) == MouseEvent.EventType.MOVE)
                        type |= MouseEvent.EventType.DRAG;
                    // Look for new buttons pressed
                    if (buttonState[i] == true && mousebuttons[i] == false)
                        type |= MouseEvent.EventType.MOUSEDOWN;
                    // Look for buttons now released
                    else if (buttonState[i] == false && mousebuttons[i] == true)
                        type |= MouseEvent.EventType.MOUSEUP;
                }

                if (HiddenMouseSwitched)
                    HiddenMouseSwitched = false;

                // Create mouse event
                CurrentMouseEvent = new MouseEvent(-deltaX, -deltaY, pos, type, buttonState, LastMouseEvent);
                mousebuttons = buttonState;
                mouseX = pos.X;
                mouseY = pos.Y;

                // If i GUI, send to WindowManager
                if (MouseState == client.MouseState.GUI && (type > 0))
                {
                    WindowManager.Instance.HandleMouseEvent(CurrentMouseEvent);
                    mousedy = mousedx = 0;
                }
                else
                {
                    mousedx = deltaX;
                    mousedy = deltaY;
                }
            }
            else if (CurrentMouseEvent != null)
            {
                CurrentMouseEvent.lastEvent = null;
                LastMouseEvent = CurrentMouseEvent;
                CurrentMouseEvent = new MouseEvent(0, 0, CurrentMouseEvent.Position, MouseEvent.EventType.NONE, CurrentMouseEvent.ButtonState, CurrentMouseEvent);
            }
        }
        

        // Initialize input class
        public void InitializeInput()
        {
            RenderForm renderForm = Client.Instance.form;
            renderForm.LostFocus += new EventHandler(renderForm_LostFocus);
            renderForm.GotFocus += new EventHandler(renderForm_GotFocus);
            renderForm.MouseMove += new System.Windows.Forms.MouseEventHandler(renderForm_MouseMove);
            renderForm.KeyPress += new KeyPressEventHandler(renderForm_KeyPress);
            renderForm.KeyUp += new System.Windows.Forms.KeyEventHandler(renderForm_KeyUp);
            renderForm.KeyDown += new System.Windows.Forms.KeyEventHandler(renderForm_KeyDown);
            renderForm.MouseDown += new MouseEventHandler(renderForm_MouseDown);
            
            // Init binds..
            Commands.Instance.AddCommand("+moveup", new CommandDelegate(IN_UpDown));
            Commands.Instance.AddCommand("-moveup", new CommandDelegate(IN_UpUp));
            Commands.Instance.AddCommand("+movedown", new CommandDelegate(IN_DownDown));
            Commands.Instance.AddCommand("-movedown", new CommandDelegate(IN_DownUp));
            Commands.Instance.AddCommand("+forward", new CommandDelegate(IN_ForwardDown));
            Commands.Instance.AddCommand("-forward", new CommandDelegate(IN_ForwardUp));
            Commands.Instance.AddCommand("+back", new CommandDelegate(IN_BackDown));
            Commands.Instance.AddCommand("-back", new CommandDelegate(IN_BackUp));
            Commands.Instance.AddCommand("+moveright", new CommandDelegate(IN_MoverightDown));
            Commands.Instance.AddCommand("-moveright", new CommandDelegate(IN_MoverightUp));
            Commands.Instance.AddCommand("+moveleft", new CommandDelegate(IN_MoveleftDown));
            Commands.Instance.AddCommand("-moveleft", new CommandDelegate(IN_MoveleftUp));

            KeyHags.Instance.SetBind(Keys.W, "+forward");
            KeyHags.Instance.SetBind(Keys.S, "+back");
            KeyHags.Instance.SetBind(Keys.A, "+moveleft");
            KeyHags.Instance.SetBind(Keys.D, "+moveright");
            //KeyHags.Instance.SetBind(Keys.C, "+movedown");
            KeyHags.Instance.SetBind(Keys.Space, "+moveup");
            HiddenMousePosition = new Point(Renderer.Instance.form.DesktopLocation.X + (Renderer.Instance.form.DesktopBounds.Width / 2), Renderer.Instance.form.DesktopLocation.Y + (Renderer.Instance.form.DesktopBounds.Height / 2));
        }

        

        // Method used to fire event to all listeners
        public void FireEvent(InputArgs e)
        {
            if (Event != null)
            {
                Event(this, e);
            }
        }

        // Frees the keylock
        public void FreeKeyLock()
        {
            _KeyLock = false;
        }

        // Enables the keylock if possible
        public bool RetrieveKeyLock(InputHandler handler)
        {
            if (!_KeyLock)
            {
                _KeyLock = true;
                LockedEvent = handler;
                return _KeyLock;
            }
            else
                return false;
        }

        private static sbyte ClampSByte(int i)
        {
            if (i < -128)
                return (sbyte)-128;
            if (i > 127)
                return (sbyte)127;
            return (sbyte)i;
        }

        private CubeHags.client.KeyEvent.Modifiers getModifiers(System.Windows.Forms.KeyEventArgs args)
        {
            CubeHags.client.KeyEvent.Modifiers mods = KeyEvent.Modifiers.NONE;
            if ((args.Modifiers & System.Windows.Forms.Keys.Control) == System.Windows.Forms.Keys.Control)
                mods |= KeyEvent.Modifiers.CONTROL;
            if ((args.Modifiers & System.Windows.Forms.Keys.Control) == System.Windows.Forms.Keys.Alt)
                mods |= KeyEvent.Modifiers.ALT;
            if ((args.Modifiers & System.Windows.Forms.Keys.Control) == System.Windows.Forms.Keys.Shift)
                mods |= KeyEvent.Modifiers.SHIFT;

            return mods;
        }

        // Focus events

        void renderForm_GotFocus(object sender, EventArgs e)
        {
            GotFocus();
        }

        void renderForm_LostFocus(object sender, EventArgs e)
        {
            LostFocus();
        }

        // Singleton implementation
        static public Input Instance
        {
            get { return _instance; }
        }
    }
}
