using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OroBI.Domain.Sellers;
using OroBI.Infrastructure.Identity;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Api.Auth;

public static partial class SellerPortalAccountEndpoints
{
    private const string RegistrationMessage = "Solicitação recebida. Se o cadastro puder prosseguir, aguarde a aprovação do administrador.";

    private static void MapSelfRegistrationEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/auth/register", RegisterAsync).AllowAnonymous();
        endpoints.MapPost("/api/v1/admin/users/{userId}/approve-registration", ApproveRegistrationAsync)
            .RequireAuthorization(AuthorizationPolicies.AdministratorOnly);
    }

    private static async Task<IResult> RegisterAsync(RegistrationRequest request, HttpContext context,
        RegistrationRateLimiter limiter, UserManager<ApplicationUser> users, OroBiDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120 || request.Name.Any(char.IsControl))
            return Results.BadRequest(new { error = "Informe seu nome com até 120 caracteres." });
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Trim().Length > 254 || !new EmailAddressAttribute().IsValid(request.Email.Trim()))
            return Results.BadRequest(new { error = "Informe um e-mail válido com até 254 caracteres." });
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 128)
            return Results.BadRequest(new { error = "Informe uma senha com até 128 caracteres." });

        using var lease = limiter.TryAcquire(request.Email);
        if (!lease.IsAcquired)
        {
            var retry = lease.TryGetMetadata(MetadataName.RetryAfter, out var duration) ? duration : TimeSpan.FromMinutes(1);
            context.Response.Headers.RetryAfter = Math.Max(1, Math.Ceiling(retry.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }
        var email = request.Email.Trim();
        var user = new ApplicationUser { Email = email, UserName = email, RegistrationName = request.Name.Trim(), IsRegistrationPending = true, IsActive = false, LockoutEnabled = true };
        // Validate the public password policy before checking duplicates, keeping validation responses consistent.
        var passwordErrors = new List<IdentityError>();
        foreach (var validator in users.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(users, user, request.Password);
            if (!validation.Succeeded) passwordErrors.AddRange(validation.Errors);
        }
        if (passwordErrors.Count > 0) return Errors(IdentityResult.Failed(passwordErrors.ToArray()));
        if (await users.FindByEmailAsync(email) is not null || await users.FindByNameAsync(email) is not null) return RegistrationAccepted();

        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var created = await users.CreateAsync(user, request.Password);
            if (!created.Succeeded)
            {
                if (created.Errors.All(error => error.Code is "DuplicateEmail" or "DuplicateUserName")) return RegistrationAccepted();
                return Errors(created);
            }
            db.AccountAuditEvents.Add(new AccountAuditEvent { ActorUserId = "anonymous", Action = "RegistrationRequested", TargetId = user.Id });
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return RegistrationAccepted();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UserNameIndex" })
        {
            // Concurrent same-email requests also converge on the unique normalized username, without mutating the winner.
            return RegistrationAccepted();
        }
    }

    private static async Task<IResult> ApproveRegistrationAsync(string userId, ApproveRegistrationRequest request,
        ClaimsPrincipal principal, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, OroBiDbContext db, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        // A row lock serializes concurrent approvals in PostgreSQL before testing the pending state.
        var user = db.Database.IsRelational()
            ? await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct)
            : await users.FindByIdAsync(userId);
        if (user is null) return Results.NotFound();
        if (!user.IsRegistrationPending) return Results.Conflict(new { error = "Este cadastro não está pendente de aprovação." });
        // Keep seller eligibility stable through the approval commit as well as serializing the account.
        var seller = db.Database.IsRelational()
            ? await db.Sellers.FromSqlInterpolated($"SELECT * FROM \"Sellers\" WHERE \"Id\" = {request.SellerId} FOR SHARE").SingleOrDefaultAsync(ct)
            : await db.Sellers.SingleOrDefaultAsync(s => s.Id == request.SellerId, ct);
        if (seller is null || !seller.IsActive)
            return Results.BadRequest(new { error = "Selecione um vendedor ativo para aprovar o cadastro." });
        var existingRoles = await users.GetRolesAsync(user);
        if (existingRoles.Count > 0 || await db.UserSellerAccesses.AnyAsync(a => a.UserId == userId, ct))
            return Results.Conflict(new { error = "O cadastro pendente possui vínculos inesperados e precisa de revisão." });
        var result = await EnsureRoleAsync("Vendedor", roles);
        if (!result.Succeeded) return Errors(result);
        result = await users.AddToRoleAsync(user, "Vendedor");
        if (!result.Succeeded) return Errors(result);
        db.UserSellerAccesses.Add(new UserSellerAccess { UserId = userId, SellerId = request.SellerId, IsActive = true, Permissions = request.Permissions ?? new() });
        user.IsActive = true;
        user.IsRegistrationPending = false;
        result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) return Errors(result);
        await AuditAsync(db, principal, "RegistrationApproved", userId, ct, new { request.SellerId, request.Permissions });
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.NoContent();
    }

    private static IResult RegistrationAccepted() => Results.Accepted(value: new { message = RegistrationMessage });
    private static IResult PendingApprovalRequired() => Results.BadRequest(new { error = "Aprove o cadastro pendente vinculando um vendedor antes de alterar o acesso." });
    public sealed record RegistrationRequest(string Name, string Email, string Password);
    public sealed record ApproveRegistrationRequest(Guid SellerId, SellerPortalPermissions? Permissions = null);
}
