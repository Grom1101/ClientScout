using System;
using System.Linq;
using System.Reflection;
using TL;

class Program {
    static void Main() {
        var path = Environment.GetEnvironmentVariable("USERPROFILE") + @"\.nuget\packages\wtelegramclient\4.4.6\lib\net8.0\WTelegramClient.dll";
        var assembly = Assembly.LoadFrom(path);
        var type = assembly.GetTypes().FirstOrDefault(t => t.Name == "Messages_MessagesBase");
        if (type == null) type = assembly.GetTypes().FirstOrDefault(t => t.Name == "messages_Messages");
        if (type != null) {
            foreach(var p in type.GetProperties()) Console.WriteLine(p.Name + " : " + p.PropertyType.Name);
            foreach(var f in type.GetFields()) Console.WriteLine(f.Name + " : " + f.FieldType.Name);
        }
    }
}
