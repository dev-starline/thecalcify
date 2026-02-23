using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
    public class ChangePassword
    {
        public int ClientId { get; set; }
        public string Password { get; set; }
    }
}
