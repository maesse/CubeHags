using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client.gui;
using System.Drawing;

namespace CubeHags.client.input
{
    public class MouseEvent
    {
        [Flags]
        public enum EventType
        {
            NONE = 0,
            MOVE = 1,
            DRAG = 2,
            MOUSEDOWN = 4,
            MOUSEUP = 8,
            BUTTON0CHANGED = 16,
            BUTTON1CHANGED = 32,
            BUTTON2CHANGED = 64
        }

        public float dX = 0, dY = 0; // delta x/y
        public Point Position = Point.Empty;
        public EventType Type = EventType.NONE;
        public MouseEvent lastEvent;
        public bool[] ButtonState = new bool[3];

        public MouseEvent()
        {
        }

        public MouseEvent(float dX, float dY, Point Position, EventType Type, bool[] ButtonState, MouseEvent lastEvent)
        {
            this.dX = dX;
            this.dY = dY;
            this.Position = Position;
            this.Type = Type;
            this.lastEvent = lastEvent;
            if(ButtonState.Length == 3)
                this.ButtonState = ButtonState;
        }

        public override string ToString()
        {
            return String.Format("MouseEvent: d({0},{1}), pos({2},{3}), Buttons:[{4}{5}{6}], Type: {7}", dX, dY, Position.X, Position.Y, (ButtonState[0]) ? "#" : "_", (ButtonState[1]) ? "#" : "_", (ButtonState[2]) ? "#" : "_", Type);
        }

        public override bool Equals(object obj)
        {
            if (obj is MouseEvent)
            {
                MouseEvent evt = (MouseEvent)obj;
                if (evt.Type == this.Type && evt.dX == this.dX && evt.dY == this.dY)
                {
                    return (evt.ButtonState[0] == this.ButtonState[0] &&
                        evt.ButtonState[1] == this.ButtonState[1] &&
                        evt.ButtonState[2] == this.ButtonState[2] && evt.Position.X == this.Position.X && evt.Position.Y == this.Position.Y);
                }
                else
                    return false;
            }
            else
                return false;
        }
    }
}
