using System;
using System.Collections.Generic;
using System.Text;
using FMOD;
using CubeHags.common;
using System.Windows.Forms;
using CubeHags.client.common;

namespace CubeHags.client.sound
{
    public sealed class SoundManager
    {
        private static readonly SoundManager _Instance = new SoundManager();
        public static SoundManager Instance { get { return _Instance; } }

        Dictionary<string, Sound> Sounds = new Dictionary<string, Sound>();

        public FMOD.System System;

        SoundManager()
        {
            Init();
            InitSounds();
        }

        void InitSounds()
        {
            
        }

        public Sound GetSound(string str)
        {
            if (Sounds.ContainsKey(str))
                return Sounds[str];
            else
                return null;
        }

        public Sound LoadSound(string path, string soundName)
        {
            if (Sounds.ContainsKey(soundName))
                return Sounds[soundName];


            if (!FileCache.Instance.Contains(path))
                return null;

            FCFile file = FileCache.Instance.GetFile(path);

            FMOD.Sound sound = null;
            FMOD.RESULT result;
            result = System.createSound(file.FullName, (FMOD.MODE.HARDWARE | FMOD.MODE._3D), ref sound);
            if (result != FMOD.RESULT.OK)
            {
                Common.Instance.WriteLine("Loading sound failed: " + path + " - " + result.ToString());
                return null;
            }

            Sound realsound = new Sound(soundName, sound);
            Sounds.Add(soundName, realsound);
            return realsound;

            //result = sound1.set3DMinMaxDistance(2.0f * DISTANCEFACTOR, 10000.0f * DISTANCEFACTOR);
            //ERRCHECK(result);
            
            //result = sound.setMode(FMOD.MODE.LOOP_NORMAL);
            //ERRCHECK(result);
        }

        void Init()
        {
            uint version = 0;
            FMOD.RESULT result;

            /*
                Create a System object and initialize.
            */
            result = FMOD.Factory.System_Create(ref System);
            ERRCHECK(result);

            result = System.getVersion(ref version);
            ERRCHECK(result);
            if (version < FMOD.VERSION.number)
            {
                MessageBox.Show("Error!  You are using an old version of FMOD " + version.ToString("X") + ".  This program requires " + FMOD.VERSION.number.ToString("X") + ".");
                Application.Exit();
            }

            FMOD.CAPS caps = FMOD.CAPS.NONE;
            FMOD.SPEAKERMODE speakermode = FMOD.SPEAKERMODE.STEREO;

            int minfrequency = 0, maxfrequency = 0;
            StringBuilder name = new StringBuilder(128);

            result = System.getDriverCaps(0, ref caps, ref minfrequency, ref maxfrequency, ref speakermode);
            ERRCHECK(result);

            result = System.setSpeakerMode(speakermode);                             /* Set the user selected speaker mode. */
            ERRCHECK(result);

            if ((caps & FMOD.CAPS.HARDWARE_EMULATED) == FMOD.CAPS.HARDWARE_EMULATED) /* The user has the 'Acceleration' slider set to off!  This is really bad for latency!. */
            {                                                                        /* You might want to warn the user about this. */
                result = System.setDSPBufferSize(1024, 10);	                     /* At 48khz, the latency between issuing an fmod command and hearing it will now be about 213ms. */
                ERRCHECK(result);
            }

            FMOD.GUID guid = new FMOD.GUID();

            result = System.getDriverInfo(0, name, 256, ref guid);
            ERRCHECK(result);

            if (name.ToString().IndexOf("SigmaTel") != -1)   /* Sigmatel sound devices crackle for some reason if the format is pcm 16bit.  pcm floating point output seems to solve it. */
            {
                result = System.setSoftwareFormat(48000, FMOD.SOUND_FORMAT.PCMFLOAT, 0, 0, FMOD.DSP_RESAMPLER.LINEAR);
                ERRCHECK(result);
            }

            result = System.init(32, FMOD.INITFLAGS.NORMAL, (IntPtr)null);
            if (result == FMOD.RESULT.ERR_OUTPUT_CREATEBUFFER)
            {
                result = System.setSpeakerMode(FMOD.SPEAKERMODE.STEREO);             /* Ok, the speaker mode selected isn't supported by this soundcard.  Switch it back to stereo... */
                ERRCHECK(result);

                result = System.init(32, FMOD.INITFLAGS.NORMAL, (IntPtr)null);        /* Replace with whatever channel count and flags you use! */
                ERRCHECK(result);
            }

            ///*
            //    Set the distance units. (meters/feet etc).
            //*/
            //result = system.set3DSettings(1.0f, DISTANCEFACTOR, 1.0f);
            //ERRCHECK(result);

            /*
                Load some sounds
            */
            

            //result = system.createSound("../../../../../examples/media/jaguar.wav", (FMOD.MODE.HARDWARE | FMOD.MODE._3D), ref sound2);
            //ERRCHECK(result);
            //result = sound2.set3DMinMaxDistance(2.0f * DISTANCEFACTOR, 10000.0f * DISTANCEFACTOR);
            //ERRCHECK(result);
            //result = sound2.setMode(FMOD.MODE.LOOP_NORMAL);
            //ERRCHECK(result);

            //result = system.createSound("../../../../../examples/media/swish.wav", (FMOD.MODE.HARDWARE | FMOD.MODE._2D), ref sound3);
            //ERRCHECK(result);

            /*
                Play sounds at certain positions
            */
            //{
            //    FMOD.VECTOR pos1 = new FMOD.VECTOR();
            //    pos1.x = -10.0f * DISTANCEFACTOR; pos1.y = -0.0f; pos1.z = 0.0f;

            //    FMOD.VECTOR vel1 = new FMOD.VECTOR();
            //    vel1.x = 0.0f; vel1.y = 0.0f; vel1.z = 0.0f;

            //    result = system.playSound(FMOD.CHANNELINDEX.FREE, sound1, true, ref channel1);
            //    ERRCHECK(result);
            //    result = channel1.set3DAttributes(ref pos1, ref vel1);
            //    ERRCHECK(result);
            //    result = channel1.setPaused(false);
            //    ERRCHECK(result);
            //}

            //{
            //    FMOD.VECTOR pos2 = new FMOD.VECTOR();
            //    pos2.x = 15.0f * DISTANCEFACTOR; pos2.y = -0.0f; pos2.z = -0.0f;

            //    FMOD.VECTOR vel2 = new FMOD.VECTOR();
            //    vel2.x = 0.0f; vel2.y = 0.0f; vel2.z = 0.0f;

            //    result = system.playSound(FMOD.CHANNELINDEX.FREE, sound2, true, ref channel2);
            //    ERRCHECK(result);
            //    result = channel2.set3DAttributes(ref pos2, ref vel2);
            //    ERRCHECK(result);
            //    result = channel2.setPaused(false);
            //    ERRCHECK(result);
            //}

            //lastpos.x = 0.0f;
            //lastpos.y = 0.0f;
            //lastpos.z = 0.0f;

            //listenerpos.x = 0.0f;
            //listenerpos.y = 0.0f;
            //listenerpos.z = -1.0f * DISTANCEFACTOR;
        }

        private void ERRCHECK(FMOD.RESULT result)
        {
            if (result != FMOD.RESULT.OK)
            {
                MessageBox.Show("FMOD error! " + result + " - " + FMOD.Error.String(result));
                Environment.Exit(-1);
            }
        }
    }
}
