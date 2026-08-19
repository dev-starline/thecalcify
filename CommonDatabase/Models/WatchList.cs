using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
   

    public class ReqWatchLists
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string WId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Layout { get; set; } = string.Empty;
        public string Charts { get; set; } = string.Empty;
        public string Sizes { get; set; } = string.Empty;
    }
    public class WatchLists: ReqWatchLists
    {
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime UpdatedDate { get; set; } = DateTime.Now;
        public int UpdatedByClientId { get; set; }
    }
}
