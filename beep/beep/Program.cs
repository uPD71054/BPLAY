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
            Console.WriteLine("beep ver0.2.3");
            xaudio beep = new xaudio();
            beep.bufferEndProc = callback;
            Console.Write("Press ESC to exit...");
            do
            {
                // process
            } while (Console.ReadKey(true).Key != ConsoleKey.Escape);
            Console.WriteLine();
        }

        static private void callback()
        {
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write("{0}Press ESC to exit...", bars[cc++ % 4]);
        }
    }

    class xaudio
    {
        // Const parametor
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



        public xaudio()
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
            sourceVoice.Start();
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

    }
}
