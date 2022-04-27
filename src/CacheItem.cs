using CSCore.Ffmpeg;
using NAudio.Lame;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YoutubeAPI
{
    public class CacheItem
    {
        public AsyncCacheStream Stream { get; }
        public DateTime CacheTime;
        public VideoInfo VideoInfo { get; }

        public CacheItem(AsyncCacheStream stream, DateTime cacheTime, VideoInfo videoInfo)
        {
            this.Stream = stream;
            this.CacheTime = cacheTime;
            this.VideoInfo = videoInfo;
        }
        public CacheItem(AsyncCacheStream stream, VideoInfo videoInfo) : this(stream, DateTime.Now, videoInfo) {}

        public Thread StartEncodeThread(Stream videoStream)
        {
            Thread t = new Thread((t) =>
            {
                try
                {
                    Stream.StartWriting();
                    using (FfmpegDecoder decoder = new FfmpegDecoder(videoStream))
                    {
                        using (var encoder = new LameMP3FileWriter(this.Stream,
                            NAudio.Wave.WaveFormat.CreateCustomFormat(
                                WaveFormatEncoding.IeeeFloat, decoder.WaveFormat.SampleRate, decoder.WaveFormat.Channels, decoder.WaveFormat.BytesPerSecond, decoder.WaveFormat.BlockAlign, decoder.WaveFormat.BitsPerSample), HomeModule.MP3_BITRATE))
                        {
                            byte[] buf = new byte[decoder.WaveFormat.BytesPerSecond * 16];
                            int count;
                            while ((count = decoder.Read(buf, 0, buf.Length)) > 0)
                            {
                                encoder.Write(buf, 0, count);
                                Thread.Sleep(150);
                            }
                        }
                    }
                    Stream.FinishWriting();
                }
                catch
                {
                    Console.WriteLine("Error occured while reading youtube stream.");
                }
            });
            t.Start();
            return t;
        }
    }
}
