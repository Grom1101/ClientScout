using System;
using System.Linq;
using System.Reflection;

class Program {
    static void Main() {
        var path = Environment.GetEnvironmentVariable("USERPROFILE") + @"\.nuget\packages\wtelegramclient\4.4.6\lib\net8.0\WTelegramClient.dll";
        var assembly = Assembly.LoadFrom(path);
        var type = assembly.GetTypes().FirstOrDefault(t => t.Name == "Client");
        var m = type.GetMethods().FirstOrDefault(x => x.Name.Contains("ForumTopic"));
        if (m != null) Console.WriteLine(m.Name);
        var methods = type.GetMethods().Where(x => x.Name.Contains("ForumTopic")).Select(x => x.Name);
        foreach(var x in methods) Console.WriteLine(x);
    }
}
