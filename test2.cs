using System;
using System.Linq;
using System.Reflection;

class Program {
    static void Main() {
        // Find DLL in NuGet package cache
        var path = Environment.GetEnvironmentVariable("USERPROFILE") + @"\.nuget\packages\wtelegramclient\4.4.6\lib\net8.0\WTelegramClient.dll;
        var assembly = Assembly.LoadFrom(path);
        var types = assembly.GetTypes().Where(t => t.Namespace == "TL" && t.Name.Contains("Filter"));
        foreach (var t in types) {
            Console.WriteLine(t.Name);
        }
    }
}
