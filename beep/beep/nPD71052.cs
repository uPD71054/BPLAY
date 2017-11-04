using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace beep
{
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
            // Transfre is performed at the first CLK pulse after the trigger.
            if ((GATE == Low) && (gate == High)) willTransfer = true;
            GATE = gate;
            // Count disable:If GATE becomes low level OUT will high level.
            if (GATE != High) return OUT = High;

            // The count is declimented by two.
            COUNT -= 2;
            // The half cycle while OUT is high level.
            if (OUT == High)
            {
                if (COUNT == 2)
                {
                    OUT = Low;
                    willTransfer = true;
                }
            }
            // The half cycle while OUT is low level.
            else
            {
                // Count N is odd.
                if ((cntReg[0] & 0x01) == 0x01)
                {
                    if (COUNT == 0)
                    {
                        OUT = High;
                        willTransfer = true;
                    }
                }
                // Count N is even.
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
}
