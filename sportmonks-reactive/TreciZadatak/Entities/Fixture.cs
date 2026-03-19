using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TreciZadatak.Entities
{
    public class Fixture
    {
        public string Name { get; set; }
        public DateTime Starting {  get; set; }
        public List<Player> Players { get; set; } = new List<Player>();

    }
}
