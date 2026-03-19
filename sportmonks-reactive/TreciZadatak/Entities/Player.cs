using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TreciZadatak.Entities
{
    public class Player
    {
        public string? Name { get; set; }
        public DateTime DateOfBirth { get; set; }
        public int Number {  get; set; }
        public string? Country { get; set; }
    }
}
