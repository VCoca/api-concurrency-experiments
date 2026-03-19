using TreciZadatak.Configuration;
using TreciZadatak.Server;

var appSettings = new AppSettings();
var server = new WebServer(appSettings);

try
{
    await server.StartAsync();
    Console.ReadKey();
}
finally
{
    server.Stop();
}
