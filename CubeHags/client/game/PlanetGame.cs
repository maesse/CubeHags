using System;
using System.Collections.Generic;
using System.Text;

namespace CubeHags.client.game
{
    public class PlanetGame
    {
        public bool Paused = false;

        public PlanetGame()
        {

        }

        public void Frame(float delta, Input.UserCommand cmd)
        {
            if (Paused)
                return;

            delta *= 0.001f;
            Input.Instance.CreateCmd();

            GameWorld.Instance.Frame(Input.Instance.UserCmd, delta);
        }

        public void HandleScroll(int scrollVal)
        {
            if (scrollVal == 65416)
            {
                // down
                if (GameWorld.Instance.ZoomAmount > 0.1f)
                {
                    GameWorld.Instance.ZoomAmount -= 0.1f;
                    if (GameWorld.Instance.ZoomAmount < 0.1f)
                        GameWorld.Instance.ZoomAmount = 0.1f;
                }
            }
            else if (scrollVal == 120)
            {
                // up
                if (GameWorld.Instance.ZoomAmount < 1.3f)
                {
                    GameWorld.Instance.ZoomAmount += 0.1f;
                    if (GameWorld.Instance.ZoomAmount > 1.3f)
                        GameWorld.Instance.ZoomAmount = 1.3f;
                }
            }
        }
    }
}
