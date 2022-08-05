using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace YoutubeAPI
{
    public class HomeModule : ICarterModule
    {
        public const int MP3_BITRATE = 128000;

        private static byte[] unauthorizedText = Encoding.UTF8.GetBytes("<h2>Unauthorized.</h2> Invalid or missing key.");
        private static byte[] badRequestText = Encoding.UTF8.GetBytes("<h2>Bad Request.</h2> Missing video ID.");
        private static byte[] badRequestLongText = Encoding.UTF8.GetBytes("<h2>Bad Request.</h2> Video too long, max 1 hr.");
        private static byte[] notFoundText = Encoding.UTF8.GetBytes("<h2>Not Found.</h2> Video does not exist.");

        private static YouTube youtube = YouTube.Default;

        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/info/", async (r) =>
            {
                var q = r.Request.Query;
                if (Program.Keys == null || q.ContainsKey("key") && Program.Keys.Contains(q["key"][0]))
                {
                    if (q.ContainsKey("id"))
                    {
                        HttpResponseMessage response = await GetAsync($"https://www.googleapis.com/youtube/v3/videos?part=contentDetails,snippet&id={q["id"][0]}&key={Program.YoutubeAPIKey}");

                        JsonReader reader = new JsonTextReader(new StringReader(await response.Content.ReadAsStringAsync()));
                        string title = String.Empty;
                        TimeSpan duration = TimeSpan.Zero;
                        while (await reader.ReadAsync())
                        {
                            if (reader.TokenType == JsonToken.PropertyName)
                                switch (reader.Value)
                                {
                                    case "title":
                                        title = await reader.ReadAsStringAsync();
                                        break;

                                    case "duration":
                                        duration = XmlConvert.ToTimeSpan(await reader.ReadAsStringAsync());
                                        break;

                                    case "totalResults":
                                        if ((int)await reader.ReadAsInt32Async() == 0)
                                        {
                                            r.Response.StatusCode = 404;
                                            r.Response.ContentType = "text/html";
                                            await r.Response.StartAsync();
                                            await r.Response.BodyWriter.WriteAsync(notFoundText);
                                            await r.Response.CompleteAsync();
                                            reader.Close();
                                            return;
                                        }
                                        break;
                                }
                        }
                        reader.Close();

                        
                        if (q.ContainsKey("format"))
                        {
                            switch (q["format"][0])
                            {
                                case "e2":
                                    r.Response.StatusCode = 200;
                                    r.Response.ContentType = "text/plain";
                                    await r.Response.StartAsync();
                                    await r.Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes($"http://{r.Request.Host}/audio/?key={q["key"][0]}&id={q["id"][0]}:::{title}:::{duration.TotalSeconds}<"));
                                    await r.Response.CompleteAsync();
                                    break;

                                case "json":
                                default:
                                    r.Response.StatusCode = 200;
                                    r.Response.ContentType = "application/json";
                                    await r.Response.StartAsync();
                                    await r.Response.BodyWriter.WriteAsync(
                                        Encoding.UTF8.GetBytes(
                                            "{" +
                                                $"\"name\": \"{title}\"," +
                                                $"\"duration\": {duration.TotalSeconds}," +
                                                $"\"link\": \"http://{r.Request.Host}/audio/?key={q["key"][0]}&id={q["id"][0]}\"" +
                                            "}")
                                    );
                                    await r.Response.CompleteAsync();
                                    break;
                            }
                        }
                        else
                        {
                            r.Response.StatusCode = 200;
                            r.Response.ContentType = "application/json";
                            await r.Response.StartAsync();
                            await r.Response.BodyWriter.WriteAsync(
                                Encoding.UTF8.GetBytes(
                                    "{" +
                                        $"\"name\": \"{title}\"," +
                                        $"\"duration\": {duration.TotalSeconds}," +
                                        $"\"link\": \"http://{r.Request.Host}/audio/?key={q["key"][0]}&id={q["id"][0]}\"" +
                                    "}")
                            );
                            await r.Response.CompleteAsync();
                        }
                    }
                    else
                    {
                        r.Response.StatusCode = 400;
                        r.Response.ContentType = "text/html";
                        await r.Response.StartAsync();
                        await r.Response.BodyWriter.WriteAsync(badRequestText);
                        await r.Response.CompleteAsync();
                        return;
                    }
                }
                else
                {
                    r.Response.StatusCode = 403;
                    r.Response.ContentType = "text/html";
                    await r.Response.StartAsync();
                    await r.Response.BodyWriter.WriteAsync(unauthorizedText);
                    await r.Response.CompleteAsync();
                    return;
                }
            });

            bool requesting = false;
            bool firstRequestProcessed = false;
            app.MapGet("/audio/", async (r) =>
            {
                var q = r.Request.Query;
                if (Program.Keys == null || q.ContainsKey("key") && Program.Keys.Contains(q["key"][0]))
                {
                    if (q.ContainsKey("id"))
                    {
                        while (requesting)
                        {
                            await Task.Delay(25);
                        }
                        if (!firstRequestProcessed)
                        {
                            requesting = true;
#pragma warning disable CS4014
                            Task.Delay(5000).ContinueWith(_ => firstRequestProcessed = false);
#pragma warning restore CS4014
                        }
                        CacheItem cacheItem;
                        if (Program.mp3Cache.TryGetValue(q["id"][0], out cacheItem))
                        {
                            firstRequestProcessed = true;
                            requesting = false;

                            cacheItem.CacheTime = DateTime.Now;

                            r.Response.StatusCode = 200;
                            r.Response.ContentType = "audio/mpeg";

                            Logger.Log(Logger.LogLevel.INFO, "New Song Request From {col:10}" + r.Connection.RemoteIpAddress + "{col:15}. \n\tSending cached content");

                            await r.Response.StartAsync();
                            Stream stream = cacheItem.Stream.CreateReader();
                            Stream bodyWriter = r.Response.BodyWriter.AsStream();

                            byte[] buf = new byte[65536];
                            int count;

                            while ((count = await stream.ReadAsync(buf, 0, buf.Length)) > 0 || cacheItem.Stream.isWriting)
                            {
                                await bodyWriter.WriteAsync(buf, 0, count);
                                await Task.Delay(500);
                            }

                            await r.Response.CompleteAsync();
                            return;
                        }
                        else
                        {
                            HttpResponseMessage response = await GetAsync($"https://www.youtube.com/oembed?format=json&url=http://www.youtube.com/watch?v={q["id"][0]}");
                            if (response.StatusCode != HttpStatusCode.BadRequest)
                            {
                                IEnumerable<YouTubeVideo> videos = await youtube.GetAllVideosAsync($"https://youtube.com/watch?v={q["id"][0]}");
                                YouTubeVideo video = null;
                                foreach (YouTubeVideo v in videos)
                                {
                                    if (v.AdaptiveKind == AdaptiveKind.Audio && v.AudioFormat == AudioFormat.Aac)
                                    {
                                        video = v;
                                        break;
                                    }
                                }
                                if (video.Info.LengthSeconds > 3600)
                                {
                                    r.Response.StatusCode = 400;
                                    r.Response.ContentType = "text/html";
                                    await r.Response.StartAsync();
                                    await r.Response.BodyWriter.WriteAsync(badRequestLongText);
                                    await r.Response.CompleteAsync();
                                    return;
                                }

                                cacheItem = new(new AsyncCacheStream(), DateTime.Now, video.Info);
                                Program.mp3Cache.Add(q["id"][0], cacheItem);
                                Thread encodeThread = cacheItem.StartEncodeThread(video.Stream());
                                firstRequestProcessed = true;
                                requesting = false;

                                r.Response.StatusCode = 200;
                                r.Response.ContentType = "audio/mpeg";

                                Logger.Log(Logger.LogLevel.INFO, "New Song Request From {col:10}"+r.Connection.RemoteIpAddress+ "{col:15}. \n\tSending new content");
                                await r.Response.StartAsync();
                                Stream stream = cacheItem.Stream.CreateReader();
                                Stream bodyWriter = r.Response.BodyWriter.AsStream();

                                byte[] buf = new byte[65536];
                                int count;

                                while ((count = await stream.ReadAsync(buf, 0, buf.Length)) > 0 || cacheItem.Stream.isWriting)
                                {
                                    await bodyWriter.WriteAsync(buf, 0, count);
                                    await Task.Delay(500);
                                }
                                await r.Response.CompleteAsync();
                                return;
                            }
                            else
                            {
                                r.Response.StatusCode = 404;
                                r.Response.ContentType = "text/html";
                                await r.Response.StartAsync();
                                await r.Response.BodyWriter.WriteAsync(notFoundText);
                                await r.Response.CompleteAsync();
                                return;
                            }
                        }
                    }
                    else
                    {
                        r.Response.StatusCode = 400;
                        r.Response.ContentType = "text/html";
                        await r.Response.StartAsync();
                        await r.Response.BodyWriter.WriteAsync(badRequestText);
                        await r.Response.CompleteAsync();
                        return;
                    }
                }
                else
                {
                    r.Response.StatusCode = 403;
                    r.Response.ContentType = "text/html";
                    await r.Response.StartAsync();
                    await r.Response.BodyWriter.WriteAsync(unauthorizedText);
                    await r.Response.CompleteAsync();
                    return;
                }
            });
        }

        private static HttpClientHandler handler = new HttpClientHandler()
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        private HttpClient c = new HttpClient(handler);

        public Task<HttpResponseMessage> GetAsync(string uri) => c.GetAsync(uri);

    }
}