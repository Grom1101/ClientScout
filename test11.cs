using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

class Program {
    static void Main() {
        var path = Environment.GetEnvironmentVariable("USERPROFILE") + @"\.nuget\packages\wtelegramclient\4.4.6\lib\net8.0\WTelegramClient.dll";
        var assembly = Assembly.LoadFrom(path);
        
        var extMethods = assembly.GetTypes()
            .Where(t => t.IsSealed && !t.IsGenericType && !t.IsNested)
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(m => m.IsDefined(typeof(ExtensionAttribute), false));
            
        var getReplies = extMethods.FirstOrDefault(m => m.Name == "Messages_GetReplies");
        if (getReplies != null) {
            Console.WriteLine(getReplies.Name + "(" + string.Join(", ", getReplies.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
        } else {
            Console.WriteLine("Not found");
        }
    }
}
