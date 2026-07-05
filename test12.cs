using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ClientScout.Infrastructure.Persistence;

class Program {
    static async Task Main() {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite("Data Source=d:\\Projects\\ClientScout\\data\\app.db");
        using var db = new AppDbContext(optionsBuilder.Options);
        
        var source = await db.Sources.FirstOrDefaultAsync(s => s.Name.Contains("Read"));
        if (source != null) {
            Console.WriteLine("Source: " + source.Name + " Url: " + source.Url + " Marker: " + source.LastMessageMarker);
        } else {
            Console.WriteLine("Source not found");
        }
    }
}
