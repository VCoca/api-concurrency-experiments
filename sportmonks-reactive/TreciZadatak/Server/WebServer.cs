using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreciZadatak.Configuration;
using TreciZadatak.Logging;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Net;
using TreciZadatak.Entities;
using System.Text.Json.Nodes;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TreciZadatak.Server
{
    public class WebServer
    {
        private readonly HttpListener httpListener = new HttpListener();
        private readonly HttpClient httpClient = new HttpClient();
        private readonly AppSettings appSettings;

        private readonly IObservable<HttpListenerContext> requests;
        private IDisposable? subscription;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        private readonly Dictionary<int, string> countryMap = new Dictionary<int, string>();

        public WebServer(AppSettings appSettings)
        {
            this.appSettings = appSettings;
            httpListener.Prefixes.Add(appSettings.GetListenerPrefix());

            var stopSignal = Observable.FromEvent(h => cts.Token.Register(h), h => { });

            requests = Observable
                .Defer(() => Observable.FromAsync(httpListener.GetContextAsync))
                .Repeat()
                .TakeUntil(stopSignal)
                .ObserveOn(TaskPoolScheduler.Default)
                .SelectMany(c => Observable.FromAsync(async () =>
                {
                    Logger.Log($"{c.Request.Url} - Startovana obrada na niti: {Thread.CurrentThread.ManagedThreadId}");
                    await HandleRequest(c);
                    Logger.Log($"Zavrsena obrada na niti: {Thread.CurrentThread.ManagedThreadId}");
                    return c;
                }));
        }
        public async Task StartAsync()
        {
            await LoadCountriesAsync();

            foreach (var kv in countryMap)
            {
                Logger.Log($"Country loaded: {kv.Key} -> {kv.Value}");
            }

            httpListener.Start();
            Logger.Log($"Web server pokrenut na {appSettings.GetListenerPrefix()}");

            subscription = requests.Subscribe(_ => { }, ex => Logger.Log($"Greska u obradi zahteva: {ex.Message}"), () => Logger.Log("Web server je zaustavljen"));
        }
        public void Stop()
        {
            cts.Cancel();
            httpListener.Stop();
            subscription?.Dispose();
        }
        public async Task HandleRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;

                int id;
                int per_page = int.TryParse(request.QueryString["per_page"], out var pp) ? pp : 1;
                int page = int.TryParse(request.QueryString["page"], out var p) ? p : 1;
                var latestId = await GetFixtureIdAsync(per_page, page);
                if (latestId.HasValue)
                    id = latestId.Value;
                else
                    id = 1;
                var url = appSettings.GetAPIUrl(id);
                Logger.Log($"Pozivam API: {url}");

                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    context.Response.StatusCode = (int)response.StatusCode;
                    await RespondWithTextAsync(context, $"API greska: {response.StatusCode}");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("data", out var fixtureEl))
                {
                    var fixture = ParseJson(fixtureEl);
                    await RespondWithJsonAsync(context, fixture);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    await RespondWithTextAsync(context, "Nema podataka za zadati ID.");
                }

            }
            catch (Exception ex)
            {
                Logger.Log($"Greska u HandleRequest: {ex.Message}");
                context.Response.StatusCode = 500;
                await RespondWithTextAsync(context, "Interna greska servera");
            }
        }
        public Fixture ParseJson(JsonElement fixtureEl)
        {
            var f = new Fixture();
            f.Name = fixtureEl.GetProperty("name").GetString() ?? "";
            if(fixtureEl.TryGetProperty("starting_at", out var stEl) && stEl.GetString() is string stStr)
                f.Starting = DateTime.Parse(stStr);

            if(fixtureEl.TryGetProperty("lineups", out var lineupsEl) && lineupsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in lineupsEl.EnumerateArray())
                {
                    var pl = new Player();

                    if (p.TryGetProperty("player_name", out var pnEl))
                        pl.Name = pnEl.GetString() ?? "";

                    if (p.TryGetProperty("jersey_number", out var jnEl) && jnEl.TryGetInt32(out var jn))
                        pl.Number = jn;

                    if (p.TryGetProperty("player", out var playerNested) && playerNested.ValueKind == JsonValueKind.Object)
                    {
                        if (playerNested.TryGetProperty("date_of_birth", out var dobEl))
                        {
                            var dobStr = dobEl.GetString();
                            if (!string.IsNullOrWhiteSpace(dobStr) && DateTime.TryParse(dobStr, out var dob))
                                pl.DateOfBirth = dob;
                        }
                        if (playerNested.TryGetProperty("nationality_id", out var natIdEl))
                        {
                            int natId = natIdEl.GetInt32();
                            pl.Country = countryMap.TryGetValue(natId, out var name) ? name : "Unknown";
                        }
                    }
                    f.Players.Add(pl);
                }
            }
            return f;
        }
        private async Task RespondWithJsonAsync(HttpListenerContext context, object content)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            byte[] buffer = JsonSerializer.SerializeToUtf8Bytes(content, options);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        private async Task RespondWithTextAsync(HttpListenerContext context, string content)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }
        private async Task LoadCountriesAsync()
        {
            var url = appSettings.GetCountriesUrl();
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var data = doc.RootElement.GetProperty("data");
            foreach(var country in data.EnumerateArray())
            {
                int id = country.GetProperty("id").GetInt32();
                string name = country.GetProperty("name").GetString() ?? "Unknown";
                countryMap[id] = name;
            }
        }
        private async Task<int?> GetFixtureIdAsync(int per_page, int page)
        {
            try
            {
                var url = appSettings.GetFixturesUrl(per_page, page);
                Logger.Log($"Pozivam API za fixture: {url}");

                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Log($"Greska pri dobijanju fixture-a: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                {
                    var firstFixture = dataEl.EnumerateArray().FirstOrDefault();
                    if (firstFixture.ValueKind != JsonValueKind.Undefined && firstFixture.TryGetProperty("id", out var idEl))
                    {
                        int fixtureId = idEl.GetInt32();
                        Logger.Log($"Fixture ID: {fixtureId}");
                        return fixtureId;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Greska u GetFixtureIdAsync: {ex.Message}");
                return null;
            }
        }
    }
}
