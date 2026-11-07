using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoAPI.Application.Services;

namespace TodoAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/todo-series")]
public sealed class TodoSeriesController(TodoService todoService) : ControllerBase
{
    [HttpDelete("{seriesId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int seriesId, CancellationToken cancellationToken)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await todoService.DeleteSeriesAsync(userId, seriesId, cancellationToken);
        return result.NotFound ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Série não encontrada.") : NoContent();
    }
}
