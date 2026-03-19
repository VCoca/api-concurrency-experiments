using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrviZadatak.Logging
{
    public static class Logger
    {
        private static readonly object _lock = new object();

        public static void Log(string message)
        {
            lock (_lock)
            {
                Console.WriteLine(message);
            }
        }
    }
}
