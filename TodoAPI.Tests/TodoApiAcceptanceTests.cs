using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TodoAPI.Infrastructure.Persistence;
using TodoAPI.Application.Services;
using Xunit;

namespace TodoAPI.Tests;

public sealed class TodoApiAcceptanceTests : IClassFixture<TodoApiFactory>
{
    private readonly HttpClient _client;
    private readonly TodoApiFactory _factory;

    public TodoApiAcceptanceTests(TodoApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_rejects_duplicate_username_without_case_sensitivity()
    {
        var username = $"user-{Guid.NewGuid():N}";

        var first = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username,
            password = "A-secure-password-123!"
        });
        var duplicate = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = username.ToUpperInvariant(),
            password = "A-secure-password-123!"
        });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
        var savedUser = await dbContext.Users.SingleAsync(user => user.Username == username);
        Assert.NotEqual("A-secure-password-123!", savedUser.PasswordHash);

        using var invalidLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username,
            password = "incorrect-password"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, invalidLogin.StatusCode);
        Assert.Equal("application/problem+json", invalidLogin.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Todo_crud_is_authenticated_and_scoped_to_owner()
    {
        var owner = await CreateUserAndLoginAsync($"owner-{Guid.NewGuid():N}");
        var otherUser = await CreateUserAndLoginAsync($"other-{Guid.NewGuid():N}");

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/todos")
        {
            Content = JsonContent.Create(new { titulo = "Tarefa", descricao = "Minha tarefa" })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);
        using var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var createdJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var id = createdJson.RootElement.GetProperty("id").GetInt32();
        Assert.Equal("Minha tarefa", createdJson.RootElement.GetProperty("descricao").GetString());
        Assert.False(createdJson.RootElement.GetProperty("concluido").GetBoolean());
        Assert.Equal(DateTimeKind.Utc, createdJson.RootElement.GetProperty("dataCriacao").GetDateTime().Kind);
        Assert.Equal(JsonValueKind.Null, createdJson.RootElement.GetProperty("dataConclusaoUtc").ValueKind);

        using var otherCreate = AuthenticatedRequest(HttpMethod.Post, "/api/todos", otherUser.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Tarefa de outro usuário" }));
        using var otherCreateResponse = await _client.SendAsync(otherCreate);
        Assert.Equal(HttpStatusCode.Created, otherCreateResponse.StatusCode);

        using var ownerList = AuthenticatedRequest(HttpMethod.Get, "/api/todos", owner.Token);
        using var ownerListResponse = await _client.SendAsync(ownerList);
        using var ownerListJson = JsonDocument.Parse(await ownerListResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, ownerListJson.RootElement.GetProperty("items").GetArrayLength());

        using var otherGet = AuthenticatedRequest(HttpMethod.Get, $"/api/todos/{id}", otherUser.Token);
        using var otherGetResponse = await _client.SendAsync(otherGet);
        Assert.Equal(HttpStatusCode.NotFound, otherGetResponse.StatusCode);

        using var otherUpdate = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{id}", otherUser.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Acesso indevido", concluido = true }));
        using var otherUpdateResponse = await _client.SendAsync(otherUpdate);
        Assert.Equal(HttpStatusCode.NotFound, otherUpdateResponse.StatusCode);

        using var ownUpdate = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{id}", owner.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Atualizada", concluido = true }));
        using var ownUpdateResponse = await _client.SendAsync(ownUpdate);
        Assert.Equal(HttpStatusCode.OK, ownUpdateResponse.StatusCode);

        using var otherDelete = AuthenticatedRequest(HttpMethod.Delete, $"/api/todos/{id}", otherUser.Token);
        using var otherDeleteResponse = await _client.SendAsync(otherDelete);
        Assert.Equal(HttpStatusCode.NotFound, otherDeleteResponse.StatusCode);

        using var ownDelete = AuthenticatedRequest(HttpMethod.Delete, $"/api/todos/{id}", owner.Token);
        using var ownDeleteResponse = await _client.SendAsync(ownDelete);
        Assert.Equal(HttpStatusCode.NoContent, ownDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task Completion_timestamp_is_server_managed_and_tracks_completion_transitions()
    {
        var user = await CreateUserAndLoginAsync($"completion-{Guid.NewGuid():N}");
        var clientSuppliedTimestamp = "2001-01-01T00:00:00Z";

        using var createCompleted = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new
            {
                titulo = "Tarefa",
                descricao = "Concluída na criação",
                concluido = true,
                dataConclusaoUtc = clientSuppliedTimestamp
            }));
        var beforeCreate = DateTime.UtcNow;
        using var createCompletedResponse = await _client.SendAsync(createCompleted);
        var afterCreate = DateTime.UtcNow;
        Assert.Equal(HttpStatusCode.Created, createCompletedResponse.StatusCode);
        using var completedJson = JsonDocument.Parse(await createCompletedResponse.Content.ReadAsStringAsync());
        var createCompletionTimestamp = completedJson.RootElement.GetProperty("dataConclusaoUtc").GetDateTime();
        Assert.Equal(DateTimeKind.Utc, createCompletionTimestamp.Kind);
        Assert.InRange(createCompletionTimestamp, beforeCreate, afterCreate);
        Assert.NotEqual(DateTime.Parse(clientSuppliedTimestamp).ToUniversalTime(), createCompletionTimestamp);
        var completedTodoId = completedJson.RootElement.GetProperty("id").GetInt32();

        using var createIncomplete = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Pendente" }));
        using var createIncompleteResponse = await _client.SendAsync(createIncomplete);
        using var incompleteJson = JsonDocument.Parse(await createIncompleteResponse.Content.ReadAsStringAsync());
        var incompleteTodoId = incompleteJson.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(JsonValueKind.Null, incompleteJson.RootElement.GetProperty("dataConclusaoUtc").ValueKind);

        using var complete = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{incompleteTodoId}", user.Token,
            JsonContent.Create(new
            {
                titulo = "Tarefa",
                descricao = "Pendente",
                concluido = true,
                dataConclusaoUtc = clientSuppliedTimestamp
            }));
        var beforeTransition = DateTime.UtcNow;
        using var completeResponse = await _client.SendAsync(complete);
        var afterTransition = DateTime.UtcNow;
        using var completedTransitionJson = JsonDocument.Parse(await completeResponse.Content.ReadAsStringAsync());
        var transitionTimestamp = completedTransitionJson.RootElement.GetProperty("dataConclusaoUtc").GetDateTime();
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        Assert.Equal(DateTimeKind.Utc, transitionTimestamp.Kind);
        Assert.InRange(transitionTimestamp, beforeTransition, afterTransition);
        Assert.NotEqual(DateTime.Parse(clientSuppliedTimestamp).ToUniversalTime(), transitionTimestamp);

        using var keepCompleted = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{incompleteTodoId}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Ainda concluída", concluido = true }));
        using var keepCompletedResponse = await _client.SendAsync(keepCompleted);
        using var stillCompletedJson = JsonDocument.Parse(await keepCompletedResponse.Content.ReadAsStringAsync());
        Assert.Equal(transitionTimestamp, stillCompletedJson.RootElement.GetProperty("dataConclusaoUtc").GetDateTime());

        using var reopen = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{incompleteTodoId}", user.Token,
            JsonContent.Create(new
            {
                titulo = "Tarefa",
                descricao = "Reaberta",
                concluido = false,
                dataConclusaoUtc = clientSuppliedTimestamp
            }));
        using var reopenResponse = await _client.SendAsync(reopen);
        using var reopenedJson = JsonDocument.Parse(await reopenResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);
        Assert.Equal(JsonValueKind.Null, reopenedJson.RootElement.GetProperty("dataConclusaoUtc").ValueKind);

        using var keepCreatedCompleted = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{completedTodoId}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Continua concluída", concluido = true }));
        using var keepCreatedCompletedResponse = await _client.SendAsync(keepCreatedCompleted);
        using var stillCreatedCompletedJson = JsonDocument.Parse(await keepCreatedCompletedResponse.Content.ReadAsStringAsync());
        Assert.Equal(createCompletionTimestamp, stillCreatedCompletedJson.RootElement.GetProperty("dataConclusaoUtc").GetDateTime());
    }

    [Fact]
    public async Task Recurring_todo_uses_server_owned_schedule_and_validates_frequency_and_duration()
    {
        var user = await CreateUserAndLoginAsync($"recurrence-create-{Guid.NewGuid():N}");

        using var missingDuration = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Inválida", frequenciaRecorrencia = "Diaria" }));
        using var missingDurationResponse = await _client.SendAsync(missingDuration);
        Assert.Equal(HttpStatusCode.BadRequest, missingDurationResponse.StatusCode);

        using var invalidDuration = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Inválida", frequenciaRecorrencia = "Mensal", duracaoRecorrencia = 0 }));
        using var invalidDurationResponse = await _client.SendAsync(invalidDuration);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDurationResponse.StatusCode);

        using var invalidFrequency = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Inválida", frequenciaRecorrencia = "Anual", duracaoRecorrencia = 3 }));
        using var invalidFrequencyResponse = await _client.SendAsync(invalidFrequency);
        Assert.Equal(HttpStatusCode.BadRequest, invalidFrequencyResponse.StatusCode);

        using var durationWithoutFrequency = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Inválida", duracaoRecorrencia = 3 }));
        using var durationWithoutFrequencyResponse = await _client.SendAsync(durationWithoutFrequency);
        Assert.Equal(HttpStatusCode.BadRequest, durationWithoutFrequencyResponse.StatusCode);

        using var create = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new
            {
                titulo = "Tarefa",
                descricao = "Diária",
                frequenciaRecorrencia = "Diaria",
                duracaoRecorrencia = 3,
                dataPrevistaUtc = "2001-01-01T00:00:00Z",
                serieRecorrenciaId = 999,
                numeroOcorrencia = 999
            }));
        var beforeCreate = DateTime.UtcNow;
        using var response = await _client.SendAsync(create);
        var afterCreate = DateTime.UtcNow;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        var createdAt = root.GetProperty("dataCriacao").GetDateTime();
        var dueAt = root.GetProperty("dataPrevistaUtc").GetDateTime();
        Assert.Equal("Diaria", root.GetProperty("frequenciaRecorrencia").GetString());
        Assert.Equal(3, root.GetProperty("duracaoRecorrencia").GetInt32());
        Assert.True(root.GetProperty("serieRecorrenciaId").GetInt32() > 0);
        Assert.Equal(1, root.GetProperty("numeroOcorrencia").GetInt32());
        Assert.Equal(createdAt, dueAt);
        Assert.InRange(createdAt, beforeCreate, afterCreate);
        Assert.NotEqual(DateTime.Parse("2001-01-01T00:00:00Z").ToUniversalTime(), dueAt);

        using var createCompleted = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Concluída", concluido = true, frequenciaRecorrencia = "Mensal", duracaoRecorrencia = 2 }));
        using var completedResponse = await _client.SendAsync(createCompleted);
        Assert.Equal(HttpStatusCode.Created, completedResponse.StatusCode);
        using var completedJson = JsonDocument.Parse(await completedResponse.Content.ReadAsStringAsync());
        var completedSeriesId = completedJson.RootElement.GetProperty("serieRecorrenciaId").GetInt32();
        Assert.True(completedJson.RootElement.GetProperty("concluido").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, completedJson.RootElement.GetProperty("dataConclusaoUtc").ValueKind);
        var completedSeriesItems = (await GetTodoItemsAsync(user.Token))
            .Where(item => item.GetProperty("serieRecorrenciaId").GetInt32() == completedSeriesId)
            .ToArray();
        Assert.Equal(new[] { 1, 2 }, completedSeriesItems.Select(item => item.GetProperty("numeroOcorrencia").GetInt32()).Order().ToArray());
    }

    [Fact]
    public async Task Completing_recurring_todo_creates_successor_once_and_reopening_does_not_cancel_it()
    {
        var user = await CreateUserAndLoginAsync($"recur-{Guid.NewGuid():N}");
        var created = await CreateRecurringTodoAsync(user.Token, "Daily", "Diaria", 3);
        var todoId = created.GetProperty("id").GetInt32();
        var seriesId = created.GetProperty("serieRecorrenciaId").GetInt32();
        var firstDue = created.GetProperty("dataPrevistaUtc").GetDateTime();

        using var complete = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{todoId}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Daily", concluido = true, frequenciaRecorrencia = "Diaria", duracaoRecorrencia = 3 }));
        using var completedResponse = await _client.SendAsync(complete);
        Assert.Equal(HttpStatusCode.OK, completedResponse.StatusCode);
        using var completedJson = JsonDocument.Parse(await completedResponse.Content.ReadAsStringAsync());
        var completionTime = completedJson.RootElement.GetProperty("dataConclusaoUtc").GetDateTime();

        var itemsAfterCompletion = await GetTodoItemsAsync(user.Token);
        Assert.Equal(2, itemsAfterCompletion.Count);
        var successor = itemsAfterCompletion.Single(item => item.GetProperty("numeroOcorrencia").GetInt32() == 2);
        Assert.Equal(seriesId, successor.GetProperty("serieRecorrenciaId").GetInt32());
        Assert.Equal(firstDue.AddDays(1), successor.GetProperty("dataPrevistaUtc").GetDateTime());
        Assert.False(successor.GetProperty("concluido").GetBoolean());

        using var repeatComplete = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{todoId}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Daily", concluido = true, frequenciaRecorrencia = "Diaria", duracaoRecorrencia = 3 }));
        using var repeatedResponse = await _client.SendAsync(repeatComplete);
        using var repeatedJson = JsonDocument.Parse(await repeatedResponse.Content.ReadAsStringAsync());
        Assert.Equal(completionTime, repeatedJson.RootElement.GetProperty("dataConclusaoUtc").GetDateTime());
        Assert.Equal(2, (await GetTodoItemsAsync(user.Token)).Count);

        using var reopen = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{todoId}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Daily", concluido = false, frequenciaRecorrencia = "Diaria", duracaoRecorrencia = 3 }));
        using var reopenedResponse = await _client.SendAsync(reopen);
        using var reopenedJson = JsonDocument.Parse(await reopenedResponse.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, reopenedJson.RootElement.GetProperty("dataConclusaoUtc").ValueKind);
        Assert.Equal(2, (await GetTodoItemsAsync(user.Token)).Count);

        using var completeAgain = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{todoId}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Daily", concluido = true, frequenciaRecorrencia = "Diaria", duracaoRecorrencia = 3 }));
        using var completedAgainResponse = await _client.SendAsync(completeAgain);
        using var completedAgainJson = JsonDocument.Parse(await completedAgainResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, completedAgainResponse.StatusCode);
        Assert.NotEqual(JsonValueKind.Null, completedAgainJson.RootElement.GetProperty("dataConclusaoUtc").ValueKind);
        var afterRecompletion = await GetTodoItemsAsync(user.Token);
        Assert.Equal(2, afterRecompletion.Count);
        Assert.DoesNotContain(afterRecompletion, item => item.GetProperty("numeroOcorrencia").GetInt32() == 3);
    }

    [Fact]
    public async Task Recurrence_can_convert_update_and_delete_occurrences_or_series()
    {
        var user = await CreateUserAndLoginAsync($"recur-edit-{Guid.NewGuid():N}");
        var regularTodo = await CreateTodoAsync(user.Token, "Standalone");
        var todoId = regularTodo.GetProperty("id").GetInt32();

        var beforeConversion = DateTime.UtcNow;
        using var convert = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{todoId}", user.Token,
            JsonContent.Create(new
            {
                titulo = "Tarefa",
                descricao = "Base semanal",
                concluido = false,
                frequenciaRecorrencia = "Semanal",
                duracaoRecorrencia = 4,
                dataPrevistaUtc = "2001-01-01T00:00:00Z",
                serieRecorrenciaId = 777,
                numeroOcorrencia = 999
            }));
        using var convertResponse = await _client.SendAsync(convert);
        var afterConversion = DateTime.UtcNow;
        Assert.Equal(HttpStatusCode.OK, convertResponse.StatusCode);
        using var convertedJson = JsonDocument.Parse(await convertResponse.Content.ReadAsStringAsync());
        var seriesId = convertedJson.RootElement.GetProperty("serieRecorrenciaId").GetInt32();
        var firstDue = convertedJson.RootElement.GetProperty("dataPrevistaUtc").GetDateTime();
        Assert.Equal(1, convertedJson.RootElement.GetProperty("numeroOcorrencia").GetInt32());
        Assert.InRange(firstDue, beforeConversion, afterConversion);

        var otherUser = await CreateUserAndLoginAsync($"recur-other-{Guid.NewGuid():N}");
        using var foreignSeriesDelete = AuthenticatedRequest(HttpMethod.Delete, $"/api/todo-series/{seriesId}", otherUser.Token);
        using var foreignSeriesDeleteResponse = await _client.SendAsync(foreignSeriesDelete);
        Assert.Equal(HttpStatusCode.NotFound, foreignSeriesDeleteResponse.StatusCode);

        using var completeFirst = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{todoId}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Base semanal", concluido = true, frequenciaRecorrencia = "Semanal", duracaoRecorrencia = 4 }));
        using var completeFirstResponse = await _client.SendAsync(completeFirst);
        var items = await GetTodoItemsAsync(user.Token);
        var second = items.Single(item => item.GetProperty("numeroOcorrencia").GetInt32() == 2);
        var secondId = second.GetProperty("id").GetInt32();
        var secondDue = second.GetProperty("dataPrevistaUtc").GetDateTime();
        Assert.Equal(firstDue.AddDays(7), secondDue);

        using var reduceTooFar = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{secondId}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Base mensal", concluido = false, frequenciaRecorrencia = "Mensal", duracaoRecorrencia = 1 }));
        using var rejectedReduction = await _client.SendAsync(reduceTooFar);
        Assert.Equal(HttpStatusCode.BadRequest, rejectedReduction.StatusCode);

        using var editSeries = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{secondId}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Base mensal", concluido = false, frequenciaRecorrencia = "Mensal", duracaoRecorrencia = 4 }));
        using var editSeriesResponse = await _client.SendAsync(editSeries);
        Assert.Equal(HttpStatusCode.OK, editSeriesResponse.StatusCode);
        using var editedJson = JsonDocument.Parse(await editSeriesResponse.Content.ReadAsStringAsync());
        Assert.Equal("Mensal", editedJson.RootElement.GetProperty("frequenciaRecorrencia").GetString());
        Assert.Equal(secondDue, editedJson.RootElement.GetProperty("dataPrevistaUtc").GetDateTime());

        using var completeSecond = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{secondId}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Base mensal", concluido = true, frequenciaRecorrencia = "Mensal", duracaoRecorrencia = 4 }));
        using var completeSecondResponse = await _client.SendAsync(completeSecond);
        var afterSecondCompletion = await GetTodoItemsAsync(user.Token);
        var third = afterSecondCompletion.Single(item => item.GetProperty("numeroOcorrencia").GetInt32() == 3);
        Assert.Equal(secondDue.AddMonths(1), third.GetProperty("dataPrevistaUtc").GetDateTime());
        Assert.Equal("Base mensal", third.GetProperty("descricao").GetString());
        Assert.Equal(seriesId, third.GetProperty("serieRecorrenciaId").GetInt32());

        using var noScopeDelete = AuthenticatedRequest(HttpMethod.Delete, $"/api/todos/{todoId}", user.Token);
        using var scopeRequiredResponse = await _client.SendAsync(noScopeDelete);
        Assert.Equal(HttpStatusCode.BadRequest, scopeRequiredResponse.StatusCode);

        using var invalidScopeDelete = AuthenticatedRequest(HttpMethod.Delete, $"/api/todos/{todoId}?scope=all", user.Token);
        using var invalidScopeResponse = await _client.SendAsync(invalidScopeDelete);
        Assert.Equal(HttpStatusCode.BadRequest, invalidScopeResponse.StatusCode);

        using var deleteOccurrence = AuthenticatedRequest(HttpMethod.Delete, $"/api/todos/{todoId}?scope=occurrence", user.Token);
        using var deleteOccurrenceResponse = await _client.SendAsync(deleteOccurrence);
        Assert.Equal(HttpStatusCode.NoContent, deleteOccurrenceResponse.StatusCode);
        Assert.DoesNotContain((await GetTodoItemsAsync(user.Token)), item => item.GetProperty("id").GetInt32() == todoId);
        Assert.Contains((await GetTodoItemsAsync(user.Token)), item => item.GetProperty("numeroOcorrencia").GetInt32() == 2);

        using var deleteSeries = AuthenticatedRequest(HttpMethod.Delete, $"/api/todo-series/{seriesId}", user.Token);
        using var deleteSeriesResponse = await _client.SendAsync(deleteSeries);
        Assert.Equal(HttpStatusCode.NoContent, deleteSeriesResponse.StatusCode);
        Assert.Empty(await GetTodoItemsAsync(user.Token));

        var secondSeriesTask = await CreateRecurringTodoAsync(user.Token, "Series", "Mensal", 2);
        var secondSeriesId = secondSeriesTask.GetProperty("serieRecorrenciaId").GetInt32();
        var secondSeriesTodoId = secondSeriesTask.GetProperty("id").GetInt32();
        using var deleteByScope = AuthenticatedRequest(HttpMethod.Delete, $"/api/todos/{secondSeriesTodoId}?scope=series", user.Token);
        using var deleteByScopeResponse = await _client.SendAsync(deleteByScope);
        Assert.Equal(HttpStatusCode.NoContent, deleteByScopeResponse.StatusCode);
        using var deletedSeriesEndpoint = AuthenticatedRequest(HttpMethod.Delete, $"/api/todo-series/{secondSeriesId}", user.Token);
        using var deletedSeriesResponse = await _client.SendAsync(deletedSeriesEndpoint);
        Assert.Equal(HttpStatusCode.NotFound, deletedSeriesResponse.StatusCode);

        var emptySeriesTodo = await CreateRecurringTodoAsync(user.Token, "Empty series", "Diaria", 2);
        var emptySeriesId = emptySeriesTodo.GetProperty("serieRecorrenciaId").GetInt32();
        var emptyOccurrenceId = emptySeriesTodo.GetProperty("id").GetInt32();
        using var removeLastOccurrence = AuthenticatedRequest(HttpMethod.Delete, $"/api/todos/{emptyOccurrenceId}?scope=occurrence", user.Token);
        using var removeLastOccurrenceResponse = await _client.SendAsync(removeLastOccurrence);
        Assert.Equal(HttpStatusCode.NoContent, removeLastOccurrenceResponse.StatusCode);
        using var cancelEmptySeries = AuthenticatedRequest(HttpMethod.Delete, $"/api/todo-series/{emptySeriesId}", user.Token);
        using var cancelEmptySeriesResponse = await _client.SendAsync(cancelEmptySeries);
        Assert.Equal(HttpStatusCode.NoContent, cancelEmptySeriesResponse.StatusCode);
    }

    [Fact]
    public void Monthly_recurrence_clamps_to_last_day_and_preserves_utc_time()
    {
        var januaryEnd = new DateTime(2025, 1, 31, 14, 30, 0, DateTimeKind.Utc);
        var leapJanuaryEnd = new DateTime(2024, 1, 31, 14, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2025, 2, 28, 14, 30, 0, DateTimeKind.Utc),
            TodoAPI.Domain.Services.RecurrenceSchedule.CalculateNextDueUtc(januaryEnd, TodoAPI.Domain.Entities.RecurrenceFrequency.Mensal));
        Assert.Equal(new DateTime(2024, 2, 29, 14, 30, 0, DateTimeKind.Utc),
            TodoAPI.Domain.Services.RecurrenceSchedule.CalculateNextDueUtc(leapJanuaryEnd, TodoAPI.Domain.Entities.RecurrenceFrequency.Mensal));
    }

    [Fact]
    public async Task Clearing_recurrence_configuration_preserves_existing_todo_and_stops_generation()
    {
        var user = await CreateUserAndLoginAsync($"recur-off-{Guid.NewGuid():N}");
        var created = await CreateRecurringTodoAsync(user.Token, "Recurring", "Diaria", 3);
        var id = created.GetProperty("id").GetInt32();
        var seriesId = created.GetProperty("serieRecorrenciaId").GetInt32();
        var due = created.GetProperty("dataPrevistaUtc").GetDateTime();

        using var disable = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{id}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Edited", concluido = false, frequenciaRecorrencia = (string?)null, duracaoRecorrencia = (int?)null }));
        using var disableResponse = await _client.SendAsync(disable);
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
        using var disabledJson = JsonDocument.Parse(await disableResponse.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, disabledJson.RootElement.GetProperty("frequenciaRecorrencia").ValueKind);
        Assert.Equal(JsonValueKind.Null, disabledJson.RootElement.GetProperty("duracaoRecorrencia").ValueKind);
        Assert.Equal(seriesId, disabledJson.RootElement.GetProperty("serieRecorrenciaId").GetInt32());
        Assert.Equal(1, disabledJson.RootElement.GetProperty("numeroOcorrencia").GetInt32());
        Assert.Equal(due, disabledJson.RootElement.GetProperty("dataPrevistaUtc").GetDateTime());

        using var completeDisabled = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{id}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Edited", concluido = true, frequenciaRecorrencia = (string?)null, duracaoRecorrencia = (int?)null }));
        using var completeDisabledResponse = await _client.SendAsync(completeDisabled);
        Assert.Equal(HttpStatusCode.OK, completeDisabledResponse.StatusCode);
        Assert.Single(await GetTodoItemsAsync(user.Token));

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
        var series = await dbContext.RecurrenceSeries.SingleAsync(item => item.Id == seriesId);
        Assert.False(series.IsActive);
        Assert.Null(series.Frequency);
        Assert.Null(series.Duration);
    }

    [Fact]
    public async Task Duration_equal_to_highest_generated_occurrence_ends_the_series()
    {
        var user = await CreateUserAndLoginAsync($"recur-end-{Guid.NewGuid():N}");
        var first = await CreateRecurringTodoAsync(user.Token, "Weekly", "Semanal", 3);
        var firstId = first.GetProperty("id").GetInt32();
        var seriesId = first.GetProperty("serieRecorrenciaId").GetInt32();

        using var completeFirst = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{firstId}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Weekly", concluido = true, frequenciaRecorrencia = "Semanal", duracaoRecorrencia = 3 }));
        using var completeFirstResponse = await _client.SendAsync(completeFirst);
        var second = (await GetTodoItemsAsync(user.Token)).Single(item => item.GetProperty("numeroOcorrencia").GetInt32() == 2);
        var secondId = second.GetProperty("id").GetInt32();

        using var endAfterSecond = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{secondId}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Weekly", concluido = false, frequenciaRecorrencia = "Semanal", duracaoRecorrencia = 2 }));
        using var endResponse = await _client.SendAsync(endAfterSecond);
        Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);

        using var completeSecond = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{secondId}", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Weekly", concluido = true, frequenciaRecorrencia = "Semanal", duracaoRecorrencia = 2 }));
        using var completeSecondResponse = await _client.SendAsync(completeSecond);
        Assert.Equal(HttpStatusCode.OK, completeSecondResponse.StatusCode);
        var items = await GetTodoItemsAsync(user.Token);
        Assert.Equal(2, items.Count);
        Assert.DoesNotContain(items, item => item.GetProperty("numeroOcorrencia").GetInt32() == 3);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
        var series = await dbContext.RecurrenceSeries.SingleAsync(item => item.Id == seriesId);
        Assert.False(series.IsActive);
        Assert.Equal(2, series.Duration);
    }

    [Fact]
    public async Task Category_crud_is_user_scoped_and_categories_in_use_cannot_be_deleted()
    {
        var owner = await CreateUserAndLoginAsync($"category-owner-{Guid.NewGuid():N}");
        var otherUser = await CreateUserAndLoginAsync($"category-other-{Guid.NewGuid():N}");

        using var ownerCategoriesRequest = AuthenticatedRequest(HttpMethod.Get, "/api/categories", owner.Token);
        using var ownerCategoriesResponse = await _client.SendAsync(ownerCategoriesRequest);
        Assert.Equal(HttpStatusCode.OK, ownerCategoriesResponse.StatusCode);
        using var ownerCategoriesJson = JsonDocument.Parse(await ownerCategoriesResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            new[] { "Estudos", "Reunião", "Trabalho", "Pessoal" },
            ownerCategoriesJson.RootElement.EnumerateArray().Select(category => category.GetProperty("nome").GetString()).ToArray());

        var longName = new string('C', 300);
        using var createCategory = AuthenticatedRequest(HttpMethod.Post, "/api/categories", owner.Token,
            JsonContent.Create(new { nome = longName }));
        using var createCategoryResponse = await _client.SendAsync(createCategory);
        Assert.Equal(HttpStatusCode.Created, createCategoryResponse.StatusCode);
        using var createdCategoryJson = JsonDocument.Parse(await createCategoryResponse.Content.ReadAsStringAsync());
        var categoryId = createdCategoryJson.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(longName, createdCategoryJson.RootElement.GetProperty("nome").GetString());

        using var otherGet = AuthenticatedRequest(HttpMethod.Get, $"/api/categories/{categoryId}", otherUser.Token);
        using var otherGetResponse = await _client.SendAsync(otherGet);
        Assert.Equal(HttpStatusCode.NotFound, otherGetResponse.StatusCode);

        using var otherUpdate = AuthenticatedRequest(HttpMethod.Put, $"/api/categories/{categoryId}", otherUser.Token,
            JsonContent.Create(new { nome = "Acesso indevido" }));
        using var otherUpdateResponse = await _client.SendAsync(otherUpdate);
        Assert.Equal(HttpStatusCode.NotFound, otherUpdateResponse.StatusCode);

        using var createTodo = AuthenticatedRequest(HttpMethod.Post, "/api/todos", owner.Token,
            JsonContent.Create(new { titulo = "Relatório", descricao = "Escrever relatório", categoriaId = categoryId }));
        using var createTodoResponse = await _client.SendAsync(createTodo);
        Assert.Equal(HttpStatusCode.Created, createTodoResponse.StatusCode);
        using var todoJson = JsonDocument.Parse(await createTodoResponse.Content.ReadAsStringAsync());
        Assert.Equal("Relatório", todoJson.RootElement.GetProperty("titulo").GetString());
        Assert.Equal("Escrever relatório", todoJson.RootElement.GetProperty("descricao").GetString());
        Assert.Equal(categoryId, todoJson.RootElement.GetProperty("categoriaId").GetInt32());
        Assert.Equal(longName, todoJson.RootElement.GetProperty("categoriaNome").GetString());
        Assert.Equal("Media", todoJson.RootElement.GetProperty("prioridade").GetString());

        using var foreignCategoryOnTodo = AuthenticatedRequest(HttpMethod.Post, "/api/todos", otherUser.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "Categoria alheia", categoriaId = categoryId }));
        using var foreignCategoryResponse = await _client.SendAsync(foreignCategoryOnTodo);
        Assert.Equal(HttpStatusCode.BadRequest, foreignCategoryResponse.StatusCode);

        using var deleteInUse = AuthenticatedRequest(HttpMethod.Delete, $"/api/categories/{categoryId}", owner.Token);
        using var deleteInUseResponse = await _client.SendAsync(deleteInUse);
        Assert.Equal(HttpStatusCode.Conflict, deleteInUseResponse.StatusCode);
        Assert.Equal("application/problem+json", deleteInUseResponse.Content.Headers.ContentType?.MediaType);

        using var deleteTodo = AuthenticatedRequest(HttpMethod.Delete,
            $"/api/todos/{todoJson.RootElement.GetProperty("id").GetInt32()}", owner.Token);
        using var deleteTodoResponse = await _client.SendAsync(deleteTodo);
        Assert.Equal(HttpStatusCode.NoContent, deleteTodoResponse.StatusCode);

        using var updateCategory = AuthenticatedRequest(HttpMethod.Put, $"/api/categories/{categoryId}", owner.Token,
            JsonContent.Create(new { nome = "Relatórios" }));
        using var updateCategoryResponse = await _client.SendAsync(updateCategory);
        Assert.Equal(HttpStatusCode.OK, updateCategoryResponse.StatusCode);

        using var deleteCategory = AuthenticatedRequest(HttpMethod.Delete, $"/api/categories/{categoryId}", owner.Token);
        using var deleteCategoryResponse = await _client.SendAsync(deleteCategory);
        Assert.Equal(HttpStatusCode.NoContent, deleteCategoryResponse.StatusCode);
    }

    [Fact]
    public async Task Todo_title_and_priority_are_validated_and_recurrence_changes_only_future_occurrences()
    {
        var user = await CreateUserAndLoginAsync($"title-recurrence-{Guid.NewGuid():N}");
        var categories = await GetCategoriesAsync(user.Token);
        var firstCategoryId = categories[0].GetProperty("id").GetInt32();
        var secondCategoryId = categories[1].GetProperty("id").GetInt32();

        using var missingTitle = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new { descricao = "Sem título" }));
        using var missingTitleResponse = await _client.SendAsync(missingTitle);
        Assert.Equal(HttpStatusCode.BadRequest, missingTitleResponse.StatusCode);

        using var blankTitle = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new { titulo = "   ", descricao = "Título em branco" }));
        using var blankTitleResponse = await _client.SendAsync(blankTitle);
        Assert.Equal(HttpStatusCode.BadRequest, blankTitleResponse.StatusCode);

        using var longTitle = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new { titulo = new string('T', 201), descricao = "Título longo" }));
        using var longTitleResponse = await _client.SendAsync(longTitle);
        Assert.Equal(HttpStatusCode.BadRequest, longTitleResponse.StatusCode);

        using var invalidPriority = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new { titulo = "Prioridade", descricao = "Inválida", prioridade = "Urgente" }));
        using var invalidPriorityResponse = await _client.SendAsync(invalidPriority);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPriorityResponse.StatusCode);

        using var createSeries = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new
            {
                titulo = "Título inicial",
                descricao = "Descrição inicial",
                categoriaId = firstCategoryId,
                prioridade = "Alta",
                frequenciaRecorrencia = "Diaria",
                duracaoRecorrencia = 4
            }));
        using var createSeriesResponse = await _client.SendAsync(createSeries);
        Assert.Equal(HttpStatusCode.Created, createSeriesResponse.StatusCode);
        using var initialJson = JsonDocument.Parse(await createSeriesResponse.Content.ReadAsStringAsync());
        var initialId = initialJson.RootElement.GetProperty("id").GetInt32();
        var seriesId = initialJson.RootElement.GetProperty("serieRecorrenciaId").GetInt32();
        Assert.Equal("Título inicial", initialJson.RootElement.GetProperty("titulo").GetString());
        Assert.Equal("Alta", initialJson.RootElement.GetProperty("prioridade").GetString());
        Assert.Equal("Estudos", initialJson.RootElement.GetProperty("categoriaNome").GetString());

        using var completeInitial = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{initialId}", user.Token,
            JsonContent.Create(new
            {
                titulo = "Título inicial",
                descricao = "Descrição inicial",
                categoriaId = firstCategoryId,
                prioridade = "Alta",
                concluido = true,
                frequenciaRecorrencia = "Diaria",
                duracaoRecorrencia = 4
            }));
        using var completeInitialResponse = await _client.SendAsync(completeInitial);
        Assert.Equal(HttpStatusCode.OK, completeInitialResponse.StatusCode);
        var beforeEdit = await GetTodoItemsAsync(user.Token);
        var second = beforeEdit.Single(item => item.GetProperty("numeroOcorrencia").GetInt32() == 2);
        var secondId = second.GetProperty("id").GetInt32();
        Assert.Equal("Título inicial", second.GetProperty("titulo").GetString());
        Assert.Equal("Estudos", second.GetProperty("categoriaNome").GetString());
        Assert.Equal("Alta", second.GetProperty("prioridade").GetString());

        using var editFutureDefaults = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{initialId}", user.Token,
            JsonContent.Create(new
            {
                titulo = "Título futuro",
                descricao = "Descrição futura",
                categoriaId = secondCategoryId,
                prioridade = "Baixa",
                concluido = true,
                frequenciaRecorrencia = "Diaria",
                duracaoRecorrencia = 4
            }));
        using var editFutureResponse = await _client.SendAsync(editFutureDefaults);
        Assert.Equal(HttpStatusCode.OK, editFutureResponse.StatusCode);

        var afterEdit = await GetTodoItemsAsync(user.Token);
        var unchangedSecond = afterEdit.Single(item => item.GetProperty("id").GetInt32() == secondId);
        Assert.Equal("Título inicial", unchangedSecond.GetProperty("titulo").GetString());
        Assert.Equal("Descrição inicial", unchangedSecond.GetProperty("descricao").GetString());
        Assert.Equal(firstCategoryId, unchangedSecond.GetProperty("categoriaId").GetInt32());
        Assert.Equal("Alta", unchangedSecond.GetProperty("prioridade").GetString());

        using var completeSecond = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{secondId}", user.Token,
            JsonContent.Create(new
            {
                titulo = "Título futuro",
                descricao = "Descrição futura",
                categoriaId = secondCategoryId,
                prioridade = "Baixa",
                concluido = true,
                frequenciaRecorrencia = "Diaria",
                duracaoRecorrencia = 4
            }));
        using var completeSecondResponse = await _client.SendAsync(completeSecond);
        Assert.Equal(HttpStatusCode.OK, completeSecondResponse.StatusCode);
        var afterSuccessor = await GetTodoItemsAsync(user.Token);
        var third = afterSuccessor.Single(item => item.GetProperty("numeroOcorrencia").GetInt32() == 3);
        Assert.Equal("Título futuro", third.GetProperty("titulo").GetString());
        Assert.Equal("Descrição futura", third.GetProperty("descricao").GetString());
        Assert.Equal(secondCategoryId, third.GetProperty("categoriaId").GetInt32());
        Assert.Equal("Reunião", third.GetProperty("categoriaNome").GetString());
        Assert.Equal("Baixa", third.GetProperty("prioridade").GetString());

        using var deleteReferencedCategory = AuthenticatedRequest(HttpMethod.Delete, $"/api/categories/{secondCategoryId}", user.Token);
        using var referencedCategoryResponse = await _client.SendAsync(deleteReferencedCategory);
        Assert.Equal(HttpStatusCode.Conflict, referencedCategoryResponse.StatusCode);

        using var cancelSeries = AuthenticatedRequest(HttpMethod.Delete, $"/api/todo-series/{seriesId}", user.Token);
        using var cancelSeriesResponse = await _client.SendAsync(cancelSeries);
        Assert.Equal(HttpStatusCode.NoContent, cancelSeriesResponse.StatusCode);

        using var deleteUnusedCategory = AuthenticatedRequest(HttpMethod.Delete, $"/api/categories/{secondCategoryId}", user.Token);
        using var unusedCategoryResponse = await _client.SendAsync(deleteUnusedCategory);
        Assert.Equal(HttpStatusCode.NoContent, unusedCategoryResponse.StatusCode);
    }

    [Fact]
    public async Task Scheduling_preferences_are_scoped_and_todo_windows_normalize_to_utc_and_reject_overlap()
    {
        var owner = await CreateUserAndLoginAsync($"schedule-owner-{Guid.NewGuid():N}");
        var other = await CreateUserAndLoginAsync($"schedule-other-{Guid.NewGuid():N}");

        using var defaultPreference = AuthenticatedRequest(HttpMethod.Get, "/api/preferences", owner.Token);
        using var defaultResponse = await _client.SendAsync(defaultPreference);
        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
        using var defaultJson = JsonDocument.Parse(await defaultResponse.Content.ReadAsStringAsync());
        Assert.Equal("America/Fortaleza", defaultJson.RootElement.GetProperty("timeZoneId").GetString());

        using var updatePreference = AuthenticatedRequest(HttpMethod.Put, "/api/preferences", owner.Token,
            JsonContent.Create(new { timeZoneId = "America/New_York" }));
        using var updateResponse = await _client.SendAsync(updatePreference);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var otherPreference = AuthenticatedRequest(HttpMethod.Get, "/api/preferences", other.Token);
        using var otherResponse = await _client.SendAsync(otherPreference);
        using var otherJson = JsonDocument.Parse(await otherResponse.Content.ReadAsStringAsync());
        Assert.Equal("America/Fortaleza", otherJson.RootElement.GetProperty("timeZoneId").GetString());

        using var invalidPreference = AuthenticatedRequest(HttpMethod.Put, "/api/preferences", owner.Token,
            JsonContent.Create(new { timeZoneId = "Not/A_Real_Zone" }));
        using var invalidResponse = await _client.SendAsync(invalidPreference);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        using var partial = AuthenticatedRequest(HttpMethod.Post, "/api/todos", owner.Token,
            JsonContent.Create(new { titulo = "Janela", descricao = "Parcial", dataInicioUtc = "2026-11-09T09:00:00-05:00" }));
        using var partialResponse = await _client.SendAsync(partial);
        Assert.Equal(HttpStatusCode.BadRequest, partialResponse.StatusCode);

        using var timestampWithoutOffset = AuthenticatedRequest(HttpMethod.Post, "/api/todos", owner.Token,
            JsonContent.Create(new
            {
                titulo = "Sem fuso", descricao = "ISO sem designador", dataInicioUtc = "2026-11-09T09:00:00",
                dataFimUtc = "2026-11-09T10:00:00"
            }));
        using var timestampWithoutOffsetResponse = await _client.SendAsync(timestampWithoutOffset);
        Assert.Equal(HttpStatusCode.BadRequest, timestampWithoutOffsetResponse.StatusCode);

        using var spansLocalDays = AuthenticatedRequest(HttpMethod.Post, "/api/todos", owner.Token,
            JsonContent.Create(new
            {
                titulo = "Virada local", descricao = "Não suportado", dataInicioUtc = "2026-11-09T23:30:00-05:00",
                dataFimUtc = "2026-11-10T00:30:00-05:00"
            }));
        using var spansLocalDaysResponse = await _client.SendAsync(spansLocalDays);
        Assert.Equal(HttpStatusCode.BadRequest, spansLocalDaysResponse.StatusCode);

        using var firstWindow = AuthenticatedRequest(HttpMethod.Post, "/api/todos", owner.Token,
            JsonContent.Create(new
            {
                titulo = "Janela 1", descricao = "Primeiro", dataInicioUtc = "2026-11-09T09:00:00-05:00",
                dataFimUtc = "2026-11-09T10:00:00-05:00"
            }));
        using var firstResponse = await _client.SendAsync(firstWindow);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        using var firstJson = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        Assert.Equal(DateTimeOffset.Parse("2026-11-09T14:00:00Z"), firstJson.RootElement.GetProperty("dataInicioUtc").GetDateTimeOffset());
        Assert.Equal(DateTimeOffset.Parse("2026-11-09T15:00:00Z"), firstJson.RootElement.GetProperty("dataFimUtc").GetDateTimeOffset());

        using var adjacent = AuthenticatedRequest(HttpMethod.Post, "/api/todos", owner.Token,
            JsonContent.Create(new
            {
                titulo = "Adjacente", descricao = "Fronteira permitida", dataInicioUtc = "2026-11-09T15:00:00Z",
                dataFimUtc = "2026-11-09T16:00:00Z"
            }));
        using var adjacentResponse = await _client.SendAsync(adjacent);
        Assert.Equal(HttpStatusCode.Created, adjacentResponse.StatusCode);

        using var overlap = AuthenticatedRequest(HttpMethod.Post, "/api/todos", owner.Token,
            JsonContent.Create(new
            {
                titulo = "Conflito", descricao = "Sobreposição", dataInicioUtc = "2026-11-09T09:30:00-05:00",
                dataFimUtc = "2026-11-09T10:30:00-05:00"
            }));
        using var overlapResponse = await _client.SendAsync(overlap);
        Assert.Equal(HttpStatusCode.Conflict, overlapResponse.StatusCode);

        var items = await GetTodoItemsAsync(owner.Token);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Recurring_schedule_projection_blocks_future_overlap_and_preserves_occurrence_windows()
    {
        var user = await CreateUserAndLoginAsync($"schedule-series-{Guid.NewGuid():N}");
        using var createSeries = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new
            {
                titulo = "Série", descricao = "Diária", frequenciaRecorrencia = "Diaria", duracaoRecorrencia = 3,
                dataInicioUtc = "2026-12-01T09:00:00-03:00", dataFimUtc = "2026-12-01T10:00:00-03:00"
            }));
        using var seriesResponse = await _client.SendAsync(createSeries);
        Assert.Equal(HttpStatusCode.Created, seriesResponse.StatusCode);
        using var firstJson = JsonDocument.Parse(await seriesResponse.Content.ReadAsStringAsync());
        var firstId = firstJson.RootElement.GetProperty("id").GetInt32();
        var firstStart = firstJson.RootElement.GetProperty("dataInicioUtc").GetDateTime();
        Assert.Equal(DateTimeKind.Utc, firstStart.Kind);

        using var overlapping = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new
            {
                titulo = "Conflito futuro", descricao = "Colide com #2", dataInicioUtc = "2026-12-02T09:30:00-03:00",
                dataFimUtc = "2026-12-02T10:30:00-03:00"
            }));
        using var overlapResponse = await _client.SendAsync(overlapping);
        Assert.Equal(HttpStatusCode.Conflict, overlapResponse.StatusCode);

        using var complete = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{firstId}", user.Token,
            JsonContent.Create(new
            {
                titulo = "Série", descricao = "Diária", concluido = true, frequenciaRecorrencia = "Diaria", duracaoRecorrencia = 3,
                dataInicioUtc = "2026-12-01T09:00:00-03:00", dataFimUtc = "2026-12-01T10:00:00-03:00"
            }));
        using var completeResponse = await _client.SendAsync(complete);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var items = await GetTodoItemsAsync(user.Token);
        var second = Assert.Single(items, x => x.GetProperty("numeroOcorrencia").GetInt32() == 2);
        Assert.Equal(DateTime.Parse("2026-12-02T12:00:00Z").ToUniversalTime(), second.GetProperty("dataInicioUtc").GetDateTime());
    }

    [Fact]
    public void Scheduling_projection_resolves_dst_and_month_end_from_anchor()
    {
        var monthly = new TodoAPI.Domain.Entities.RecurrenceSeries
        {
            UserId = 1, DescriptionBase = "x", TitleBase = "x", Frequency = TodoAPI.Domain.Entities.RecurrenceFrequency.Mensal,
            Duration = 3, IsActive = true, NextOccurrenceNumber = 2, LastOccurrenceDueUtc = DateTime.UtcNow,
            NextDataPrevistaUtc = DateTime.UtcNow, ScheduleAnchorLocalDate = new DateOnly(2027, 1, 31),
            ScheduleStartLocalTime = new TimeOnly(9, 0), ScheduleEndLocalTime = new TimeOnly(10, 0)
        };
        Assert.Equal(new DateOnly(2027, 3, 31), DateOnly.FromDateTime(SchedulingService.Project(monthly, "America/New_York", 3).StartUtc));

        monthly.Frequency = TodoAPI.Domain.Entities.RecurrenceFrequency.Diaria;
        monthly.ScheduleAnchorLocalDate = new DateOnly(2027, 3, 13);
        monthly.ScheduleStartLocalTime = new TimeOnly(2, 30);
        monthly.ScheduleEndLocalTime = new TimeOnly(4, 30);
        var spring = SchedulingService.Project(monthly, "America/New_York", 2);
        Assert.Equal(DateTime.Parse("2027-03-14T07:30:00Z").ToUniversalTime(), spring.StartUtc);
        Assert.Equal(DateTime.Parse("2027-03-14T08:30:00Z").ToUniversalTime(), spring.EndUtc);

        monthly.ScheduleAnchorLocalDate = new DateOnly(2027, 11, 6);
        monthly.ScheduleStartLocalTime = new TimeOnly(1, 30);
        monthly.ScheduleEndLocalTime = new TimeOnly(2, 30);
        var fall = SchedulingService.Project(monthly, "America/New_York", 2);
        Assert.Equal(DateTime.Parse("2027-11-07T05:30:00Z").ToUniversalTime(), fall.StartUtc);
    }

    [Fact]
    public async Task Recurrence_worker_deactivates_and_does_not_persist_unexpected_schedule_conflict()
    {
        var user = await CreateUserAndLoginAsync($"schedule-worker-{Guid.NewGuid():N}");
        using var request = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new
            {
                titulo = "Série worker", descricao = "Diária", frequenciaRecorrencia = "Diaria", duracaoRecorrencia = 2,
                dataInicioUtc = "2026-12-01T09:00:00-03:00", dataFimUtc = "2026-12-01T10:00:00-03:00"
            }));
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var seriesId = json.RootElement.GetProperty("serieRecorrenciaId").GetInt32();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
            var series = await db.RecurrenceSeries.SingleAsync(x => x.Id == seriesId);
            series.NextDataPrevistaUtc = DateTime.UtcNow.AddMinutes(-1);
            var userId = await db.Users.Where(x => x.Username == user.Username).Select(x => x.Id).SingleAsync();
            db.Todos.Add(new TodoAPI.Domain.Entities.TodoItem
            {
                UserId = userId,
                Titulo = "Interferência", Descricao = "Inserida fora da API", DataCriacao = DateTime.UtcNow,
                DataInicioUtc = DateTime.Parse("2026-12-02T12:30:00Z").ToUniversalTime(),
                DataFimUtc = DateTime.Parse("2026-12-02T13:30:00Z").ToUniversalTime()
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var recurrence = scope.ServiceProvider.GetRequiredService<RecurrenceService>();
            Assert.Equal(0, await recurrence.ProcessDueAsync(DateTime.UtcNow));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
            var series = await db.RecurrenceSeries.SingleAsync(x => x.Id == seriesId);
            Assert.False(series.IsActive);
            Assert.Equal(2, await db.Todos.CountAsync(x => x.UserId == series.UserId));
        }
    }

    [Fact]
    public async Task Editing_second_occurrence_window_anchors_future_projection_to_occurrence_two()
    {
        var user = await CreateUserAndLoginAsync($"schedule-anchor-{Guid.NewGuid():N}");
        using var create = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new
            {
                titulo = "Série", descricao = "Diária", frequenciaRecorrencia = "Diaria", duracaoRecorrencia = 3,
                dataInicioUtc = "2026-12-01T09:00:00-03:00", dataFimUtc = "2026-12-01T10:00:00-03:00"
            }));
        using var createResponse = await _client.SendAsync(create);
        using var firstJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var firstId = firstJson.RootElement.GetProperty("id").GetInt32();
        using var completeFirst = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{firstId}", user.Token,
            JsonContent.Create(new
            {
                titulo = "Série", descricao = "Diária", concluido = true, frequenciaRecorrencia = "Diaria", duracaoRecorrencia = 3,
                dataInicioUtc = "2026-12-01T09:00:00-03:00", dataFimUtc = "2026-12-01T10:00:00-03:00"
            }));
        using var completeFirstResponse = await _client.SendAsync(completeFirst);
        Assert.Equal(HttpStatusCode.OK, completeFirstResponse.StatusCode);
        var afterFirst = await GetTodoItemsAsync(user.Token);
        var second = Assert.Single(afterFirst, item => item.GetProperty("numeroOcorrencia").GetInt32() == 2);
        var secondId = second.GetProperty("id").GetInt32();

        using var editAndCompleteSecond = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{secondId}", user.Token,
            JsonContent.Create(new
            {
                titulo = "Série", descricao = "Diária", concluido = true, frequenciaRecorrencia = "Diaria", duracaoRecorrencia = 3,
                dataInicioUtc = "2026-12-05T11:00:00-03:00", dataFimUtc = "2026-12-05T12:00:00-03:00"
            }));
        using var secondResponse = await _client.SendAsync(editAndCompleteSecond);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var afterSecond = await GetTodoItemsAsync(user.Token);
        var third = Assert.Single(afterSecond, item => item.GetProperty("numeroOcorrencia").GetInt32() == 3);
        Assert.Equal(DateTime.Parse("2026-12-06T14:00:00Z").ToUniversalTime(), third.GetProperty("dataInicioUtc").GetDateTime());
        Assert.Equal(DateTime.Parse("2026-12-06T15:00:00Z").ToUniversalTime(), third.GetProperty("dataFimUtc").GetDateTime());
    }

    [Fact]
    public async Task Converting_completed_standalone_todo_to_scheduled_series_generates_successor_with_window()
    {
        var user = await CreateUserAndLoginAsync($"schedule-convert-{Guid.NewGuid():N}");
        var standalone = await CreateTodoAsync(user.Token, "Avulsa");
        var todoId = standalone.GetProperty("id").GetInt32();
        using var convert = AuthenticatedRequest(HttpMethod.Put, $"/api/todos/{todoId}", user.Token,
            JsonContent.Create(new
            {
                titulo = "Converter", descricao = "Recorrente", concluido = true,
                frequenciaRecorrencia = "Diaria", duracaoRecorrencia = 2,
                dataInicioUtc = "2026-12-01T09:00:00-03:00", dataFimUtc = "2026-12-01T10:00:00-03:00"
            }));
        using var response = await _client.SendAsync(convert);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await GetTodoItemsAsync(user.Token);
        var successor = Assert.Single(items, item => item.GetProperty("numeroOcorrencia").GetInt32() == 2);
        Assert.Equal(DateTime.Parse("2026-12-02T12:00:00Z").ToUniversalTime(), successor.GetProperty("dataInicioUtc").GetDateTime());
        Assert.Equal(DateTime.Parse("2026-12-02T13:00:00Z").ToUniversalTime(), successor.GetProperty("dataFimUtc").GetDateTime());
    }

    [Fact]
    public async Task Recurrence_catch_up_is_bounded_and_never_reuses_deleted_occurrence_numbers()
    {
        var user = await CreateUserAndLoginAsync($"recur-catch-{Guid.NewGuid():N}");
        var first = await CreateRecurringTodoAsync(user.Token, "Catch up", "Diaria", 4);
        var seriesId = first.GetProperty("serieRecorrenciaId").GetInt32();
        var firstId = first.GetProperty("id").GetInt32();

        using var deleteFirst = AuthenticatedRequest(HttpMethod.Delete, $"/api/todos/{firstId}?scope=occurrence", user.Token);
        using var deleteResponse = await _client.SendAsync(deleteFirst);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
            var series = await dbContext.RecurrenceSeries.SingleAsync(candidate => candidate.Id == seriesId);
            series.NextDataPrevistaUtc = DateTime.UtcNow.AddDays(-3);
            await dbContext.SaveChangesAsync();

            var recurrenceService = scope.ServiceProvider.GetRequiredService<RecurrenceService>();
            await recurrenceService.ProcessDueAsync(DateTime.UtcNow);
        }

        var generated = await GetTodoItemsAsync(user.Token);
        Assert.Equal(3, generated.Count);
        Assert.Equal(new[] { 2, 3, 4 }, generated.Select(item => item.GetProperty("numeroOcorrencia").GetInt32()).Order().ToArray());
        Assert.DoesNotContain(generated, item => item.GetProperty("numeroOcorrencia").GetInt32() == 1);

        using (var scope = _factory.Services.CreateScope())
        {
            var recurrenceService = scope.ServiceProvider.GetRequiredService<RecurrenceService>();
            await recurrenceService.ProcessDueAsync(DateTime.UtcNow);
        }

        var afterRepeat = await GetTodoItemsAsync(user.Token);
        Assert.Equal(3, afterRepeat.Count);
        Assert.All(afterRepeat, item => Assert.InRange(item.GetProperty("numeroOcorrencia").GetInt32(), 2, 4));
    }

    [Fact]
    public async Task Todo_endpoints_require_auth_and_validate_description_and_pagination()
    {
        using var unauthorized = await _client.GetAsync("/api/todos");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var user = await CreateUserAndLoginAsync($"validation-{Guid.NewGuid():N}");
        using var tooLong = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = new string('x', 201) }));
        using var tooLongResponse = await _client.SendAsync(tooLong);
        Assert.Equal(HttpStatusCode.BadRequest, tooLongResponse.StatusCode);
        Assert.Equal("application/problem+json", tooLongResponse.Content.Headers.ContentType?.MediaType);

        using var blankDescription = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = "   " }));
        using var blankDescriptionResponse = await _client.SendAsync(blankDescription);
        Assert.Equal(HttpStatusCode.BadRequest, blankDescriptionResponse.StatusCode);

        using var invalidPageSize = AuthenticatedRequest(HttpMethod.Get, "/api/todos?page=1&pageSize=16", user.Token);
        using var invalidPageSizeResponse = await _client.SendAsync(invalidPageSize);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageSizeResponse.StatusCode);

        using var invalidPage = AuthenticatedRequest(HttpMethod.Get, "/api/todos?page=0", user.Token);
        using var invalidPageResponse = await _client.SendAsync(invalidPage);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageResponse.StatusCode);

        for (var i = 0; i < 16; i++)
        {
            using var create = AuthenticatedRequest(HttpMethod.Post, "/api/todos", user.Token,
                JsonContent.Create(new { titulo = "Tarefa", descricao = $"Task {i}" }));
            using var response = await _client.SendAsync(create);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        using var firstPage = AuthenticatedRequest(HttpMethod.Get, "/api/todos", user.Token);
        using var firstPageResponse = await _client.SendAsync(firstPage);
        Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);
        using var firstJson = JsonDocument.Parse(await firstPageResponse.Content.ReadAsStringAsync());
        Assert.Equal(15, firstJson.RootElement.GetProperty("items").GetArrayLength());

        using var secondPage = AuthenticatedRequest(HttpMethod.Get, "/api/todos?page=2&pageSize=15", user.Token);
        using var secondPageResponse = await _client.SendAsync(secondPage);
        using var secondJson = JsonDocument.Parse(await secondPageResponse.Content.ReadAsStringAsync());
        Assert.Single(secondJson.RootElement.GetProperty("items").EnumerateArray());

        using var beyondEnd = AuthenticatedRequest(HttpMethod.Get, "/api/todos?page=3&pageSize=15", user.Token);
        using var beyondEndResponse = await _client.SendAsync(beyondEnd);
        using var beyondEndJson = JsonDocument.Parse(await beyondEndResponse.Content.ReadAsStringAsync());
        Assert.Empty(beyondEndJson.RootElement.GetProperty("items").EnumerateArray());
    }

    private async Task<(string Token, string Username)> CreateUserAndLoginAsync(string username)
    {
        var password = "A-secure-password-123!";
        using var register = await _client.PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        using var login = await _client.PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return (json.RootElement.GetProperty("token").GetString()!, username);
    }

    private async Task<JsonElement> CreateTodoAsync(string token, string description)
    {
        using var request = AuthenticatedRequest(HttpMethod.Post, "/api/todos", token,
            JsonContent.Create(new { titulo = "Tarefa", descricao = description }));
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private async Task<JsonElement> CreateRecurringTodoAsync(string token, string description, string frequency, int duration)
    {
        using var request = AuthenticatedRequest(HttpMethod.Post, "/api/todos", token,
            JsonContent.Create(new
            {
                titulo = "Tarefa",
                descricao = description,
                frequenciaRecorrencia = frequency,
                duracaoRecorrencia = duration
            }));
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private async Task<List<JsonElement>> GetTodoItemsAsync(string token, string query = "")
    {
        using var request = AuthenticatedRequest(HttpMethod.Get, $"/api/todos{query}", token);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("items").EnumerateArray().Select(item => item.Clone()).ToList();
    }

    private async Task<List<JsonElement>> GetCategoriesAsync(string token)
    {
        using var request = AuthenticatedRequest(HttpMethod.Get, "/api/categories", token);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray().Select(item => item.Clone()).ToList();
    }

    private static HttpRequestMessage AuthenticatedRequest(HttpMethod method, string uri, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}

public sealed class TodoApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"todo-api-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:TodoDatabase"] = $"Data Source={_databasePath}",
                ["Jwt:Issuer"] = "TodoApi.Tests",
                ["Jwt:Audience"] = "TodoApi.Tests.Client",
                ["Jwt:SigningKey"] = "test-signing-key-that-is-at-least-32-characters-long"
            }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        if (disposing && File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
