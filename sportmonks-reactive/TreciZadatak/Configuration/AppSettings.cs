using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace TreciZadatak.Configuration
{
    public class AppSettings
    {
        public string ApiToken { get; set; }
        public int Port { get; set; } = 8080;
        public string BaseUrl = "https://api.sportmonks.com/v3/";
        public string GetListenerPrefix() => $"http://localhost:{Port}/";

        public string GetAPIUrl(int id)
        {
            return $"{BaseUrl}football/fixtures/{id}?include=lineups.player&api_token={ApiToken}";
        }

        public string GetCountriesUrl()
        {
            return $"{BaseUrl}core/countries?api_token={ApiToken}";
        }

        public string GetFixturesUrl(int per_page, int page)
        {
            return $"{BaseUrl}football/fixtures/?api_token={ApiToken}&sort=starting_at&order=desc&per_page={per_page}&page={page}";
        }

        public string GetPlayerUrl(int id)
        {
            return $"{BaseUrl}football/players/{id}?api_token={ApiToken}";
        }

        public AppSettings()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("AppSettings.json", optional: false, reloadOnChange: true)
                .Build();

            ApiToken = config["ApiToken"];
            Port = int.TryParse(config["Port"], out var port) ? port : 8080;
        }
    }
}
