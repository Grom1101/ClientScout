using System;
using Npgsql;
class Program {
    static void Main() {
        using var conn = new NpgsqlConnection("Host=localhost;Database=clientscout;Username=postgres;Password=postgres");
        conn.Open();
        using var cmd = new NpgsqlCommand("SELECT s.data FROM hangfire.state s JOIN hangfire.job j ON s.jobid = j.id WHERE j.invocationdata::text LIKE '%ExpandProfileBackgroundAsync%' AND s.name = 'Failed' ORDER BY s.id DESC LIMIT 1;", conn);
        using var reader = cmd.ExecuteReader();
        if (reader.Read()) {
            Console.WriteLine(reader.GetString(0));
        } else {
            Console.WriteLine("No failed states found.");
        }
    }
}
