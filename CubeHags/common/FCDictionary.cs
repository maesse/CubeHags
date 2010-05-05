using System;
using System.Collections.Generic;
 
using System.Text;

namespace CubeHags.client.common
{
    public class FCDictionary
    {
        public string Name { get; set; }
        public string FullName { get; set; }

        // Children
        public Dictionary<string, FCFile> Files;
        public Dictionary<string, FCDictionary> Dirs;


        public FCDictionary(string FullName)
        {
            this.FullName = FullName;
            this.Name = GetLastDirectory(FullName);

            Files = new Dictionary<string, FCFile>();
            Dirs = new Dictionary<string, FCDictionary>();
        }

        public FCFile AddFile(string FullName)
        {
            FCFile file = new FCFile(FullName);
            if (!Files.ContainsKey(file.FileName))
            {
                Files.Add(file.FileName, file);
                return file;
            }
            else
                return null;
        }

        public FCDictionary AddDirectory(string FullName)
        {
            FCDictionary dir = new FCDictionary(FullName);
            Dirs.Add(dir.Name, dir);
            return dir;
        }

        public FCFile GetFile(string name)
        {
            int dirIndex = name.IndexOf(@"\");
            if (dirIndex > 0)
            {
                // Look for a directory
                string dir = name.Substring(0, dirIndex);
                if (Dirs.ContainsKey(dir))
                {
                    dirIndex++;
                    // Continue searching down the tree
                    string shortened = name.Substring(dirIndex, name.Length - dirIndex);
                    return Dirs[dir].GetFile(shortened);
                }
            }
            else
            {
                // Look for a file
                string filename = FCFile.GetFileName(name, true);
                if (Files.ContainsKey(filename))
                    return Files[filename];
                
                // Search subfolders
                foreach (FCDictionary dir in Dirs.Values)
                {
                    FCFile file = dir.GetFile(filename);
                    if (file != null)
                        return file;
                }
            }

            return null;
        }

        // Search children for a file
        public bool ContainsFile(string name)
        {
            int dirIndex = name.IndexOf(@"\");
            if (dirIndex > 0)
            {
                // Look for a directory
                string dir = name.Substring(0, dirIndex);
                if (Dirs.ContainsKey(dir))
                {
                    dirIndex++;
                    // Continue searching down the tree
                    string shortened = name.Substring(dirIndex, name.Length - dirIndex);
                    return Dirs[dir].ContainsFile(shortened);
                }
            }
            else
            {
                // Look for a file
                string filename = FCFile.GetFileName(name,true);
                if (Files.ContainsKey(filename))
                    return true;
            }

            return false;
        }

        public static string GetLastDirectory(string FullName)
        {
            int dirIndex = FullName.LastIndexOf(@"\");
            if (dirIndex > 0 && dirIndex != FullName.Length-1)
            {
                return FullName.Substring(dirIndex+1);
            }
            else
                return FullName;
        }

        //// Returns array of directories gathered from a path
        //public static string[] GetDirectories(string FullName)
        //{

        //}

    }
}
