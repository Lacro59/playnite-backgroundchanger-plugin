using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APNG.Tool
{
    public static class BinaryReader_Litten
    {
        public static byte[] ReadBytesLN(this BinaryReader src, int count)
        {
            byte[] bb = src.ReadBytes(count);
            Array.Reverse(bb);
            return bb;
        }
        public static short ReadInt16LN(this BinaryReader src)
        {
            byte[] buf = src.ReadBytes(2);
            Array.Reverse(buf);
            return BitConverter.ToInt16(buf, 0);
        }

        public static ushort ReadUInt16LN(this BinaryReader src)
        {
            byte[] buf = src.ReadBytes(2);
            Array.Reverse(buf);
            return BitConverter.ToUInt16(buf, 0);
        }

        public static int ReadInt32LN(this BinaryReader src)
        {
            byte[] buf = src.ReadBytes(4);
            Array.Reverse(buf);
            return BitConverter.ToInt32(buf, 0);
        }

        public static uint ReadUInt32LN(this BinaryReader src)
        {
            byte[] buf = src.ReadBytes(4);
            Array.Reverse(buf);
            return BitConverter.ToUInt32(buf, 0);
        }

        public static long ReadInt64LN(this BinaryReader src)
        {
            byte[] buf = src.ReadBytes(8);
            Array.Reverse(buf);
            return BitConverter.ToInt64(buf, 0);
        }

        public static ulong ReadUInt64LN(this BinaryReader src)
        {
            byte[] buf = src.ReadBytes(8);
            Array.Reverse(buf);
            return BitConverter.ToUInt64(buf, 0);
        }
    }

    public static class BinaryWriter_Litten
    {
        public static void WriteLN(this BinaryWriter src, short data)
        {
            byte[] buf = BitConverter.GetBytes(data);
            Array.Reverse(buf);
            src.Write(buf);
        }

        public static void WriteLN(this BinaryWriter src, ushort data)
        {
            byte[] buf = BitConverter.GetBytes(data);
            Array.Reverse(buf);
            src.Write(buf);
        }

        public static void WriteLN(this BinaryWriter src, int data)
        {
            byte[] buf = BitConverter.GetBytes(data);
            Array.Reverse(buf);
            src.Write(buf);
        }

        public static void WriteLN(this BinaryWriter src, uint data)
        {
            byte[] buf = BitConverter.GetBytes(data);
            Array.Reverse(buf);
            src.Write(buf);
        }

        public static void WriteLN(this BinaryWriter src, long data)
        {
            byte[] buf = BitConverter.GetBytes(data);
            Array.Reverse(buf);
            src.Write(buf);
        }

        public static void WriteLN(this BinaryWriter src, ulong data)
        {
            byte[] buf = BitConverter.GetBytes(data);
            Array.Reverse(buf);
            src.Write(buf);
        }
    }
}
