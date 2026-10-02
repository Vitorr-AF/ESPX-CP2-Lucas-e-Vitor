using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ExpenseHub.Api.Endpoints;

internal static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/expenses").WithTags("Expenses");

        group.MapPost("/", CreateAsync).WithName("CreateExpense").RequireAuthorization(AuthPolicies.Employee);
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateExpense").RequireAuthorization(AuthPolicies.Employee);
        group.MapGet("/", ListAsync).WithName("ListExpenses").RequireAuthorization(AuthPolicies.ExpenseReader);
        group.MapGet("/{id:guid}", GetAsync).WithName("GetExpense").RequireAuthorization(AuthPolicies.ExpenseReader);
        group.MapPost("/{id:guid}/submit", SubmitAsync).WithName("SubmitExpense").RequireAuthorization(AuthPolicies.Employee);
        group.MapPost("/{id:guid}/approve", ApproveAsync).WithName("ApproveExpense").RequireAuthorization(AuthPolicies.Approver);
        group.MapPost("/{id:guid}/reject", RejectAsync).WithName("RejectExpense").RequireAuthorization(AuthPolicies.Approver);
        group.MapPost("/{id:guid}/pay", PayAsync).WithName("PayExpense").RequireAuthorization(AuthPolicies.Finance);
        group.MapGet("/{id:guid}/history", GetHistoryAsync).WithName("GetExpenseHistory").RequireAuthorization(AuthPolicies.ExpenseReader);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        ExpenseRequest request,
        ClaimsPrincipal principal,
        ExpenseService service,
        CancellationToken cancellationToken)
    {
        ExpenseResponse created = await service.CreateAsync(principal.ToUserContext(), request, cancellationToken);

        return TypedResults.Created($"/api/expenses/{created.Id}", created);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        ExpenseRequest request,
        ClaimsPrincipal principal,
        ExpenseService service,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.UpdateAsync(principal.ToUserContext(), id, request, cancellationToken));
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal principal, ExpenseService service, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.ListAsync(principal.ToUserContext(), cancellationToken));
    }

    private static async Task<IResult> GetAsync(Guid id, ClaimsPrincipal principal, ExpenseService service, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.GetAsync(principal.ToUserContext(), id, cancellationToken));
    }

    private static async Task<IResult> SubmitAsync(Guid id, ClaimsPrincipal principal, ExpenseService service, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.SubmitAsync(principal.ToUserContext(), id, cancellationToken));
    }

    private static async Task<IResult> ApproveAsync(Guid id, ClaimsPrincipal principal, ExpenseService service, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.ApproveAsync(principal.ToUserContext(), id, cancellationToken));
    }

    private static async Task<IResult> RejectAsync(
        Guid id,
        RejectExpenseRequest request,
        ClaimsPrincipal principal,
        ExpenseService service,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.RejectAsync(principal.ToUserContext(), id, request, cancellationToken));
    }

    private static async Task<IResult> PayAsync(Guid id, ClaimsPrincipal principal, ExpenseService service, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.PayAsync(principal.ToUserContext(), id, cancellationToken));
    }

    private static async Task<IResult> GetHistoryAsync(Guid id, ClaimsPrincipal principal, ExpenseService service, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.GetHistoryAsync(principal.ToUserContext(), id, cancellationToken));
    }
}
