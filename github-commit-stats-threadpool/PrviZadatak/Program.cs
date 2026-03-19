using PrviZadatak.Configuration;
using PrviZadatak.CacheManager;
using PrviZadatak.Server;

var appSettings = new AppSettings();
var cache = new Cache(appSettings);
var server = new WebServer(appSettings, cache);
server.Start();
