using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace beep
{
    class Program
    {
        static void Main(string[] args)
        {
            uPD71054 timer = new uPD71054();
            timer.SetStatus(0x36);
            timer.SetCount(0x07);
            timer.SetCount(0x00);
            for (int i = 0; i < 10; i++)
            {
                timer.Update(1);
            }
        }
    }

    public class uPD71054
    {
        const int High = 1, Low = 0, ON = 1, OFF = 0, True = 1, False = 0;

        // Counter Parameter
        public ushort COUNT { get; internal set; }
        public int OUT { get; internal set; }
        public int GATE { get; internal set; }
        public int NC { get; internal set; }
        public int CM { get; internal set; }        /* always Mode3(SquareWaveGenerator) */
        public int RWM { get; internal set; }
        public int BCD { get; internal set; }       /* always binary */

        internal bool willTransfer;
        internal byte statReg, statLatch;

        /* Counter Register */
        internal byte[] cntReg;
        internal int writeCount;
        // Register operation
        public void WriteRegister(byte data)
        {
            if (writeCount == 1) NC = High;
            writeCount = writeCount & 0x01;
            cntReg[writeCount++] = data;
        }
        public void WriteRegister(byte data, int oe)
        {
            if (oe != Low) return;
            writeCount = writeCount & 0x01;
            cntReg[writeCount++] = data;
        }
        internal void Transfer()
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
        internal ushort cntLatch;
        internal int latched, readCount;
        // Latch operation
        internal void WriteLatch(ushort data)
        {
            if (latched != Low) return;
            cntLatch = data;
        }
        internal void WriteLatch(ushort data, int oe)
        {
            if (latched != Low) return;
            if (oe != Low) return;
            cntLatch = data;
        }
        public byte ReadLatch()
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
                latched = Low;
                return (byte)(cntLatch >> 8);
            }
        }



        public uPD71054()
        {
            cntReg = new byte[2] { 0, 0 };
            writeCount = 0;
            SetStatus(0x36);
        }

        internal void SetStatus(byte data)
        {
            statReg = (byte)(data & 0x3F);
            // counter latch command
            if ((data & 0x30) >> 4 == 0)
            {
                latched = High;
                return;
            }

            // set CountMode and R/WMode (p.10)
            RWM = (byte)((data & 0x30) >> 4);
            CM = (byte)((data & 0x0E) >> 1);
            if (CM > 5) CM -= 4;
            NC = High;
#if DEBUG
            showStatus();
#endif
        }

        internal byte GetStatus()
        {
            latched = Low;
            return statLatch;
        }

        internal void SetCount(byte data)
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

        internal byte GetCount(byte data)
        {
            switch (RWM)
            {
                case 1: /* Lower */
                    latched = 0;
                    return (byte)(cntLatch & 0xFF);
                case 2: /* Higher */
                    latched = 0;
                    return (byte)(cntLatch >> 8);
                case 3: /* Lower-Higher */
                    return ReadLatch();
                default:
                    break;
            }
            return 0;
        }

        internal void Update(int gate)
        {
            if (willTransfer)
            {
                Transfer();
                WriteLatch(COUNT);
#if DEBUG
                showStatus();
#endif
                return;
            }

            if ((GATE == Low) && (gate == High))
            {
                willTransfer = true;      // 次のCLKパルスで転送
            }
            GATE = gate;

            if (NC == 0)
            {
                countDownMode3(gate);
                WriteLatch(COUNT);
            }
#if DEBUG
            showStatus();
#endif
        }

        internal int countDownMode3(int gate)
        {
            // カウント禁止モード：GATEがLowならOUTはHiになります。
            if (GATE != High) return OUT = High;

            // 2ずつデクリメント
            COUNT -= 2;

            // 最初の半周期
            if (OUT == High)
            {
                if (COUNT == 2)
                {
                    OUT = Low;
                    willTransfer = true;
                }
            }
            // 最後の半周期
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
            return OUT;
        }

        private void showStatus()
        {
            Console.WriteLine("*** COUNTER STATUS ***");
            Console.WriteLine("CM:{0}\tRWM:{1}\tOUT:{2}\tNC:{3}\tGATE:{4}", CM, RWM, OUT, NC, GATE);
            Console.WriteLine("COUNT VALUE...{0}", COUNT);
            Console.WriteLine("COUNT REGISTER...H：{0}\tL:{1}", cntReg[1], cntReg[0]);
            Console.WriteLine("COUNT LATCH...{0}", cntLatch);
        }
    }
}
