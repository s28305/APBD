using Tutorial6.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tutorial6.DTO;
using Tutorial6.Helpers;
using Tutorial6.Models;

namespace Tutorial6.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController(AnimalClinicContext context, IAuthenticationService authService) : ControllerBase
{
    private readonly PasswordHasher<User> _passwordHasher = new();
    
    [Route("register")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateUser([FromBody] UserDto userDto, CancellationToken cancellationToken)
    {
        var userType = await context.UserTypes.FirstOrDefaultAsync(u => u.Name == "User", cancellationToken);
        
        if (userType == null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "UserType Not Found",
                Detail = "UserType 'User' isn't present in the database.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
        
        var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Username == userDto.Username, cancellationToken);
        
        if (existingUser != null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Username exists",
                Detail = "User with such username already exists in the database.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
        
        var user = userDto.Map();
        
        user.Password = _passwordHasher.HashPassword(user, user.Password);
        user.TypeId = context.UserTypes.First(r => r.Name == "User").Id;
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return Created();
    }
    
    [Route("login")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponseDto>> LoginUser([FromBody] LoginUserDto loginUser,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.Include(u => u.Type).FirstOrDefaultAsync(
            u => u.Username == loginUser.Username,
            cancellationToken: cancellationToken);
        
        if (user == null)
        {
            return Unauthorized();
        }

        var verificationRes = _passwordHasher.VerifyHashedPassword(user, user.Password, loginUser.Password);
        
        if (verificationRes == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        var responseDto = new LoginResponseDto
        {
            AccessToken = authService.GenerateAccessToken(user),
            RefreshToken = authService.GenerateRefreshToken()
        };

        user.RefreshToken = responseDto.RefreshToken;
       
        user.RefreshTokenExpire = DateTime.Now.AddMinutes(30);  // valid for half an hour

        context.Entry(user).State = EntityState.Modified;
        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(responseDto);
    }
    
    
    [Route("refresh")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponseDto>> RefreshToken([FromBody] LoginResponseDto auth, CancellationToken cancellationToken)
    {
        var isValid = await authService.ValidateExpiredTokenAsync(auth.AccessToken);
        
        if (!isValid)
        {
            return Forbid();
        }
        
        var user = await context.Users.Include(u => u.Type)
            .FirstOrDefaultAsync(u => u.RefreshToken == auth.RefreshToken, cancellationToken: cancellationToken);
        
        if (user == null || user.RefreshTokenExpire < DateTime.Now)
        {
            return Forbid();
        }

        var responseDto = new LoginResponseDto
        {
            AccessToken = authService.GenerateAccessToken(user),
            RefreshToken = authService.GenerateRefreshToken()
        };

        user.RefreshToken = responseDto.RefreshToken;
        user.RefreshTokenExpire = DateTime.Now.AddMinutes(30);

        context.Entry(user).State = EntityState.Modified;
        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(responseDto);
    }
}