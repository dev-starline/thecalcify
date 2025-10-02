using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Interfaces
{
    public interface IJwtBlacklistService
    {
        Task AddToBlacklistAsync(string token);
        Task<bool> IsBlacklistedAsync(string token);
    }

    public class JwtBlacklistService : IJwtBlacklistService
    {
        private static readonly HashSet<string> _blacklist = new();

        public Task AddToBlacklistAsync(string token)
        {
            _blacklist.Add(token);
            return Task.CompletedTask;
        }

        public Task<bool> IsBlacklistedAsync(string token)
        {
            return Task.FromResult(_blacklist.Contains(token));
        }
    }
}
