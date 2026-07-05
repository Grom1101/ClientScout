using System;
using System.Linq;
using System.Reflection;

class Program {
    static void Main() {
        var path = Environment.GetEnvironmentVariable("USERPROFILE") + @"\.nuget\packages\wtelegramclient\4.4.6\lib\net8.0\WTelegramClient.dll";
        var assembly = Assembly.LoadFrom(path);
        var clientType = assembly.GetTypes().FirstOrDefault(t => t.Name == "Client");
        foreach (var m in clientType.GetMethods().Where(m => m.Name == "Invoke" || m.Name == "Call" || m.Name == "Send" || m.Name == "SendAsync")) {
            Console.WriteLine(m.Name);
        }
    }
}
