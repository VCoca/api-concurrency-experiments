using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrviZadatak.Exceptions
{
    public class GitAPIException : Exception
    {
        public int StatusCode {  get; }
        public string ApiMessage { get; }

        public GitAPIException(string message, int statusCode, string? apiMessage = null) 
            : base(message)
        {
            StatusCode = statusCode;
            ApiMessage = apiMessage;
        }
    }
}
