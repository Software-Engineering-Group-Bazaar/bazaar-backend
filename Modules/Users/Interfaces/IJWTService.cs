using System.Collections.Generic;
using System.Threading.Tasks;
using Users.Models;

namespace Users.Interfaces
{

    public interface IJWTService
    {
        Task<(string, DateTime)> GenerateTokenAsync(User user, IList<string> roles);
    }
}