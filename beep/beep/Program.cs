using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharpDX;
using SharpDX.XAudio2;
using SharpDX.Multimedia;

namespace beep
{
    class Program
    {
        const int NUMOFSAMPLE = 4410;
        const int NUMOFBUF = 2;


        static AudioBuffer[] audioBuffer;
        static bool[] isWritten;
        static int bufferCount = 0;
        static xBeep beep;
        static nPD71054 timer;

        static UInt32 frameCount;

        static void Main(string[] args)
        {
            Console.WriteLine(" *** beep ver0.02 ***");

            // initialize
            beep = new xBeep();
            timer = new nPD71054();
            audioBufferInitialize();

            int helz = 2000;

            audioBuffer[bufferCount].Stream.Position = 0;
            timer.SetCount((byte)(44100 / helz & 0xFF));
            audioBuffer[bufferCount].Stream.Write((byte)((byte)timer.Update(1) * (Byte.MaxValue / 4)));
            timer.SetCount((byte)(44100 / helz >> 8));
            audioBuffer[bufferCount].Stream.Write((byte)((byte)timer.Update(1) * (Byte.MaxValue / 4)));
            timer.showStatus();

            audioBuffer[bufferCount].Stream.Write((byte)((byte)timer.Update(1) * (Byte.MaxValue / 4)));
            for (int i = 2; i < NUMOFSAMPLE; i++)
            {
                audioBuffer[bufferCount].Stream.Write((byte)((byte)timer.Update(1) * (Byte.MaxValue / 4)));
            }
            bufferCount++;

            helz = 1000;
            audioBuffer[bufferCount].Stream.Position = 0;
            timer.SetCount((byte)(44100 / helz & 0xFF));
            audioBuffer[bufferCount].Stream.Write((byte)((byte)timer.Update(1) * (Byte.MaxValue / 4)));
            timer.SetCount((byte)(44100 / helz >> 8));
            audioBuffer[bufferCount].Stream.Write((byte)((byte)timer.Update(1) * (Byte.MaxValue / 4)));
            timer.showStatus();

            audioBuffer[bufferCount].Stream.Write((byte)((byte)timer.Update(1) * (Byte.MaxValue / 4)));
            for (int i = 2; i < NUMOFSAMPLE; i++)
            {
                audioBuffer[bufferCount].Stream.Write((byte)((byte)timer.Update(1) * (Byte.MaxValue / 4)));
                if (i % 800 == 0)
                {
                    timer.showStatus();
                }
            }
            beep.submitBuffer(audioBuffer[0]);
            beep.submitBuffer(audioBuffer[1]);
            Console.ReadKey();
        }


        static void audioBufferInitialize()
        {
            // multi buffer
            audioBuffer = new AudioBuffer[NUMOFBUF];

            isWritten = new bool[NUMOFBUF];
            for (int i = 0; i < NUMOFBUF; i++)
            {
                audioBuffer[i] = new AudioBuffer
                {
                    Stream = new DataStream(NUMOFSAMPLE, true, true),
                    Flags = BufferFlags.EndOfStream,
                    AudioBytes = NUMOFSAMPLE,
                    LoopCount = 0
                };
                isWritten[i] = false;
            }
            bufferCount = 0;

        }
    }

    public class nPD71054
    {
        public const int High = 1, Low = 0, ON = 1, OFF = 0, True = 1, False = 0;

        // Counter Parameter
        public ushort COUNT { get; private set; }
        public int OUT { get; private set; }
        public int GATE { get; private set; }
        public int NC { get; private set; }
        public int CM { get; private set; }        /* always Mode3(SquareWaveGenerator) */
        public int RWM { get; private set; }
        public int BCD { get; private set; }       /* always binary */
        private bool willTransfer;
        private byte statReg;

        /* Counter Register */
        private byte[] cntReg = new byte[2] { 0, 0 };
        private int writeCount;

        private void WriteRegister(byte data)
        {
            if (writeCount == 1)
            {
                willTransfer = true;
                NC = High;
            }
            writeCount = writeCount & 0x01;
            cntReg[writeCount++] = data;
        }
        private void WriteRegister(byte data, int oe)
        {
            if (oe != Low) return;
            if (writeCount == 1)
            {
                willTransfer = true;
                NC = High;
            }
            writeCount = writeCount & 0x01;
            cntReg[writeCount++] = data;
        }
        private void Transfer()
        {
            // 偶数ならNを転送
            if ((cntReg[Low] & 0x01) == 0x00)
            {
                COUNT = (ushort)((cntReg[High] << 8) + cntReg[Low]);
            }
            // 奇数ならN-1を転送
            else COUNT = (ushort)((cntReg[High] << 8) + cntReg[Low] - 1);
            // 転送完了後:NC = Low        
            NC = Low;
            willTransfer = false;
        }

        /* Counter Latch */
        private ushort cntLatch;
        private int cntLatched, readCount;

        private void WriteLatch(ushort data)
        {
            if (cntLatched != Low) return;
            cntLatch = data;
        }
        private void WriteLatch(ushort data, int oe)
        {
            if (cntLatched != Low) return;
            if (oe != Low) return;
            cntLatch = data;
        }
        private byte ReadLatch()
        {
            readCount = readCount & 0x01;
            if (readCount == 0)
            {
                readCount++;
                return (byte)(cntLatch & 0xFF);
            }
            else
            {
                readCount++;
                cntLatched = Low;
                return (byte)(cntLatch >> 8);
            }
        }



        public nPD71054()
        {
            Reset();
        }

        public void Reset()
        {
            cntReg = new byte[2] { 0, 0 };
            cntLatch = 0;
            writeCount = 0;
            readCount = 0;
            cntLatched = 0;
            SetStatus(0x36);
        }

        public void SetStatus(byte data)
        {
            statReg = (byte)(data & 0x3F);
            // counter latch command
            if ((data & 0x30) >> 4 == 0)
            {
                cntLatched = High;
                return;
            }

            // set CountMode and R/WMode (p.10)
            RWM = (byte)((data & 0x30) >> 4);
            CM = 3;
            NC = High;
        }

        public byte GetStatus()
        {
            return (byte)(OUT << 8 + NC << 7 + statReg);
        }

        public void SetCount(byte data)
        {
            switch (RWM)
            {
                case 1: /* Lower */
                    WriteRegister(data);
                    WriteRegister(0x00);
                    break;
                case 2: /* Higher */
                    WriteRegister(0x00);
                    WriteRegister(data);
                    break;
                case 3: /* Lower-Higher */
                    WriteRegister(data);
                    break;
                default:
                    break;
            }
        }

        public byte GetCount()
        {
            switch (RWM)
            {
                case 1: /* Lower */
                    cntLatched = 0;
                    return (byte)(cntLatch & 0xFF);
                case 2: /* Higher */
                    cntLatched = 0;
                    return (byte)(cntLatch >> 8);
                case 3: /* Lower-Higher */
                    return ReadLatch();
                default:
                    break;
            }
            return 0;
        }

        public int Update(int gate)
        {
            if (willTransfer)
            {
                Transfer();
                WriteLatch(COUNT);
                return OUT;
            }

            CountDownMode3(gate);
            return OUT;
        }

        private int CountDownMode3(int gate)
        {
            // If Input GATE Trigger, Count Register will transfer.
            if ((GATE == Low) && (gate == High)) willTransfer = true;
            GATE = gate;

            // カウント禁止モード：GATEがLowならOUTはHiになります
            if (GATE != High) return OUT = High;
            // NullCountフラグHighならカウント動作スキップ
            if (NC != Low) return OUT;

            COUNT -= 2;
            // First half period
            if (OUT == High)
            {
                if (COUNT == 2)
                {
                    OUT = Low;
                    willTransfer = true;
                }
            }
            // Last half period
            else
            {
                if ((cntReg[0] & 0x01) == 0x01)
                {
                    if (COUNT == 0)
                    {
                        OUT = High;
                        willTransfer = true;
                    }
                }
                else
                {
                    if (COUNT == 2)
                    {
                        OUT = High;
                        willTransfer = true;
                    }
                }
            }
            WriteLatch(COUNT);
            return OUT;
        }

        public void showStatus()
        {
            Console.WriteLine("*** COUNTER STATUS ***");
            Console.WriteLine("CM:{0}\tRWM:{1}\tOUT:{2}\tNC:{3}\tGATE:{4}", CM, RWM, OUT, NC, GATE);
            Console.WriteLine("COUNT VALUE...{0}", COUNT);
            Console.WriteLine("COUNT REGISTER...H：{0}\tL:{1}", cntReg[1], cntReg[0]);
            Console.WriteLine("COUNT LATCH...{0}", cntLatch);
        }
    }

    public class xBeep
    {
        // Const parametor
        private const int SAMPLERATE = 44100 /* kHz */;
        private const int BITDEPTH = 8 /* 8bit/sample */;
        private const int CHANNELS = 1 /* monoral */;

        internal XAudio2 device;
        internal MasteringVoice masteringVoice;
        internal WaveFormat waveFormat;
        internal SourceVoice sourceVoice;

        public delegate void audioCallback();
        public audioCallback bufEndProc, bufStartProc;

        public xBeep()
        {
            audioInit();
        }
        public xBeep(audioCallback bufferEnd)
        {
            audioInit();
            sourceVoice.BufferEnd += (context) => bufferEnd();
            //sourceVoice.BufferStart += (context) => bufStartProc();
        }
        ~xBeep()
        {
            sourceVoice.Stop();
            sourceVoice.Dispose();
            masteringVoice.Dispose();
            device.Dispose();
        }

        private void audioInit()
        {
            device = new XAudio2();
            masteringVoice = new MasteringVoice(device);
            waveFormat = new WaveFormat(SAMPLERATE, BITDEPTH, CHANNELS);
            sourceVoice = new SourceVoice(device, waveFormat);
            sourceVoice.Start();
        }
        public void Start()
        {
            sourceVoice.Start();
        }
        public void submitBuffer(AudioBuffer buffer)
        {
            sourceVoice.SubmitSourceBuffer(buffer, null);
        }
    }
}