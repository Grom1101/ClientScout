using System;
using System.Linq;
using System.Reflection;

class Program {
    static void Main() {
        var path = Environment.GetEnvironmentVariable("USERPROFILE") + @"\.nuget\packages\wtelegramclient\4.4.6\lib\net8.0\WTelegramClient.dll";
        var assembly = Assembly.LoadFrom(path);
        var type = assembly.GetTypes().FirstOrDefault(t => t.Name == "Client");
        var methods = type.GetMethods().Where(x => x.Name.Contains("Topic") || x.Name.Contains("Forum")).Select(x => x.Name).Distinct();
        foreach(var x in methods) Console.WriteLine(x);
    }
}
