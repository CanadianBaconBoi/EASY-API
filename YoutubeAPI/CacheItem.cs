using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using System;
using System.Diagnostics;
using System.Threading;

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
        public Thread StartEncodeThread(Video video)
        {
            Thread t = new Thread(t =>
            {
                try
                {
                    Stream.StartWriting();
                    FFMpegArguments
                            .FromUrlInput(new Uri(video.Uri), options => options
                                .WithAudioCodec(AudioCodec.Aac))
                            .OutputToPipe(new StreamPipeSink(this.Stream), options => options
                                .WithCustomArgument("-f mp3")
                                .WithAudioBitrate(AudioQuality.Normal)
                                .WithAudioSamplingRate(48000)
                            )
                            .ProcessSynchronously();
                    Stream.FinishWriting();
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex);
                    Console.WriteLine("Error occured while reading youtube stream.");
                }
            });
            t.Start();
            return t;
        }
    }
}
