using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CubeHags.client.common
{
    // Cubehags file caching system. Doesn't actually cache the files, but is more like a dictionary
    public sealed class FileCache
    {
        FCDictionary Base;
        FileCache()
        {
            Base = new FCDictionary("Base");
            Init();
        }

        // Add paths
        private void Init()
        {
            long startTime = HighResolutionTimer.Ticks;
            Insert(System.Windows.Forms.Application.StartupPath+"/data/");
            Insert(System.Windows.Forms.Application.StartupPath+"/data/gui/");
            Insert(@"C:\Users\mads\Desktop\Kode Stuff\Data\source-files\cstrike\");
            Insert(@"C:\Users\mads\Desktop\Kode Stuff\Data\source-files\cstrike\materials");
            Insert(@"C:\Users\mads\Desktop\Kode Stuff\Data\");
            Insert(@"C:\Users\mads\Desktop\Kode Stuff\Data\materials");
            Insert(System.Windows.Forms.Application.StartupPath);
            startTime = HighResolutionTimer.Ticks - startTime;
            float totalTime = (float)startTime / HighResolutionTimer.Frequency;
            System.Console.WriteLine("[FileCache] Init time: " + totalTime + "s");
        }

        public bool Contains(string FullName)
        {
            FullName = FullName.Replace("/", @"\").ToLower();
            return Base.ContainsFile(FullName);
        }

        public FCFile GetFile(string FullName)
        {
            FullName = FullName.Replace("/", @"\").ToLower();
            return Base.GetFile(FullName);
        }


        // Scans a directory and file tree and adds it to the file cache
        public void Insert(string Path)
        {
            Path = Path.Replace("/", @"\");

            if (Directory.Exists(Path))
            {
                Insert(Path, Base);
            }
            else
                System.Console.WriteLine("[FileCache] Insert(): Directory '" + Path + "' does not exist");
        }

        // Scans a directory and file tree and adds it a given dict
        private void Insert(string FullPath, FCDictionary dict)
        {
            // Add files
            string[] files = Directory.GetFiles(FullPath);
            for (int i = 0; i < files.Length; i++)
            {
                files[i] = files[i].ToLower();
                string file = FCFile.GetFileName(files[i], false);
                // Dont add files that are already contained
                if(!dict.Files.ContainsKey(file))
                    dict.AddFile(files[i]);
            }

            // Add dir recursively
            string[] dirs = Directory.GetDirectories(FullPath);
            for (int i = 0; i < dirs.Length; i++)
            {
                dirs[i] = dirs[i].ToLower();
                string dir = FCDictionary.GetLastDirectory(dirs[i]);

                // Only create new Dir object if it doesnt exists
                if (!dict.Dirs.ContainsKey(dir))
                    Insert(dirs[i], dict.AddDirectory(dirs[i])); // Add to new dir
                else
                    Insert(dirs[i], dict.Dirs[dir]); // Add to old dir
            }
        }

        // Singleton implementation
        private static readonly FileCache _Instance = new FileCache();
        public static FileCache Instance { get { return _Instance; } }
    }
}
