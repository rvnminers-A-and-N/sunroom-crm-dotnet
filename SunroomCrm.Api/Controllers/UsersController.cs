using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunroomCrm.Core.DTOs.Auth;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Core.Interfaces.Repositories;

namespace SunroomCrm.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _users;

    public UsersController(IUserRepository users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _users.GetAllAsync();
        return Ok(users.Select(MapUserDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _users.GetByIdAsync(id);
        if (user == null) return NotFound();
        return Ok(MapUserDto(user));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        var user = await _users.GetByIdAsync(id);
        if (user == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name))
            user.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.Role) &&
            Enum.TryParse<UserRole>(request.Role, true, out var role))
        {
            user.Role = role;
        }

        if (request.AvatarUrl != null)
            user.AvatarUrl = request.AvatarUrl;

        await _users.UpdateAsync(user);
        return Ok(MapUserDto(user));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _users.ExistsAsync(id))
            return NotFound();

        await _users.DeleteAsync(id);
        return NoContent();
    }

    private static UserDto MapUserDto(User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        Role = user.Role.ToString(),
        AvatarUrl = user.AvatarUrl,
        CreatedAt = user.CreatedAt
    };
}

public class UpdateUserRequest
{
    public string? Name { get; set; }
    public string? Role { get; set; }
    public string? AvatarUrl { get; set; }
}
