using System;
using System.Linq;
using System.Reflection;

class Program {
    static void Main() {
        var path = Environment.GetEnvironmentVariable("USERPROFILE") + @"\.nuget\packages\wtelegramclient\4.4.6\lib\net8.0\WTelegramClient.dll";
        var assembly = Assembly.LoadFrom(path);
        var clientType = assembly.GetTypes().FirstOrDefault(t => t.Name == "Client");
        var methods = clientType.GetMethods().Where(m => m.Name.Contains("Messages_GetReplies"));
        foreach (var m in methods) {
            Console.WriteLine(m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
        }
    }
}
