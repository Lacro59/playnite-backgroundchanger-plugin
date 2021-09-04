using APNG.Tool;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APNG
{
    public class CPng_Reader
    {
        public List<Chunk> Chunks { set; get; } = new List<Chunk>();
        byte[] m_PNGHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        public IHDR IHDR { get { return this.Chunks.FirstOrDefault(x => x is IHDR) as IHDR; } }
            
        public Dictionary<fcTL, MemoryStream> SpltAPng()
        {
            Dictionary<fcTL, MemoryStream> pngs = new Dictionary<fcTL, MemoryStream>();
            IHDR ihdr = new IHDR(this.IHDR);

            MemoryStream mm = new MemoryStream();
            fcTL lastfctl = null;
            for (int i = 0; i < this.Chunks.Count; i++)
            {
                switch (this.Chunks[i].ChunkType)
                {
                    case ChunkTypes.IDAT:
                        {
                            IDAT idat = this.Chunks[i] as IDAT;
                            mm.Write(idat.Data, 0, idat.Data.Length);
                        }
                        break;
                    case ChunkTypes.fcTL:
                        {
                            fcTL fctl = this.Chunks[i] as fcTL;
                            if (mm.Length > 0)
                            {
                                MemoryStream ms = new MemoryStream();
                                CPNG_Writer pngw_ = new CPNG_Writer();
                                pngw_.Open(ms);
                                ihdr.Width = lastfctl.Width;
                                ihdr.Height = lastfctl.Height;
                                pngw_.WriteIHDR(ihdr);
                                pngw_.WriteIDAT(mm.ToArray());
                                pngw_.WriteIEND();
                                ms.Position = 0;
                                pngs.Add(lastfctl, ms);
                            }
                            lastfctl = fctl;
                            mm.SetLength(0);
                        }
                        break;
                    case ChunkTypes.fdAT:
                        {
                            fdAT fdat = this.Chunks[i] as fdAT;
                            mm.Write(fdat.Data, 0, fdat.Data.Length);
                        }
                        break;
                    case ChunkTypes.IEND:
                        { 
                            MemoryStream ms = new MemoryStream();
                            CPNG_Writer pngw_ = new CPNG_Writer();
                            pngw_.Open(ms);
                            if (lastfctl != null)
                            {
                                ihdr.Width = lastfctl.Width;
                                ihdr.Height = lastfctl.Height;
                                pngw_.WriteIHDR(ihdr);
                                pngw_.WriteIDAT(mm.ToArray());
                                pngw_.WriteIEND();
                                ms.Position = 0;
                                pngs.Add(lastfctl, ms);
                            }
                            
                            mm.SetLength(0);
                        }
                        break;
                }
            }
            mm.Close();
            mm.Dispose();
            return pngs;
        }

        public CPng_Reader Open(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);
            bool result = true;
            byte[] sss = br.ReadBytes(8);
            System.Diagnostics.Trace.WriteLine(BitConverter.ToString(sss));
            while (true)
            {
                if((stream.Length - stream.Position) <4)
                {
                    break;
                }
                int len = br.ReadInt32LN();
                sss = br.ReadBytes(4);
                string id = Encoding.UTF8.GetString(sss);

                switch (id)
                {
                    case "IHDR":
                        {
                            IHDR ihdr = new IHDR();
                            ihdr.Pos = stream.Position;
                            ihdr.Size = len;
                            ParseIHDR(br, ihdr);
                            this.Chunks.Add(ihdr);
                            System.Diagnostics.Trace.WriteLine($"IHDR width:{ihdr.Width} height:{ihdr.Height}");
                        }
                        break;
                    case "acTL":
                        {
                            acTL actl = new acTL();
                            actl.Pos = stream.Position;
                            actl.Size = len;
                            actl.Num_Frames = br.ReadInt32LN();
                            actl.Num_Plays = br.ReadInt32LN();
                            actl.CRC = br.ReadBytes(4);
                            this.Chunks.Add(actl);
                            System.Diagnostics.Trace.WriteLine($"acTL num_frames:{actl.Num_Frames} num_plays:{actl.Num_Plays}");
                        }
                        break;
                    case "fcTL":
                        {
                            fcTL fcTL = new fcTL();
                            fcTL.Pos = stream.Position;
                            fcTL.Size = len;
                            fcTL.SequenceNumber = br.ReadInt32LN();
                            fcTL.Width = br.ReadInt32LN();
                            fcTL.Height = br.ReadInt32LN();
                            fcTL.X_Offset = br.ReadInt32LN();
                            fcTL.Y_Offset = br.ReadInt32LN();
                            fcTL.Delay_Num = br.ReadInt16LN();
                            fcTL.Delay_Den = br.ReadInt16LN();
                            byte dispose = br.ReadByte();
                            byte blend = br.ReadByte();
                            fcTL.Dispose_op = (fcTL.Diposes)dispose;
                            fcTL.Blend_op = (fcTL.Blends)blend;
                            fcTL.CRC = br.ReadBytes(4);
                            this.Chunks.Add(fcTL);
                            System.Diagnostics.Trace.WriteLine($"fcTL sequence_number:{fcTL.SequenceNumber} x:{fcTL.X_Offset} y:{fcTL.Y_Offset} width:{fcTL.Width} height:{fcTL.Height} delay_num:{fcTL.Delay_Num} delay_den:{fcTL.Delay_Den}, Dispose:{fcTL.Dispose_op} Blend:{fcTL.Blend_op}");
                        }
                        break;
                    case "fdAT":
                        {
                            fdAT fdat = new fdAT();
                            fdat.Pos = stream.Position;
                            fdat.Size = len-4;
                            fdat.SequenceNumber = br.ReadInt32LN();
                            fdat.Data = br.ReadBytes(len-4);
                            fdat.CRC = br.ReadBytes(4);
                            this.Chunks.Add(fdat);
                            System.Diagnostics.Trace.WriteLine($"fdAT sequence_number:{fdat.SequenceNumber} len:{fdat.Size}");
                        }
                        break;
                    case "IDAT":
                        {
                            IDAT idat = new IDAT();
                            idat.Pos = stream.Position;
                            idat.Size = len;
                            //br.BaseStream.Seek(len, SeekOrigin.Current);
                            idat.Data = br.ReadBytes(len);
                            idat.CRC = br.ReadBytes(4);
                            this.Chunks.Add(idat);
                            System.Diagnostics.Trace.WriteLine($"IDAT len:{len}");
                        }
                        break;
                    case "IEND":
                        {
                            Chunk iend = new Chunk() { ChunkType = ChunkTypes.IEND };
                            iend.CRC = br.ReadBytes(4);
                            this.Chunks.Add(iend);
                            System.Diagnostics.Trace.WriteLine($"IEND len:{len} crc:{BitConverter.ToString(iend.CRC)}");
                        }
                        break;
                    case "pHYs":
                        {
                            pHYs phys = new pHYs();
                            phys.Pos = stream.Position;
                            phys.Size = len;
                            phys.X = br.ReadInt32LN();
                            phys.Y = br.ReadInt32LN();
                            phys.Unit = br.ReadByte();
                            this.Chunks.Add(phys);
                            phys.CRC = br.ReadBytes(4);
                        }
                        break;
                    default:
                        {
                            System.Diagnostics.Trace.WriteLine(id);
                            br.ReadBytes(len + 4);
                        }
                        break;
                }
            }
            return this;
        }

        void ParseIHDR(BinaryReader br, IHDR data)
        {
            data.Width = br.ReadInt32LN();
            data.Height = br.ReadInt32LN();
            data.BitDepth = br.ReadByte();
            data.ColorType = br.ReadByte();
            data.Compression = br.ReadByte();
            data.Filter = br.ReadByte();
            data.Iterlace = br.ReadByte();
            data.CRC = br.ReadBytes(4);
            //System.Diagnostics.Trace.WriteLine($"IHDR crc:{BitConverter.ToString(data.CRC)}");
        }
    }

    
}
