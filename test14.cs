using System;
using Microsoft.Data.Sqlite;
class P {
    static void Main() {
        using var c = new SqliteConnection("Data Source=d:/Projects/ClientScout/data/app.db");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Name, Url FROM Sources";
        using var r = cmd.ExecuteReader();
        while(r.Read()) Console.WriteLine($"{r[0]} | {r[1]}");
    }
}
