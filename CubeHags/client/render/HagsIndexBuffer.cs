using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX.Direct3D9;
using SlimDX;

namespace CubeHags.client.render
{
    public class HagsIndexBuffer
    {
        // Todo: Make more sophisticated id system that releases id's when disposed
        private static ushort NextIndexBufferID = 1;
        private ushort _IndexBufferID;
        public ushort IndexBufferID { get { return _IndexBufferID; } }

        private IndexBuffer _IB;
        public IndexBuffer IB { get { return _IB; } }

        public HagsIndexBuffer()
        {
            _IndexBufferID = NextIndexBufferID++;
            Renderer.Instance.IndexBuffers.Add(IndexBufferID, this);
        }

        public void SetIB<T>(T[] indices, int nBytes, Usage usage, bool sixteenBit) where T : struct
        {
            SetIB<T>(indices, nBytes, usage, sixteenBit, Renderer.Instance.Is3D9Ex ? Pool.Default : Pool.Managed, -1);
        }

        public void SetIB<T>(T[] indices, int nBytes, Usage usage, bool sixteenBit, Pool pool) where T : struct
        {
            SetIB<T>(indices, nBytes, usage, sixteenBit, Renderer.Instance.Is3D9Ex ? Pool.Default : Pool.Managed, -1);
        }

        public void SetIB<T>(T[] indices, int nBytes, Usage usage, bool sixteenBit, Pool pool, int count) where T : struct
        {
            if (nBytes <= 0)
                return;

            if (_IB != null && !_IB.Disposed && _IB.Description.SizeInBytes < nBytes)
            {
                _IB.Dispose();
                _IB = new IndexBuffer(Renderer.Instance.device, nBytes, usage, pool, sixteenBit);
            }
            else if (_IB == null)
            {
                _IB = new IndexBuffer(Renderer.Instance.device, nBytes, usage, pool, sixteenBit);
            }
            try
            {
                DataStream ds = _IB.Lock(0, nBytes, _IB.IsDefaultPool ? LockFlags.Discard : LockFlags.None);
                if(count > 0)
                    ds.WriteRange<T>(indices, 0, count);
                else
                    ds.WriteRange<T>(indices);
                _IB.Unlock();
            }
            catch {
                _IB = new IndexBuffer(Renderer.Instance.device, nBytes, usage, pool, sixteenBit);
                DataStream ds = _IB.Lock(0, nBytes, _IB.IsDefaultPool ? LockFlags.Discard : LockFlags.None);
                if (count > 0)
                    ds.WriteRange<T>(indices, 0, count);
                else
                    ds.WriteRange<T>(indices);
                _IB.Unlock();
            }
            
        }

        // Gets number of verts used in a list of indices
        public static int GetVertexCount(IEnumerable<uint> list)
        {
            uint min = uint.MaxValue, max = uint.MinValue;
            foreach (uint value in list)
            {
                if (value > max)
                    max = value;
                else if (value < min)
                    min = value;
            }
            return (int)(max - min)+1;
        }

        public void Dispose()
        {
            if (_IB != null)
                _IB.Dispose();
        }
    }
}
