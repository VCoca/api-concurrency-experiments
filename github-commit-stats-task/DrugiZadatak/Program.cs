using DrugiZadatak.CacheManager;
using DrugiZadatak.Configuration;
using DrugiZadatak.Server;

var appSettings = new AppSettings();
var cache = new Cache(appSettings);
var server = new WebServer(appSettings, cache);
await server.StartAsync();
