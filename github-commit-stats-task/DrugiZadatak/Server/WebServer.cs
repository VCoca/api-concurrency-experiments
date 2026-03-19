using DrugiZadatak.CacheManager;
using DrugiZadatak.Configuration;
using DrugiZadatak.Exceptions;
using DrugiZadatak.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DrugiZadatak.Server
{
    public class WebServer
    {
        private readonly HttpListener httpListener = new HttpListener();
        private readonly HttpClient httpClient = new HttpClient();

        private readonly Cache _cache;
        private readonly AppSettings _appSettings;

        public WebServer(AppSettings appSettings, Cache cache)
        {
            _appSettings = appSettings;
            _cache = cache;
            httpListener.Prefixes.Add(_appSettings.GetListenerPrefix());
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PrviZadatakApp/1.0");
        }

        public async Task StartAsync()
        {
            httpListener.Start();
            Logger.Log($"Web server pokrenut na {_appSettings.GetListenerPrefix()}");

            while (true)
            {
                var context = await httpListener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context));
            }
        }
        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;

            string? owner = request.QueryString["owner"];
            string? repo = request.QueryString["repo"];

            string customQuery = $"owner={owner}&repo={repo}";

            if (_cache.TryGet(customQuery, out var cached))
            {
                Logger.Log("Pronadjen u kesu");
                await RespondWithJsonAsync(context, new { commits = cached });
                return;
            }
            try
            {
                var url = _appSettings.GetApiUrl(owner, repo);
                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    string? errMessage = null;
                    string errJson = await response.Content.ReadAsStringAsync();
                    throw new GitAPIException($"Git API greska: {response.StatusCode}", (int)response.StatusCode, errMessage);
                }
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                int totalCommits = 0;

                foreach (var contributor in doc.RootElement.EnumerateArray())
                {
                    if (contributor.TryGetProperty("total", out var total))
                    {
                        totalCommits += total.GetInt32();
                    }
                }
                _cache.Add(customQuery, totalCommits);
                Logger.Log("Odgovor dobijen i kesiran");
                await RespondWithJsonAsync(context, new { commits = totalCommits });

            }
            catch (GitAPIException ex)
            {
                Logger.Log($"Greska: {ex.ApiMessage}");
                context.Response.StatusCode = ex.StatusCode;
                await RespondWithTextAsync(context, ex.Message);
            }
        }
        private async Task RespondWithJsonAsync(HttpListenerContext context, object? content)
        {
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            };
            byte[] buffer = JsonSerializer.SerializeToUtf8Bytes(content, options);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            await context.Response.OutputStream.FlushAsync();
            context.Response.Close();
        }
        private async Task RespondWithTextAsync(HttpListenerContext context, string content)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(content);
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            await context.Response.OutputStream.FlushAsync();
            context.Response.Close();
        }
    }
}
