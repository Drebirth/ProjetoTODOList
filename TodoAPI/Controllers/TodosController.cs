using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoAPI.Application.Contracts;
using TodoAPI.Application.Services;
using TodoAPI.Domain.Entities;

namespace TodoAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/todos")]
public sealed class TodosController(TodoService todoService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<TodoResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateTodoRequest request, CancellationToken cancellationToken)
    {
        var result = await todoService.CreateAsync(
            GetUserId(),
            request.Titulo,
            request.Descricao,
            request.Concluido,
            request.CategoriaId,
            request.Prioridade,
            request.FrequenciaRecorrencia,
            request.DuracaoRecorrencia,
            request.DataInicioUtc,
            request.DataFimUtc,
            cancellationToken);
        if (result.Conflict) return ConflictProblem();
        if (result.ValidationError is not null)
        {
            return ValidationError(result.ValidationError);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet]
    [ProducesResponseType<TodoPageResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 15)
        {
            ModelState.AddModelError(
                page < 1 ? "page" : "pageSize",
                "Use page maior que zero e pageSize entre 1 e 15.");
            return ValidationProblem(ModelState);
        }

        var result = await todoService.GetPageAsync(GetUserId(), page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<TodoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var todo = await todoService.GetByIdAsync(GetUserId(), id, cancellationToken);
        return todo is null ? NotFoundProblem() : Ok(todo);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<TodoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpdateTodoRequest request, CancellationToken cancellationToken)
    {
        var result = await todoService.UpdateAsync(
            GetUserId(),
            id,
            request.Titulo,
            request.Descricao,
            request.Concluido,
            request.CategoriaId,
            request.Prioridade,
            request.FrequenciaRecorrencia,
            request.DuracaoRecorrencia,
            request.DataInicioUtc,
            request.DataFimUtc,
            cancellationToken);
        if (result.NotFound)
        {
            return NotFoundProblem();
        }

        if (result.Conflict) return ConflictProblem();

        return result.ValidationError is not null ? ValidationError(result.ValidationError) : Ok(result.Value);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, [FromQuery] string? scope, CancellationToken cancellationToken)
    {
        var result = await todoService.DeleteAsync(GetUserId(), id, scope, cancellationToken);
        if (result.NotFound)
        {
            return NotFoundProblem();
        }

        return result.ValidationError is not null ? ValidationError(result.ValidationError) : NoContent();
    }

    private IActionResult ValidationError(string message)
    {
        ModelState.AddModelError("recurrence", message);
        return ValidationProblem(ModelState);
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private ObjectResult ConflictProblem() => Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflito na agenda.");

    private ObjectResult NotFoundProblem() => Problem(statusCode: StatusCodes.Status404NotFound, title: "Tarefa não encontrada.");
}
