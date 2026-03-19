using PrviZadatak.CacheManager;
using PrviZadatak.Configuration;
using PrviZadatak.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrviZadatak.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace PrviZadatak.Server
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

        public void Start()
        {
            httpListener.Start();
            Logger.Log($"Web server pokrenut na {_appSettings.GetListenerPrefix()}");

            while (true)
            {
                var context = httpListener.GetContext();
                ThreadPool.QueueUserWorkItem(HandleRequest, context);
            }
        }
        private void HandleRequest(object? state)
        {
            var context = (HttpListenerContext)state!;
            var request = context.Request;

            string? owner = request.QueryString["owner"];
            string? repo = request.QueryString["repo"];

            string customQuery = $"owner={owner}&repo={repo}";

            if(_cache.TryGet(customQuery, out var cached))
            {
                Logger.Log("Pronadjen u kesu");
                RespondWithJson(context, new { commits = cached });
                return;
            }
            try
            {
                var url = _appSettings.GetApiUrl(owner, repo);
                var response = httpClient.GetAsync(url).Result;
                if (!response.IsSuccessStatusCode)
                {
                    string? errMessage = null;
                    var errJson = response.Content.ReadAsStringAsync().Result;
                    using var docErr = JsonDocument.Parse(errJson);
                    if(docErr.RootElement.TryGetProperty("error", out var err))
                    {
                        errMessage = err.GetProperty("message").GetString();
                    }
                    throw new GitAPIException($"Git API greska: {response.StatusCode}", (int)response.StatusCode, errMessage);
                }
                var json = response.Content.ReadAsStringAsync().Result;
                using var doc = JsonDocument.Parse(json);
                int totalCommits = 0;

                foreach(var contributor in doc.RootElement.EnumerateArray())
                {
                    if(contributor.TryGetProperty("total", out var total))
                    {
                        totalCommits += total.GetInt32();
                    }
                }
                _cache.Add(customQuery, totalCommits);
                Logger.Log("Odgovor dobijen i kesiran");
                RespondWithJson(context, new { comits = totalCommits });

            }
            catch (GitAPIException ex)
            {
                Logger.Log($"Greska: {ex.ApiMessage}");
                context.Response.StatusCode = ex.StatusCode;
                RespondWithText(context, ex.Message);
            }
        }
        private void RespondWithJson(HttpListenerContext context, object? content)
        {
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            };
            byte[] buffer = JsonSerializer.SerializeToUtf8Bytes(content, options);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }
        private void RespondWithText(HttpListenerContext context, string content)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(content);
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }
    }
}
