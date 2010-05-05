using System;
using System.Collections.Generic;
 
using System.Text;
using System.Collections;

namespace CubeHags.client.render
{
    class SortItem
    {
        
        // 2 bit - fullscreen layer
        // 3 bit - viewport
        // 3 bit - viewport layer
        // 2 bit - translucency
        // 11 bit - vertexbuffer
        // 11 bit - indexbuffer
        // 16 bit material ID
        // 16 bit depth

        [Flags]
        public enum FSLayer
        {
            GAME = 0x3,
            EFFECT = 0x2,
            HUD = 0x1,
            EXTRA = 0x0
        } // 2 bit

        [Flags]
        public enum Viewport
        {
            STATIC = 0x7,
            DYNAMIC = 0x6,
            INSTANCED = 0x5,
            FOUR = 0x4,
            FIVE = 0x3,
            SIX = 0x2,
            SEVEN = 0x1,
            EIGHT = 0x0
        } // 3 bit

        [Flags]
        public enum VPLayer
        {
            WORLD = 0x2,
            SKYBOX3D = 0x3,
            SKYBOX = 0x4,
            EFFECT = 0x1,
            HUD = 0x0
        } // 3 bit

        [Flags]
        public enum Translucency
        {
            OPAQUE = 0x3,
            NORMAL = 0x2,
            ADDITIVE = 0x1,
            SUBSTRACTIVE = 0x0
        } // 2 bit

        //static void Main(string[] args)
        //{
        //    Random random = new Random();
        //    for (int i = 0; i < 10; i++)
        //    {
        //        uint mat = (uint)random.Next(1073741823);
        //        System.Console.Write("Input: " + mat);
        //        ulong result = GenerateBits(FSLayer.GAME, Viewport.ONE, VPLayer.WORLD, Translucency.OPAQUE, mat, 0);
        //        System.Console.WriteLine(" - Output: " + GetMaterial(result));
        //    }
        //    for (int i = 0; i < 10; i++)
        //    {
        //        uint mat = (uint)random.Next(1073741823);
        //        System.Console.Write("Input: " + mat);
        //        ulong result = GenerateBits(FSLayer.GAME, Viewport.ONE, VPLayer.WORLD, Translucency.OPAQUE, mat, 0);
        //        System.Console.WriteLine(" - Output: " + GetMaterial(result));
        //    }

        //    System.Console.ReadKey();

        //}



        public static FSLayer GetFSLayer(ulong bitvector)
        {
            FSLayer result = (FSLayer)(bitvector >> 62);
            return result;
        }

        public static Translucency GetTranslucency(ulong bitvector)
        {
            Translucency trans = (Translucency)((bitvector << 8) >> 62);
            return trans;
        }

        public static uint GetMaterial(ulong bitvector)
        {
            if (GetTranslucency(bitvector) == Translucency.OPAQUE)
                return GetMaterial(bitvector, true);
            else
                return GetMaterial(bitvector, false);
        }

        public static uint GetMaterial(ulong bitvector, bool materialFirst)
        {
            uint result = 0;

            if (materialFirst)
                result = (uint)(bitvector << 32 >> 48);
            else
                result = (uint)(bitvector << 48 >> 48);

            return result;
        }

        public static Viewport GetViewport(ulong bitvector)
        {
            Viewport vp = (Viewport)(bitvector << 2 >> 61);
            return vp;
        }

        public static VPLayer GetVPLayer(ulong bitvector)
        {
            VPLayer vpLayer = (VPLayer)(bitvector << 5 >> 61);
            return vpLayer;
        }

        public static ushort GetDepth(ulong bitvector)
        {
            if (GetTranslucency(bitvector) == Translucency.OPAQUE)
                return GetDepth(bitvector, true);
            else
                return GetDepth(bitvector, false);
        }

        public static ushort GetDepth(ulong bitvector, bool materialFirst)
        {
            ushort result = 0;

            if (materialFirst)
                result = (ushort)(bitvector << 48 >> 48);
            else
                result = (ushort)(bitvector << 32 >> 48);

            return result;
        }

        public static ushort GetVBID(ulong bitvector)
        {
            ushort result = (ushort)(bitvector << 10 >> 53);
            return result;
        }

        public static ushort GetIBID(ulong bitvector)
        {
            ushort result = (ushort)(bitvector << 21 >> 53);
            return result;
        }

        // Pack input data to ulong
        public static ulong GenerateBits(FSLayer layer, Viewport vp, VPLayer vpLayer, Translucency trans, ushort Material, ushort depth, ushort ibID, ushort vbID)
        {
            ulong result = (ulong)layer << 62;
            result += (ulong)vp << 59;
            result += (ulong)vpLayer << 56;
            result += (ulong)trans << 54;

            result += (ulong)vbID << 43;
            result += (ulong)ibID << 32;

            if (trans == Translucency.OPAQUE)
            {
                // Material first
                result += (ulong)Material << 16;
                result += (ulong)depth;
            }
            else
            {
                // Depth first
                result += (ulong)depth << 16;
                result += (ulong)Material;
            }

            return result;
        }
    }
}
