using System;
using System.Linq;
using System.Reflection;

class Program {
    static void Main() {
        var assembly = Assembly.LoadFrom(@"d:\Projects\ClientScout\src\ClientScout.Api\bin\Release\net8.0\WTelegramClient.dll");
        var types = assembly.GetTypes().Where(t => t.Namespace == "TL" && t.Name.Contains("Filter"));
        foreach (var t in types) {
            Console.WriteLine(t.Name);
        }
    }
}
