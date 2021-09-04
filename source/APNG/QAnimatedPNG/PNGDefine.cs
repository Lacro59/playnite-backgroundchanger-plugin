using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APNG
{
    public class pHYs : Chunk
    {
        public pHYs()
        {
            this.ChunkType = ChunkTypes.pHYs;
        }
        public int X { set; get; }
        public int Y { set; get; }
        public byte Unit { set; get; }
    }
    public class IDAT: Chunk
    {
        public IDAT()
        {
            this.ChunkType = ChunkTypes.IDAT;
        }
        public byte[] Data { set; get; }
    }

    public class fdAT : IDAT
    {
        public fdAT()
        {
            this.ChunkType = ChunkTypes.fdAT;
        }
        public int SequenceNumber { set; get; }
    }

    public class IHDR: Chunk
    {
        public IHDR()
        {
            this.ChunkType = ChunkTypes.IHDR;
        }

        public IHDR(IHDR data)
            : base(data)
        {
            this.Width = data.Width;
            this.Height = data.Height;
            this.BitDepth = data.BitDepth;
            this.ColorType = data.ColorType;
            this.Compression = data.Compression;
            this.Filter = data.Filter;
            this.Iterlace = data.Iterlace;
        }

        public int Width { set; get; }
        public int Height { set; get; }
        public byte BitDepth { set; get; }
        public byte ColorType { set; get; }
        public byte Compression { set; get; }
        public byte Filter { set; get; }
        public byte Iterlace { set; get; }
    }

    public class fcTL:Chunk
    {
        public fcTL()
        {
            this.ChunkType = ChunkTypes.fcTL;
        }
        public int SequenceNumber { set; get; }
        public int Width { set; get; }
        public int Height { set; get; }
        public int X_Offset { set; get; }
        public int Y_Offset { set; get; }
        public int Delay_Num { set; get; }
        public int Delay_Den { set; get; }
        public Diposes Dispose_op { set; get; }
        public Blends Blend_op { set; get; }

        public enum Diposes
        {
            None,
            Background,
            Previous
        }

        public enum Blends
        {
            Source,
            Over
        }
    }



    public class acTL:Chunk
    {
        public acTL()
        {
            this.ChunkType = ChunkTypes.acTL;
        }
        public int Num_Frames { set; get; }
        public int Num_Plays { set; get; }
    }

    public enum ChunkTypes
    {
        IDAT,
        pHYs,
        IHDR,
        acTL,
        fcTL,
        fdAT,
        IEND,
    }
    public class Chunk
    {
        public Chunk()
        {

        }

        public Chunk(Chunk data)
        {
            this.ChunkType = data.ChunkType;
            this.Pos = data.Pos;
            this.Size = data.Size;
            this.CRC = new byte[data.CRC.Length];
            Array.Copy(data.CRC, this.CRC, data.CRC.Length);
        }
        public ChunkTypes ChunkType { set; get; }
        public long Pos { set; get; }
        public int Size { set; get; }
        public byte[] CRC { set; get; }

    }
}
