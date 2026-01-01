using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
    public static class GroupNameResolver
    {
        /// <summary>
        /// Builds an environment-prefixed group name to avoid collisions across QA/Prod/Dev.
        /// </summary>
        public static string Resolve(string baseGroupName)
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            return $"{env}:{baseGroupName}";
        }
    }
}
