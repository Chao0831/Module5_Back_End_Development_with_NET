// Controllers/UsersController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementAPI.Data;
using UserManagementAPI.DTOs;
using UserManagementAPI.Models;
using System.Text.RegularExpressions;

namespace UserManagementAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(UserDbContext context, ILogger<UsersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/users (with pagination and filtering)
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUsers(
        [FromQuery] bool? isActive = null,
        [FromQuery] string? department = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var query = _context.Users.AsNoTracking().AsQueryable();

            // Apply filters
            if (isActive.HasValue)
            {
                query = query.Where(u => u.IsActive == isActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(u => u.Department == department);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim().ToLower();
                query = query.Where(u => 
                    u.FirstName.ToLower().Contains(searchTerm) ||
                    u.LastName.ToLower().Contains(searchTerm) ||
                    u.Email.ToLower().Contains(searchTerm));
            }

            // Get total count for pagination
            var totalCount = await query.CountAsync();
            
            // Apply pagination
            var users = await query
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Add pagination headers
            Response.Headers.Add("X-Total-Count", totalCount.ToString());
            Response.Headers.Add("X-Page", page.ToString());
            Response.Headers.Add("X-Page-Size", pageSize.ToString());
            Response.Headers.Add("X-Total-Pages", Math.Ceiling(totalCount / (double)pageSize).ToString());

            var userDtos = users.Select(u => MapToDto(u));
            return Ok(userDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, new { error = "An error occurred while retrieving users", details = ex.Message });
        }
    }

    // GET: api/users/{id}
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserResponseDto>> GetUser(int id)
    {
        try
        {
            if (id <= 0)
            {
                return BadRequest(new { error = "Invalid user ID. ID must be a positive number." });
            }

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                _logger.LogWarning("User with ID {Id} not found", id);
                return NotFound(new { error = $"User with ID {id} not found" });
            }

            return Ok(MapToDto(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {Id}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the user", details = ex.Message });
        }
    }

    // POST: api/users
    [HttpPost]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserResponseDto>> CreateUser([FromBody] CreateUserDto createUserDto)
    {
        try
        {
            // Manual validation in case ModelState is not used
            if (createUserDto == null)
            {
                return BadRequest(new { error = "User data is required" });
            }

            // Additional email format validation
            if (!IsValidEmail(createUserDto.Email))
            {
                return BadRequest(new { error = "Invalid email format" });
            }

            // Check if email already exists
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == createUserDto.Email);

            if (existingUser != null)
            {
                return Conflict(new { error = $"User with email {createUserDto.Email} already exists" });
            }

            var user = new User
            {
                FirstName = createUserDto.FirstName.Trim(),
                LastName = createUserDto.LastName.Trim(),
                Email = createUserDto.Email.Trim().ToLower(),
                Department = createUserDto.Department?.Trim() ?? "General",
                Role = createUserDto.Role?.Trim() ?? "Employee",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User created successfully with ID {Id}", user.Id);

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, MapToDto(user));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error creating user");
            return StatusCode(500, new { error = "A database error occurred while creating the user" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new { error = "An error occurred while creating the user" });
        }
    }

    // PUT: api/users/{id}
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto updateUserDto)
    {
        try
        {
            if (id <= 0)
            {
                return BadRequest(new { error = "Invalid user ID. ID must be a positive number." });
            }

            if (updateUserDto == null)
            {
                return BadRequest(new { error = "User data is required" });
            }

            // Additional email format validation
            if (!IsValidEmail(updateUserDto.Email))
            {
                return BadRequest(new { error = "Invalid email format" });
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("Attempted to update non-existent user with ID {Id}", id);
                return NotFound(new { error = $"User with ID {id} not found" });
            }

            // Check if email is being changed and if it's already taken
            if (!string.Equals(user.Email, updateUserDto.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == updateUserDto.Email && u.Id != id);
                
                if (existingUser != null)
                {
                    return Conflict(new { error = $"Email {updateUserDto.Email} is already in use" });
                }
            }

            // Update user properties with retry logic for concurrency
            var retryCount = 0;
            var maxRetries = 3;
            var updated = false;

            while (!updated && retryCount < maxRetries)
            {
                try
                {
                    user.FirstName = updateUserDto.FirstName.Trim();
                    user.LastName = updateUserDto.LastName.Trim();
                    user.Email = updateUserDto.Email.Trim().ToLower();
                    user.Department = updateUserDto.Department.Trim();
                    user.Role = updateUserDto.Role.Trim();
                    user.IsActive = updateUserDto.IsActive;
                    user.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    updated = true;
                }
                catch (DbUpdateConcurrencyException) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    await Task.Delay(100 * retryCount); // Exponential backoff
                    await _context.Entry(user).ReloadAsync();
                }
            }

            if (!updated)
            {
                return StatusCode(409, new { error = "Concurrency conflict. Please try again." });
            }

            _logger.LogInformation("User with ID {Id} updated successfully", id);
            return Ok(MapToDto(user));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error updating user {Id}", id);
            return StatusCode(500, new { error = "A database error occurred while updating the user" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {Id}", id);
            return StatusCode(500, new { error = "An error occurred while updating the user" });
        }
    }

    // DELETE: api/users/{id} (Soft delete)
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            if (id <= 0)
            {
                return BadRequest(new { error = "Invalid user ID. ID must be a positive number." });
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("Attempted to delete non-existent user with ID {Id}", id);
                return NotFound(new { error = $"User with ID {id} not found" });
            }

            // Check if already inactive
            if (!user.IsActive)
            {
                return BadRequest(new { error = $"User with ID {id} is already inactive" });
            }

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User with ID {Id} deactivated successfully", id);
            return NoContent();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error deleting user {Id}", id);
            return StatusCode(500, new { error = "A database error occurred while deleting the user" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {Id}", id);
            return StatusCode(500, new { error = "An error occurred while deleting the user" });
        }
    }

    // PATCH: api/users/{id}/activate
    [HttpPatch("{id}/activate")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ActivateUser(int id)
    {
        try
        {
            if (id <= 0)
            {
                return BadRequest(new { error = "Invalid user ID. ID must be a positive number." });
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("Attempted to activate non-existent user with ID {Id}", id);
                return NotFound(new { error = $"User with ID {id} not found" });
            }

            // Check if already active
            if (user.IsActive)
            {
                return BadRequest(new { error = $"User with ID {id} is already active" });
            }

            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User with ID {Id} activated successfully", id);
            return Ok(MapToDto(user));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error activating user {Id}", id);
            return StatusCode(500, new { error = "A database error occurred while activating the user" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating user {Id}", id);
            return StatusCode(500, new { error = "An error occurred while activating the user" });
        }
    }

    // Helper method for email validation
    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            email = email.Trim();
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static UserResponseDto MapToDto(User user)
    {
        return new UserResponseDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Department = user.Department,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}