using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoAPI.Application.Contracts;
using TodoAPI.Application.Services;

namespace TodoAPI.Controllers;

[ApiController, Authorize, Route("api/categories")]
public sealed class CategoriasController(CategoriaService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await service.GetAllAsync(UserId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(UserId, id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCategoriaRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(UserId, request.Nome, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateCategoriaRequest request, CancellationToken ct)
    {
        var result = await service.UpdateAsync(UserId, id, request.Nome, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var (found, inUse) = await service.DeleteAsync(UserId, id, ct);
        if (!found) return NotFound();
        return inUse ? Problem(statusCode: StatusCodes.Status409Conflict, title: "Categoria em uso.") : NoContent();
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
