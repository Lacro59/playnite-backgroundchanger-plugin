using APNG.Tool;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APNG
{
    public class CPNG_Writer
    {
        BinaryWriter m_Bw;
        MemoryStream m_Temp = new MemoryStream();
        byte[] m_PNGHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        public bool Open(Stream stream)
        {
            this.m_Bw = new BinaryWriter(stream);
            this.m_Bw.Write(this.m_PNGHeader);
            return true;
        }

        public bool WriteIHDR(IHDR data)
        {
            this.m_Temp.SetLength(0);
            BinaryWriter w = new BinaryWriter(this.m_Temp);
            byte[] sss = Encoding.UTF8.GetBytes("IHDR");
            w.Write(sss);
            w.WriteLN(data.Width);
            w.WriteLN(data.Height);
            w.Write(data.BitDepth);
            w.Write(data.ColorType);
            w.Write(data.Compression);
            w.Write(data.Filter);
            w.Write(data.Iterlace);

            byte[] bb = this.m_Temp.ToArray();

            this.m_Bw.WriteLN(bb.Length - 4);
            this.m_Bw.Write(bb);
            CRC32Cls crc32 = new CRC32Cls();
            ulong crc_ulong = crc32.GetCRC32Str(bb);
            sss = BitConverter.GetBytes(crc_ulong);
            Array.Reverse(sss);
            this.m_Bw.Write(sss, 4, 4);
            return true;
        }

        public void WriteIDAT(byte[] data)
        {
            this.m_Temp.SetLength(0);
            BinaryWriter w = new BinaryWriter(this.m_Temp);
            byte[] sss = Encoding.UTF8.GetBytes("IDAT");
            w.Write(sss);
            w.Write(data);
            byte[] bb = this.m_Temp.ToArray();

            this.m_Bw.WriteLN(bb.Length - 4);
            this.m_Bw.Write(bb);
            CRC32Cls crc32 = new CRC32Cls();
            ulong crc_ulong = crc32.GetCRC32Str(bb);
            sss = BitConverter.GetBytes(crc_ulong);
            Array.Reverse(sss);
            this.m_Bw.Write(sss, 4, 4);
        }

        public void WriteIEND()
        {
            this.m_Temp.SetLength(0);
            BinaryWriter w = new BinaryWriter(this.m_Temp);
            byte[] sss = Encoding.UTF8.GetBytes("IEND");
            w.Write(sss);
            byte[] bb = this.m_Temp.ToArray();

            this.m_Bw.WriteLN(bb.Length - 4);
            this.m_Bw.Write(bb);
            CRC32Cls crc32 = new CRC32Cls();
            ulong crc_ulong = crc32.GetCRC32Str(bb);
            sss = BitConverter.GetBytes(crc_ulong);
            Array.Reverse(sss);
            this.m_Bw.Write(sss, 4, 4);
        }
    }

    class CRC32Cls
    {
        static protected ulong[] Crc32Table;
        static CRC32Cls()
        {
            ulong Crc;
            Crc32Table = new ulong[256];
            int i, j;
            for (i = 0; i < 256; i++)
            {
                Crc = (ulong)i;
                for (j = 8; j > 0; j--)
                {
                    if ((Crc & 1) == 1)
                        Crc = (Crc >> 1) ^ 0xEDB88320;
                    else
                        Crc >>= 1;
                }
                Crc32Table[i] = Crc;
            }
        }

        public ulong GetCRC32Str(string sInputString)
        {
            //GetCRC32Table();
            byte[] buffer = System.Text.ASCIIEncoding.ASCII.GetBytes(sInputString);
            ulong value = 0xffffffff;
            int len = buffer.Length;
            for (int i = 0; i < len; i++)
            {
                value = (value >> 8) ^ Crc32Table[(value & 0xFF) ^ buffer[i]];
            }
            return value ^ 0xffffffff;
        }

        public ulong GetCRC32Str(byte[] data)
        {
            //GetCRC32Table();
            byte[] buffer = data;
            ulong value = 0xffffffff;
            int len = buffer.Length;
            for (int i = 0; i < len; i++)
            {
                value = (value >> 8) ^ Crc32Table[(value & 0xFF) ^ buffer[i]];
            }
            return value ^ 0xffffffff;
        }

    }
}
