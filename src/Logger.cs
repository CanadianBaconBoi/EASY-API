using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YoutubeAPI
{
    internal static class Logger
    {
        private static Dictionary<int, List<(char[] tag, Action<String, StreamWriter> parse)>> messageTags = new() {
            {3, new() {
            (new char[]{'c', 'o', 'l'}, (tag_data, sw) =>
                    {
                        if(int.TryParse(tag_data, out int colorCode))
                            if(colorCode < 16) {
                                sw.Flush();
                                Console.ForegroundColor = (ConsoleColor)colorCode;
                            }
                    }
                )
            } }
        };

        public static string log_format = "[{dt}] {0:msg}";

        public static Dictionary<LogLevel, ConsoleColor> LogColors = new()
        {
            { LogLevel.TRACE, ConsoleColor.DarkGray },
            { LogLevel.DEBUG, ConsoleColor.Gray },
            { LogLevel.INFO,  ConsoleColor.White},
            { LogLevel.WARN,  ConsoleColor.Yellow },
            { LogLevel.ERROR, ConsoleColor.Red },
            { LogLevel.FATAL, ConsoleColor.Magenta }
        };
        private static object _lock = new();
        public static void Log(LogLevel level, string message)
        {
            lock (_lock)
            {
                ConsoleColor currentForeground = Console.ForegroundColor;
                Console.ForegroundColor = LogColors[level];
                bool tagMode = false;
                char[] tagBuffer = new char[32];
                int size = 0;
                using (Stream stdout_str = Console.OpenStandardOutput())
                using (StreamWriter stdout = new(stdout_str))
                {
                    for (int i = 0; i < message.Length; i++)
                    {
                        char c = message[i];
                        if (tagMode)
                        {
                            while (message[i] != '}' && size < 32)
                            {
                                tagBuffer[size] = message[i];
                                i++;
                                size++;
                            }
                            foreach (var tagList in messageTags)
                            {
                                foreach (var tag in tagList.Value)
                                {
                                    if (tagBuffer[0..tagList.Key].SequenceEqual(tag.tag))
                                        tag.parse(new string(tagBuffer.Take((tagList.Key + 1)..size).ToArray()), stdout);
                                }
                            }
                            size = 0;
                            tagMode = false;
                        }
                        else
                        {
                            if (c == 123)
                                tagMode = true;
                            else
                                stdout.Write(c);
                        }
                    }
                    stdout.Write('\n');
                    stdout.Flush();
                }
                Console.ForegroundColor = currentForeground;
            }
        }
        public static void Log(LogLevel level, string[] messages)
        {
            lock (_lock)
            {
                ConsoleColor currentForeground = Console.ForegroundColor;
                Console.ForegroundColor = LogColors[level];
                bool tagMode = false;
                char[] tagBuffer = new char[32];
                int size = 0;
                using (Stream stdout_str = Console.OpenStandardOutput())
                using (StreamWriter stdout = new(stdout_str))
                {
                    foreach (string message in messages)
                    {
                        foreach (char c in message)
                        {
                            if (tagMode)
                            {
                                while (c != '}' && size < 32)
                                {
                                    tagBuffer[size] = c;
                                    size++;
                                }
                                foreach (var tagList in messageTags)
                                {
                                    foreach (var tag in tagList.Value)
                                    {
                                        if (tagBuffer[0..tagList.Key].SequenceEqual(tag.tag))
                                            tag.parse(new string(tagBuffer.Take((tagList.Key + 1)..size).ToArray()), stdout);
                                    }
                                }
                                size = 0;
                                tagMode = false;
                            }
                            else
                            {
                                if (c == 123)
                                    tagMode = true;
                                else
                                    stdout.Write(c);
                            }
                        }
                        stdout.Write('\n');
                        stdout.Flush();
                    }
                }
                Console.ForegroundColor = currentForeground;
            }
        }
        internal enum LogLevel
        {
            TRACE, DEBUG, INFO, WARN, ERROR, FATAL
        }
    }
}
