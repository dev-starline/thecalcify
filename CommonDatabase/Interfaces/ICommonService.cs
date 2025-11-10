using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Interfaces
{
    public interface ICommonService
    {
        Task GetDeviceAccessSummaryAsync(int ClientId, string Username);
    }
}
