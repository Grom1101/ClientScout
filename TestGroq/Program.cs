using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;

class Program {
    static async Task Main() {
        var _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var url = "https://api.groq.com/openai/v1/chat/completions";
        var payload = new {
            model = "llama-3.3-70b-versatile",
            messages = new[] { new { role = "user", content = "say hi in json" } },
            temperature = 0.2,
            response_format = new { type = "json_object" }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, url) {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("GROQ_API_KEY"));
        request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://clientscout.local");
        request.Headers.TryAddWithoutValidation("X-Title", "ClientScout");
        
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try {
            using var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Status: {response.StatusCode}, Time: {sw.ElapsedMilliseconds}ms");
        } catch (Exception ex) {
            Console.WriteLine($"Error: {ex.Message}, Time: {sw.ElapsedMilliseconds}ms");
        }
    }
}
