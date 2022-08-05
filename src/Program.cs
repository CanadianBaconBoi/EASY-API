using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Threading;

namespace YoutubeAPI
{
    public static class Program
    {
        [Serializable]
        private class Config
        {
            [JsonProperty("YoutubeAPIKey")]
            public String YoutubeAPIKey;
            [JsonProperty("APIKeyLength")]
            public int? APIKeyLength;
            [JsonProperty("APIKeys")]
            public String[] APIKeys;
            [JsonProperty("Endpoints")]
            public String[] Endpoints;
        }

        public static string YoutubeAPIKey { get; private set; }

        private static Thread refreshCacheThread;
        private static readonly TimeSpan cacheLifeTime = TimeSpan.FromHours(1);

        public static Dictionary<string, CacheItem> mp3Cache = new Dictionary<string, CacheItem>();

        public static List<string> Keys { get; private set; }

        public static String[] Endpoints { get; private set; }

        public static void Main(string[] args)
        {
            if (File.Exists("config.json"))
            {
                Config _config = null;
                try
                {
                    _config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"), new JsonSerializerSettings() { MissingMemberHandling = MissingMemberHandling.Error });
                }
                catch (JsonSerializationException)
                {
                    File.Move("config.json", "config.json.old", true);
                    File.WriteAllText("config.json", JsonConvert.SerializeObject(new Config() { YoutubeAPIKey = "", APIKeyLength = 32, APIKeys = new string[] { }, Endpoints = new string[] { } }, Formatting.Indented));
                    Logger.Log(Logger.LogLevel.ERROR, new string[]
                    {
                        "Config file invalid, writing one... ",
                        "Stopping Server, please fill out the config."
                    });
                    return;
                }

                if(_config is Config config)
                {
                    if(config.YoutubeAPIKey == null || config.YoutubeAPIKey == String.Empty)
                    {
                        Logger.Log(Logger.LogLevel.ERROR, new string[]
                        {
                            "Youtube API Key not present ",
                            "Stopping Server, please fill out the config."
                        });
                        return;
                    } else
                    {
                        YoutubeAPIKey = config.YoutubeAPIKey;
                    }
                    
                    if(config.APIKeys == null || config.APIKeys.Length == 0)
                    {
                        Logger.Log(Logger.LogLevel.WARN, "No API Keys present, running in unsecured mode.");
                    } else if (config.APIKeyLength == null || config.APIKeyLength == 0)
                    {
                        Logger.Log(Logger.LogLevel.WARN, "No API Key Length set, running in unsecured mode.");
                    } else
                    {
                        Keys = new(config.APIKeys);
                        if (config.APIKeyLength != -1)
                            foreach (var key in Keys.ToArray()) // ToArray creates a copy
                            {
                                if(key.Length != config.APIKeyLength)
                                {
                                    Logger.Log(Logger.LogLevel.ERROR, $"API Key {key} is not of size {config.APIKeyLength}. Discarding.");
                                    Keys.RemoveAll(x => x == key);
                                }
                            }
                        if(Keys.Count == 0)
                        {
                            Logger.Log(Logger.LogLevel.ERROR, $"All API Keys discarded, exiting!!!");
                            return;
                        }
                        if (Keys.Count > 1)
                            Logger.Log(Logger.LogLevel.INFO, $"Loaded {Keys.Count} API Keys");
                        else
                            Logger.Log(Logger.LogLevel.INFO, $"Loaded {Keys.Count} API Key");
                    }
                    if (config.Endpoints == null || config.Endpoints.Length == 0)
                    {
                        Logger.Log(Logger.LogLevel.WARN, "No Endpoints configured, running on default.");
                        Endpoints = new[] { "http://0.0.0.0:8080" };
                    }
                    else
                    {
                        Endpoints = config.Endpoints;
                    }
                } else
                {
                    File.Move("config.json", "config.json.old", true);
                    File.WriteAllText("config.json", JsonConvert.SerializeObject(new Config() { YoutubeAPIKey = "", APIKeyLength = 32, APIKeys = new string[] { }, Endpoints = new string[] { } }, Formatting.Indented));
                    Logger.Log(Logger.LogLevel.ERROR, new string[]
                    {
                        "Config file invalid, writing one... ",
                        "Stopping Server, please fill out the config."
                    });
                    return;
                }
            }
            else
            {
                File.WriteAllText("config.json", JsonConvert.SerializeObject(new Config() { YoutubeAPIKey = "", APIKeyLength = 32, APIKeys = new string[] { }, Endpoints = new string[] { } }, Formatting.Indented));
                Logger.Log(Logger.LogLevel.ERROR, new string[]
                {
                    "Config file not present, writing one... ",
                    "Stopping Server, please fill out the config."
                });
                return;
            }

            refreshCacheThread = new Thread(refreshCache);
            refreshCacheThread.Start();

            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost
                .SuppressStatusMessages(true)
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                })
                .UseKestrel(serverOptions =>
                {
                    serverOptions.Limits.MaxConcurrentConnections = 10000;
                    serverOptions.Limits.MaxConcurrentUpgradedConnections = 10000;
                    serverOptions.Limits.Http2.MaxStreamsPerConnection = 2;
                    serverOptions.AllowSynchronousIO = false;
                });


            builder.Services.AddCarter();

            var app = builder.Build();
            foreach(String e in Endpoints)
            {
                app.Urls.Add(e);
            }

            app.MapCarter();

            Logger.Log(Logger.LogLevel.INFO, "Starting Server at {col:13}" + String.Join(", ", app.Urls) + "{col:15}.");

            Console.CancelKeyPress += delegate
            {
                Logger.Log(Logger.LogLevel.ERROR, "\n\n\n\nReceived ^C, shutting down.");
                Environment.Exit(0);
            };

            app.Run();
        }

        private static void refreshCache()
        {
            while (true)
            {
                Thread.Sleep(300_000);
                List<string> toDelete = new List<string>();

                lock (mp3Cache)
                {
                    foreach (KeyValuePair<string, CacheItem> pair in mp3Cache)
                        if (DateTime.Now - pair.Value.CacheTime > cacheLifeTime)
                            toDelete.Add(pair.Key);

                    foreach (string item in toDelete)
                    {
                        mp3Cache.Remove(item);
                    }
                }
            }
        }
    }
}