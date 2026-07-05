using System;
using System.Linq;
using System.Reflection;

class Program {
    static void Main() {
        var path = Environment.GetEnvironmentVariable("USERPROFILE") + @"\.nuget\packages\wtelegramclient\4.4.6\lib\net8.0\WTelegramClient.dll";
        var assembly = Assembly.LoadFrom(path);
        var types = assembly.GetTypes().Where(t => t.Name == "Messages_Search" || t.Name == "Messages_GetHistory");
        foreach (var t in types) {
            Console.WriteLine(t.Name);
            foreach (var p in t.GetFields()) {
                Console.WriteLine("  " + p.Name + " (" + p.FieldType.Name + ")");
            }
        }
    }
}
