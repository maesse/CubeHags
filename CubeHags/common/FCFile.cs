using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CubeHags.client.common
{
    public class FCFile
    {
        public string FullName { get; set; } // path + filename + extension
        public string FileName { get; set; } // filename without path and extension
        //public string Extension { get; set; } // file extension

        public FCFile(string FullName)
        {
            this.FullName = FullName;
            //Extension = GetExtension(FullName);
            FileName = GetFileName(FullName, true);
        }

        // Get filename from full (path included) filename 
        public static string GetFileName(string FullName, bool extension)
        {
            // Ensure / is used as directory char
            //FullName = FullName.Replace("/", @"\");

            // Cut directories from string
            int lastDirIndex = FullName.LastIndexOf(@"\");
            if (lastDirIndex > 0 && lastDirIndex != FullName.Length-1)
            {
                FullName = FullName.Substring(lastDirIndex+1);
            }

            if (!extension)
            {
                // Cut extension
                int extIndex = FullName.LastIndexOf('.');
                if (extIndex > 0)
                {
                    FullName = FullName.Substring(0, extIndex);
                }
            }

            return FullName;
        }

        // Gets extension from full (path included) filename
        public static string GetExtension(string FullName)
        {
            string result = "";
            int extIndex = FullName.LastIndexOf('.');
            if (extIndex > 0)
            {
                result = FullName.Substring(extIndex);
            }
            
            return result;
        }
    }
}
