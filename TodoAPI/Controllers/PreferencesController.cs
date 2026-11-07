using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoAPI.Application.Contracts;
using TodoAPI.Application.Services;

namespace TodoAPI.Controllers;

[ApiController, Authorize, Route("api/preferences")]
public sealed class PreferencesController(PreferenceService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await service.GetAsync(UserId, ct));

    [HttpPut]
    public async Task<IActionResult> Update(UpdatePreferenceRequest request, CancellationToken ct)
    {
        var result = await service.UpdateAsync(UserId, request.TimeZoneId, ct);
        if (result.NotFound) return NotFound();
        if (result.Error is not null)
        {
            ModelState.AddModelError("timeZoneId", result.Error);
            return ValidationProblem(ModelState);
        }
        if (result.Conflict) return Problem(statusCode: StatusCodes.Status409Conflict, title: "A alteração de fuso cria conflito na agenda.");
        return Ok(result.Value);
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
