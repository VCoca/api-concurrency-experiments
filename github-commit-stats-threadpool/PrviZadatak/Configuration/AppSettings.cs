using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace PrviZadatak.Configuration
{
    public class AppSettings
    {
        public int Port { get; set; }
        public int CacheSize { get; set; }

        public string GetListenerPrefix() => $"http://localhost:{Port}/";

        public string GetApiUrl(string owner, string repo) => $"https://api.github.com/repos/{owner}/{repo}/stats/contributors";

        public AppSettings()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("AppSettings.json", optional: false, reloadOnChange: true)
                .Build();

            Port = int.TryParse(config["Port"], out var port) ? port : 8080;
            CacheSize = int.TryParse(config["CacheSize"], out var cacheSize) ? cacheSize : 128;
        }
    }
}
