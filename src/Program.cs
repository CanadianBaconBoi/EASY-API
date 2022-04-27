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
                Config? _config = null;
                try
                {
                    _config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"), new JsonSerializerSettings() { MissingMemberHandling = MissingMemberHandling.Error });
                }
                catch (JsonSerializationException e)
                {
                    File.Move("config.json", "config.json.old", true);
                    File.WriteAllText("config.json", JsonConvert.SerializeObject(new Config() { YoutubeAPIKey = "", APIKeys = new string[] { }, Endpoints = new string[] { } }, Formatting.Indented));
                    HomeModule.Log(() =>
                    {
                        ConsoleColor currentForeground = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[{DateTime.Now:dd/MM/yy HH:mm:ss}] Config file invalid, writing one... ");
                        Console.WriteLine($"[{DateTime.Now:dd/MM/yy HH:mm:ss}] Stopping Server, please fill out the config.");
                        Console.ForegroundColor = currentForeground;
                    });
                    return;
                }

                if(_config is Config config)
                {
                    if(config.YoutubeAPIKey == null || config.YoutubeAPIKey == String.Empty)
                    {
                        HomeModule.Log(() =>
                        {
                            ConsoleColor currentForeground = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[{DateTime.Now:dd/MM/yy HH:mm:ss}] Youtube API Key not present ");
                            Console.WriteLine($"[{DateTime.Now:dd/MM/yy HH:mm:ss}] Stopping Server, please fill out the config.");
                            Console.ForegroundColor = currentForeground;
                        });
                        return;
                    } else
                    {
                        YoutubeAPIKey = config.YoutubeAPIKey;
                    }
                    if(config.APIKeys == null || config.APIKeys.Length == 0)
                    {
                        HomeModule.Log(() =>
                        {
                            ConsoleColor currentForeground = Console.ForegroundColor;
                            Console.Write($"[{DateTime.Now:dd/MM/yy HH:mm:ss}]");
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($" No API Keys present, running in unsecured mode.");
                            Console.ForegroundColor = currentForeground;
                        });
                    } else
                    {
                        Keys = new(config.APIKeys);
                        HomeModule.Log(() =>
                        {
                            ConsoleColor currentForeground = Console.ForegroundColor;
                            Console.Write($"[{DateTime.Now:dd/MM/yy HH:mm:ss}]");
                            Console.ForegroundColor = ConsoleColor.Green;
                            if (Keys.Count > 1)
                                Console.WriteLine($" Loaded {Keys.Count} API Keys");
                            else
                                Console.WriteLine($" Loaded {Keys.Count} API Key");
                            Console.ForegroundColor = currentForeground;
                        });
                    }
                    if (config.Endpoints == null || config.Endpoints.Length == 0)
                    {
                        HomeModule.Log(() =>
                        {
                            ConsoleColor currentForeground = Console.ForegroundColor;
                            Console.Write($"[{DateTime.Now:dd/MM/yy HH:mm:ss}]");
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($" No Endpoints configured, running on default.");
                            Console.ForegroundColor = currentForeground;
                        });
                        Endpoints = new[] { "http://0.0.0.0:8080" };
                    }
                    else
                    {
                        Endpoints = config.Endpoints;
                    }
                } else
                {
                    File.Move("config.json", "config.json.old", true);
                    File.WriteAllText("config.json", JsonConvert.SerializeObject(new Config() { YoutubeAPIKey = "", APIKeys = new string[] { }, Endpoints = new string[] { } }, Formatting.Indented));
                    HomeModule.Log(() =>
                    {
                        ConsoleColor currentForeground = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[{DateTime.Now:dd/MM/yy HH:mm:ss}] Config file invalid, writing one... ");
                        Console.WriteLine($"[{DateTime.Now:dd/MM/yy HH:mm:ss}] Stopping Server, please fill out the config.");
                        Console.ForegroundColor = currentForeground;
                    });
                    return;
                }
            }
            else
            {
                File.WriteAllText("config.json", JsonConvert.SerializeObject(new Config() { YoutubeAPIKey = "", APIKeys = new string[] { }, Endpoints = new string[] { } }, Formatting.Indented));
                HomeModule.Log(() =>
                {
                    ConsoleColor currentForeground = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[{DateTime.Now:dd/MM/yy HH:mm:ss}] Config file not present, writing one... ");
                    Console.WriteLine($"[{DateTime.Now:dd/MM/yy HH:mm:ss}] Stopping Server, please fill out the config.");
                    Console.ForegroundColor = currentForeground;
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

            HomeModule.Log(() =>
            {
                ConsoleColor currentForeground = Console.ForegroundColor;
                Console.Write($"[{DateTime.Now:dd/MM/yy HH:mm:ss}]");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($" Starting Server at ");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write(String.Join(", ", app.Urls));
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(".");
                Console.ForegroundColor = currentForeground;
            });

            Console.CancelKeyPress += delegate
            {
                HomeModule.Log(() =>
                {
                    Console.WriteLine("");
                    Console.WriteLine("");
                    Console.WriteLine("");
                    ConsoleColor currentForeground = Console.ForegroundColor;
                    Console.Write($"[{DateTime.Now:dd/MM/yy HH:mm:ss}]");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($" Received ^C, shutting down.");
                    Console.ForegroundColor = currentForeground;
                });
                Environment.Exit(0);
            };

            app.Run();
        }

        private static void refreshCache()
        {
            while (true)
            {
                Thread.Sleep(300_000);
                Console.WriteLine("Cache cleanup");
                List<string> toDelete = new List<string>();

                lock (mp3Cache)
                {
                    foreach (KeyValuePair<string, CacheItem> pair in mp3Cache)
                        if (DateTime.Now - pair.Value.CacheTime > cacheLifeTime)
                            toDelete.Add(pair.Key);

                    foreach (string item in toDelete)
                    {
                        mp3Cache.Remove(item);
                        Console.WriteLine($"removing {item}");
                    }
                }
            }
        }
    }
}