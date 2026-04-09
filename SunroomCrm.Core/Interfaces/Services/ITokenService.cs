using SunroomCrm.Core.Entities;

namespace SunroomCrm.Core.Interfaces.Services;

public interface ITokenService
{
    string GenerateToken(User user);
}
