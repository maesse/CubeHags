using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX.Direct3D9;
using SlimDX;

namespace CubeHags.client.render
{
    public class HagsVertexBuffer
    {
        private static ushort NextVertexBufferID = 1;
        private ushort _VertexBufferID;
        public ushort VertexBufferID { get { return _VertexBufferID; } }

        private VertexBuffer _VB;
        public VertexBuffer VB { get { return _VB; } }

        private VertexFormat _vf;
        public VertexFormat VF { get { return _vf; } }

        private VertexDeclaration _vd;
        public VertexDeclaration VD { get { return _vd; } }

        public HagsVertexBuffer()
        {
            _VertexBufferID = NextVertexBufferID++;
            Renderer.Instance.VertexBuffers.Add(VertexBufferID, this);
        }

        public void SetVD(VertexDeclaration vd)
        {
            if (_vd != null)
                _vd.Dispose();
            _vd = vd;
        }

        // Sets data in VB with offset for insertion. If VB is too small, the old data will be copied over
        public void SetVB<T>(T[] data, int nBytes, VertexFormat format, Usage usage, int offset) where T : struct
        {
            if (nBytes == 0)
                return;
            // Check VB status
            if (_VB == null || _VB.Disposed)
            {
                _VB = new VertexBuffer(Renderer.Instance.device, nBytes, usage, format, Renderer.Instance.Is3D9Ex || (usage & Usage.Dynamic) == Usage.Dynamic? Pool.Default : Pool.Managed);
            }
            else if (_VB.Description.FVF != format)
            {
                System.Console.WriteLine("SetVB(): Cannot append data of another format");
                return;
            }

            // Create new if too small
            if (_VB.Description.SizeInBytes < nBytes + offset)
            {
                // Dont copy old data if inserting at the start of the buffer, OR buffer is write-only
                if (offset <= 0 || (usage & Usage.WriteOnly) == Usage.WriteOnly) 
                {
                    int oldSize = _VB.Description.SizeInBytes;
                    // Dispose old vb
                    if (_VB != null && !_VB.Disposed)
                        _VB.Dispose();
                    // Create new
                    _VB = new VertexBuffer(Renderer.Instance.device, (nBytes + oldSize) * 2, usage, format, Renderer.Instance.Is3D9Ex || (usage & Usage.Dynamic) == Usage.Dynamic ? Pool.Default : Pool.Managed);
                    
                } 
                else 
                {
                    // save old vb
                    VertexBuffer oldvb = _VB;
                    // Create new vb
                    _VB = new VertexBuffer(Renderer.Instance.device, (nBytes + oldvb.Description.SizeInBytes)*2, usage, format, Renderer.Instance.Is3D9Ex || (usage & Usage.Dynamic) == Usage.Dynamic ? Pool.Default : Pool.Managed);

                    // Lock vbs
                    DataStream ds = oldvb.Lock(0, oldvb.Description.SizeInBytes, LockFlags.ReadOnly);
                    DataStream datastream = _VB.Lock(0, oldvb.Description.SizeInBytes, (usage & Usage.Dynamic) == Usage.Dynamic ? LockFlags.NoOverwrite : LockFlags.None);
                    // Copy old data
                    //System.IO.MemoryStream ms = new System.IO.MemoryStream((int)ds.Length);
                    
                    if (ds.Length <= int.MaxValue)
                    {
                        byte[] buf = new byte[ds.Length];
                        ds.Read(buf, 0, (int)ds.Length);
                        datastream.Write(buf, 0, buf.Length);
                    }
                    else
                    {
                        throw new Exception("Time to fix that buffer...");
                    }
                    //ds.CopyTo(ms);
                    //ms.CopyTo(datastream);
                    
                    // Cleanup
                    _VB.Unlock();
                    oldvb.Unlock();
                    oldvb.Dispose();
                }
            }
            // (usage & Usage.Dynamic) == Usage.Dynamic ? LockFlags.NoOverwrite :
            // Write new data
            DataStream ds2 = _VB.Lock(offset, nBytes, offset > 0 ? LockFlags.NoOverwrite:LockFlags.Discard);
            //data.CopyTo(ds2, offset);
            ds2.WriteRange<T>(data);
            _VB.Unlock();

            // Update vertex format
            if (_vf != format)
            {
                if (_vd != null && !_vd.Disposed)
                    _vd.Dispose();
                _vd = new VertexDeclaration(Renderer.Instance.device, D3DX.DeclaratorFromFVF(format));
                _vf = format;
            }
        }

        // Sets data in VB
        public void SetVB<T>(T[] data, int nBytes, VertexFormat format, Usage usage) where T : struct
        {
            // Check VB status
            if (_VB == null || _VB.Disposed || _VB.Description.FVF != format)
            {
                _VB = new VertexBuffer(Renderer.Instance.device, nBytes, usage, format, Renderer.Instance.Is3D9Ex || (usage & Usage.Dynamic) == Usage.Dynamic ? Pool.Default : Pool.Managed);
            }
            // Create new if too small
            else if (_VB.Description.SizeInBytes < nBytes)
            {
                if (_VB != null && !_VB.Disposed)
                    _VB.Dispose();
                _VB = new VertexBuffer(Renderer.Instance.device, nBytes, usage, format, Renderer.Instance.Is3D9Ex || (usage & Usage.Dynamic) == Usage.Dynamic ? Pool.Default : Pool.Managed);
            }
            
            // Write data
            DataStream datastream = _VB.Lock(0, nBytes, LockFlags.None);
            datastream.WriteRange<T>(data);
            _VB.Unlock();

            // Update vertex format
            if (_vf != format)
            {
                if (_vd != null && !_vd.Disposed)
                    _vd.Dispose();
                _vd = new VertexDeclaration(Renderer.Instance.device, D3DX.DeclaratorFromFVF(format));
                _vf = format;
            }
        }

        public void Dispose()
        {
            if (_VB != null)
                _VB.Dispose();
            if (_vd != null)
                _vd.Dispose();
        }
    }
}
