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
        static void Main(string[] args)
        {
            Console.WriteLine("beep ver0.2.2");
            xaudio beep = new xaudio();
            Console.WriteLine("Press ESC to exit...");
            do
            {
                // process
            } while (Console.ReadKey(true).Key != ConsoleKey.Escape);
        }
    }

    class xaudio
    {
        // Const parametor
        private const int SAMPLERATE = 44100 /* kHz */;
        private const int BITDEPTH = 8 /* 8bit/sample */;
        private const int CHANNELS = 1 /* monoral */;
        private const int NUMOFBUF = 4;
        private const int NUMOFSAMPLE = 882 * 2;

        internal XAudio2 device;
        internal MasteringVoice masteringVoice;
        internal WaveFormat waveFormat;
        internal SourceVoice sourceVoice;
        internal AudioBuffer[] audioBuffer;
        internal byte[][] data;
        internal int bufferCount;
        internal byte[] b = new byte[NUMOFSAMPLE];
        public xaudio()
        {
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = pulse(880);
            }
            audioInit();

            sourceVoice.BufferEnd += sourceVoice_BufferEnd;
            //sourceVoice.StreamEnd += bufferEnd;
        }

        private void audioInit()
        {

            device = new XAudio2();
            masteringVoice = new MasteringVoice(device);

            waveFormat = new WaveFormat();
            waveFormat.Channels = CHANNELS;
            waveFormat.SamplesPerSecond = SAMPLERATE;
            waveFormat.BitsPerSample = BITDEPTH;
            waveFormat.BlockAlignment = BITDEPTH / 8 * CHANNELS;
            waveFormat.AverageBytesPerSecond = SAMPLERATE * waveFormat.BlockAlignment;
            waveFormat.FormatTag = WaveFormatTag.Pcm;

            sourceVoice = new SourceVoice(device, waveFormat);

            audioBuffer = new AudioBuffer[NUMOFBUF];
            data = new byte[NUMOFBUF][];
            for (int i = 0; i < NUMOFBUF; i++)
            {
                data[i] = new byte[NUMOFSAMPLE];
                audioBuffer[i] = new AudioBuffer();
                audioBuffer[i].AudioData = new MemoryStream(data[i], true);
                audioBuffer[i].Flags = BufferFlags.EndOfStream;
                audioBuffer[i].AudioBytes = NUMOFSAMPLE;
                audioBuffer[i].LoopCount = 0;
            }

            for (int j = 0; j < NUMOFSAMPLE; j++)
            {
                data[0][j] = pulse(2000);
            }
            audioBuffer[0].AudioData.Position = 0;
            sourceVoice.SubmitSourceBuffer(audioBuffer[0]);
            for (int j = 0; j < NUMOFSAMPLE; j++)
            {
                data[1][j] = pulse(1000);
            }
            audioBuffer[1].AudioData.Position = 0;
            sourceVoice.SubmitSourceBuffer(audioBuffer[1]);
            bufferCount = 0;

            sourceVoice.Start();
        }

        int callCount = 0;
        void sourceVoice_BufferEnd(object sender, ContextEventArgs e)
        {
            Console.WriteLine("called!");

            for (int i = 0; i < NUMOFSAMPLE; i++)
            {
                data[bufferCount][i] = 0;
            }
            callCount += 16;
            audioBuffer[bufferCount].AudioData.Position = 0;
            sourceVoice.SubmitSourceBuffer(audioBuffer[bufferCount]);
            bufferCount = ++bufferCount & 0x01;
            Console.WriteLine("buffer {0}", bufferCount);
        }


        // pulse generator
        internal double phase = 0.0;
        internal byte pulse(double helz)
        {
            phase += helz / SAMPLERATE;
            phase -= Math.Floor(phase);
            if (phase > 0.5)
            {
                return Byte.MaxValue >> 2;
            }
            else
            {
                return 0;
            }
        }

        internal void streamend()
        {
            Console.WriteLine("stream end!");
        }

    }
}
