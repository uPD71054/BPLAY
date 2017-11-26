using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SlimDX.Multimedia;
using SlimDX.XAudio2;

namespace slimDXTestConsole
{
    class Program
    {
        static int cc = 0;
        static char[] bars = { '／', '―', '＼', '｜' };

        static void Main(string[] args)
        {    
            Console.WriteLine("beep ver0.3.0");
            fAudio beep = new fAudio();
            beep.bufferEndProc = callback;
            beep.Start();
            do
            {
                // process
            } while (Console.ReadKey(true).Key != ConsoleKey.Escape);
            Console.WriteLine();
            beep.Stop();
            beep.Dispose();
            
        }

        static private void callback()
        {
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write("{0}Press ESC to exit...", bars[cc++ % 4]);
        }
    }


    public class Square
    {
        private const int TABLE_LENGTH = 10;
        private const int mask = (1 << TABLE_LENGTH) - 1;
        private const int decimalPlaces = sizeof(int) * 8 - TABLE_LENGTH;

        private int samplingRate;
        private uint phase;         // 10bit:22bit fixed decimal point
        private uint delta;
        private byte[] buffer;
        private float[][] table;
        private double freq;
        private double[] noteFreq;
        private int note;

        public Square()
        {
            phase = 0;
            samplingRate = 44100;
            buffer = new byte[sizeof(float)];

            table = new float[128][];
            noteFreq = new double[128];
            for (int i = 0; i < table.Length; i++)
            {
                noteFreq[i] = 440.0 * Math.Pow(2.0, (i - 69) / 12.0);
                table[i] = mktable(noteFreq[i]);
            }
        }

        public double Frequency
        {
            get { return freq; }
            set
            {
                freq = value;
                delta = (uint)(freq / 44100 * (1 << TABLE_LENGTH) * (1 << decimalPlaces));
            }
        }

        public int Note
        {
            get { return note; }
            set
            {
                note = value;
                delta = (uint)(noteFreq[note] / 44100 * (1 << TABLE_LENGTH) * (1 << decimalPlaces));
            }
        }



        public byte[] getByte()
        {
            linearInterp();
            phase += delta;
            return buffer;
        }

        private float[] mktable(double frequency)
        {
            float[] ret = new float[1 << TABLE_LENGTH];
            // array initialize
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = 0;
            }
            // additive synthesis
            for (int n = 1; n < (samplingRate / 2) / frequency; n += 2)
            {
                for (int i = 0; i < ret.Length; i++)
                {
                    ret[i] += (float)Math.Sin(2.0d * Math.PI * n * i / ret.Length) / n;
                }
            }
            // normalize
            float max = ret.Max();
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = ret[i] / max;
            }
            return ret;
        }

        private float dx, dy;
        private void linearInterp()
        {
            dx = (float)(phase & 0xFFFF) / (1 << decimalPlaces);
            dy = table[note][((phase >> decimalPlaces) + 1) & mask] - table[note][phase >> decimalPlaces];
            buffer = BitConverter.GetBytes(table[note][phase >> decimalPlaces] + dx * dy);
        }

    }

    public class Sin
    {
        private const int TABLE_LENGTH = 10;
        private const int mask = (1 << TABLE_LENGTH) - 1;
        private const int decimalPlaces = sizeof(int) * 8 - TABLE_LENGTH;

        private uint phase;         // 10bit:22bit fixed decimal point
        private uint delta;         
        private byte[] buffer;
        private float[] table;
        private double freq;
        

        public Sin()
        {
            phase = 0;
            table = new float[1 << TABLE_LENGTH];
            buffer = new byte[sizeof(float)];
            for (int i = 0; i < table.Length; i++)
            {
                table[i] = (float)Math.Sin(2.0d * Math.PI * i / table.Length);
            }
        }

        public double Frequency
        {
            get { return freq; }
            set
            {
                freq = value;
                delta = (uint)(freq / 44100 * table.Length * (1 << decimalPlaces));
            }
        }

        public byte[] getByte()
        {
            linearInterp();
            phase += delta;
            return buffer;         
        }


        private float dx, dy;
        private void linearInterp()
        {
            dx = (float)(phase & 0xFFFF) / (1 << decimalPlaces);
            dy = table[((phase >> decimalPlaces) + 1) & mask] - table[phase >> decimalPlaces];
            buffer = BitConverter.GetBytes(table[phase >> decimalPlaces] + dx * dy);
        }

    }

    public class fAudio : IDisposable
    {
        private const int SAMPLERATE = 44100 /* kHz */;
        private const int BITDEPTH = 32/* 8bit/sample */;
        private const int CHANNELS = 1 /* monoral */;
        private const int NUMOFBUF = 2;
        private const int NUMOFSAMPLE = 882 * 2;

        internal XAudio2 device;
        internal MasteringVoice masteringVoice;
        internal WaveFormat waveFormat;
        internal SourceVoice sourceVoice;
        internal AudioBuffer[] audioBuffer;
        internal byte[][] data;
        internal int bufferCount;

        private Sin s = new Sin();
        private Square sq = new Square();
        public delegate void audioCallback();
        public audioCallback bufferEndProc;



        public fAudio()
        {
            device = new XAudio2();
            masteringVoice = new MasteringVoice(device);

            waveFormat = new WaveFormat();
            {
                waveFormat.Channels = CHANNELS;
                waveFormat.SamplesPerSecond = SAMPLERATE;
                waveFormat.BitsPerSample = BITDEPTH;
                waveFormat.BlockAlignment = (short)(waveFormat.BitsPerSample / 8 * waveFormat.Channels);
                waveFormat.AverageBytesPerSecond = waveFormat.SamplesPerSecond * waveFormat.BlockAlignment;
                waveFormat.FormatTag = WaveFormatTag.IeeeFloat;
            }

            sourceVoice = new SourceVoice(device, waveFormat);
            sourceVoice.BufferEnd += sourceVoice_BufferEnd;

            audioBuffer = new AudioBuffer[NUMOFBUF];
            data = new byte[NUMOFBUF][];
            for (int i = 0; i < NUMOFBUF; i++)
            {
                data[i] = new byte[NUMOFSAMPLE * waveFormat.BlockAlignment];
                byte[] buff;
                sq.Note = (95 - 12 * i);
                for (int j = 0; j < data[i].Length; j += waveFormat.BlockAlignment)
                {
                    buff = sq.getByte();
                    data[i][j+0] = buff[0];
                    data[i][j+1] = buff[1];
                    data[i][j+2] = buff[2];
                    data[i][j+3] = buff[3];
                }
                audioBuffer[i] = new AudioBuffer();
                audioBuffer[i].AudioData = new MemoryStream(data[i], true);
                audioBuffer[i].Flags = BufferFlags.EndOfStream;
                audioBuffer[i].AudioBytes = data[i].Length;
                audioBuffer[i].LoopCount = 0;

                audioBuffer[i].AudioData.Position = 0;
                sourceVoice.SubmitSourceBuffer(audioBuffer[i]);
            }
            bufferCount = 0;
        }

        public void Start()
        {
            sourceVoice.Start();
        }

        public void Stop()
        {
            sourceVoice.Stop();
        }

        void sourceVoice_BufferEnd(object sender, ContextEventArgs e)
        {
            for (int i = 0; i < data[bufferCount].Length; i++)
            {
                data[bufferCount][i] = 0;
            }

            audioBuffer[bufferCount].AudioData.Position = 0;
            sourceVoice.SubmitSourceBuffer(audioBuffer[bufferCount]);
            bufferCount = ++bufferCount & 0x01;

            bufferEndProc();
        }


        // pulse generator
        internal double phase = 0.0;
        internal float pulse(double helz)
        {
            phase += helz / SAMPLERATE;
            phase -= Math.Floor(phase);
            if (phase > 0.5)
            {
                return 0.5f;
            }
            else
            {
                return 0.0f;
            }
        }

        #region IDisposable Support
        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    for (int i = 0; i < audioBuffer.Length; i++)
                    {
                        audioBuffer[i].Dispose();
                    }
                    sourceVoice.Dispose();

                    masteringVoice.Dispose();
                    device.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }

    class iAudio :IDisposable
    {
        private const int SAMPLERATE = 44100 /* kHz */;
        private const int BITDEPTH = 16/* 8bit/sample */;
        private const int CHANNELS = 1 /* monoral */;
        private const int NUMOFBUF = 2;
        private const int NUMOFSAMPLE = 882;

        internal XAudio2 device;
        internal MasteringVoice masteringVoice;
        internal WaveFormat waveFormat;
        internal SourceVoice sourceVoice;
        internal AudioBuffer[] audioBuffer;
        internal byte[][] data;
        internal int bufferCount;

        public delegate void audioCallback();
        public audioCallback bufferEndProc;



        public iAudio()
        {
            device = new XAudio2();
            masteringVoice = new MasteringVoice(device);

            waveFormat = new WaveFormat();
            {
                waveFormat.Channels = CHANNELS;
                waveFormat.SamplesPerSecond = SAMPLERATE;
                waveFormat.BitsPerSample = BITDEPTH;
                waveFormat.BlockAlignment = (short)(waveFormat.BitsPerSample / 8 * waveFormat.Channels);
                waveFormat.AverageBytesPerSecond = waveFormat.SamplesPerSecond * waveFormat.BlockAlignment;
                waveFormat.FormatTag = WaveFormatTag.Pcm;
            }

            sourceVoice = new SourceVoice(device, waveFormat);
            sourceVoice.BufferEnd += sourceVoice_BufferEnd;

            audioBuffer = new AudioBuffer[NUMOFBUF];
            data = new byte[NUMOFBUF][];
            for (int i = 0; i < NUMOFBUF; i++)
            {
                data[i] = new byte[NUMOFSAMPLE * waveFormat.BlockAlignment];
                for (int j = 0; j < data[i].Length; j += waveFormat.BlockAlignment)
                {
                    switch (waveFormat.Channels | waveFormat.BitsPerSample)
                    {
                        case 9:
                            data[i][j] = pulse(2000 - 1000 * i);
                            break;
                        case 10:
                            data[i][j + 0] = data[i][j + 1] = pulse(2000 - 1000 * i);
                            break;
                        case 17:
                            data[i][j] = 0x00;
                            data[i][j + 1] = pulse(2000 - 1000 * i);
                            break;
                        case 18:
                            data[i][j + 0] = data[i][j + 2] = 0x00;
                            data[i][j + 1] = data[i][j + 3] = pulse(2000 - 1000 * i); ;
                            break;
                        default:
                            break;
                    }
                }


                audioBuffer[i] = new AudioBuffer();
                audioBuffer[i].AudioData = new MemoryStream(data[i], true);
                audioBuffer[i].Flags = BufferFlags.EndOfStream;
                audioBuffer[i].AudioBytes = data[i].Length;
                audioBuffer[i].LoopCount = 0;

                audioBuffer[i].AudioData.Position = 0;
                sourceVoice.SubmitSourceBuffer(audioBuffer[i]);
            }
            bufferCount = 0;
        }

        public void Start()
        {
            sourceVoice.Start();
        }

        public void Stop()
        {
            sourceVoice.Stop();
        }

        void sourceVoice_BufferEnd(object sender, ContextEventArgs e)
        {
            for (int i = 0; i < data[bufferCount].Length; i++)
            {
                data[bufferCount][i] = 0;
            }

            audioBuffer[bufferCount].AudioData.Position = 0;
            sourceVoice.SubmitSourceBuffer(audioBuffer[bufferCount]);
            bufferCount = ++bufferCount & 0x01;

            bufferEndProc();
        }


        // pulse generator
        internal double phase = 0.0;
        internal byte pulse(double helz)
        {
            phase += helz / SAMPLERATE;
            phase -= Math.Floor(phase);
            if (phase > 0.5)
            {
                return Byte.MaxValue >> 1;
            }
            else
            {
                return 0;
            }
        }

        #region IDisposable Support
        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    for (int i = 0; i < audioBuffer.Length; i++)
                    {
                        audioBuffer[i].Dispose();
                    }
                    sourceVoice.Dispose();

                    masteringVoice.Dispose();
                    device.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
