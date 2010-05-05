using System;
using System.Collections.Generic;
 
using System.Text;
using System.Diagnostics;

namespace CubeHags.client
{
    sealed class Profiler
    {
        private static readonly Profiler _Instance = new Profiler();
        private Dictionary<string, long> timings;
        public bool Enabled = true;

        Profiler()
        {
            timings = new Dictionary<string, long>();
        }

        public void Clear()
        {
            timings.Clear();
        }

        public Stopwatch Start()
        {
            if (Enabled)
                return Stopwatch.StartNew();
            else
                return null;
        }

        public void Stop(Stopwatch sw, string name)
        {
            if (Enabled)
            {
                sw.Stop();

                if (timings.ContainsKey(name))
                    timings[name] = sw.ElapsedTicks;
                else
                    timings.Add(name, sw.ElapsedTicks);
            }
        }

        public Dictionary<string, long> GetTimings()
        {
            return timings;
        }

        public static Profiler Instance {
            get { return _Instance; }
        }
    }
}
