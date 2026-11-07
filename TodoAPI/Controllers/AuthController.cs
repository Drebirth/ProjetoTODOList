using Microsoft.AspNetCore.Mvc;
using TodoAPI.Application.Contracts;
using TodoAPI.Application.Services;

namespace TodoAPI.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService authService, JwtTokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<RegisteredUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var (user, usernameAlreadyExists) = await authService.RegisterAsync(
            request.Username,
            request.Password,
            cancellationToken);

        if (usernameAlreadyExists)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Username já cadastrado.");
        }

        return StatusCode(StatusCodes.Status201Created, new RegisteredUserResponse(user!.Id, user.Username));
    }

    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await authService.ValidateCredentialsAsync(request.Username, request.Password, cancellationToken);
        if (user is null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Credenciais inválidas.");
        }

        var (token, expiresAtUtc) = tokenService.CreateToken(user);
        return Ok(new LoginResponse(token, "Bearer", expiresAtUtc));
    }
}
