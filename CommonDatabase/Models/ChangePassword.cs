using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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


    public class AlertPermission
    {
        [Required]
        public int ClientId { get; set; }
        [Required]
        public bool IsAlertPermission { get; set; }
    }
}
