using System;
using System.Collections.Generic;
using System.Text;

namespace CubeHags.client.sound
{
    public class Sound
    {
        public string Name;
        public FMOD.Sound FSound;

        public FMOD.Channel Channel;

        public Sound(string name, FMOD.Sound FSound)
        {
            this.Name = name;
            this.FSound = FSound;
        }

        public void Play()
        {
            //FMOD.VECTOR pos1 = new FMOD.VECTOR();
            //pos1.x = -10.0f * DISTANCEFACTOR; pos1.y = -0.0f; pos1.z = 0.0f;

            //FMOD.VECTOR vel1 = new FMOD.VECTOR();
            //vel1.x = 0.0f; vel1.y = 0.0f; vel1.z = 0.0f;

            FMOD.RESULT result = SoundManager.Instance.System.playSound(FMOD.CHANNELINDEX.FREE, FSound, true, ref Channel);
            //ERRCHECK(result);
            //result = channel1.set3DAttributes(ref pos1, ref vel1);
            //ERRCHECK(result);
            result = Channel.setPaused(false);
            //ERRCHECK(result);
        }
    }
}
