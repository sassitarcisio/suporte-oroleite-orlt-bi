using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OroBI.Application.Analytics;
using OroBI.Domain.Sellers;
using OroBI.Infrastructure.Identity;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Api.Auth;

public static partial class SellerPortalAccountEndpoints
{
    private static readonly string[] Roles = ["Administrador", "Diretoria", "Gestor", "Gerente", "Vendedor"];

    public static IEndpointRouteBuilder MapSellerPortalAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapSelfRegistrationEndpoints(endpoints);
        foreach (var prefix in new[] { "/api", "/api/v1" })
        {
            endpoints.MapPost($"{prefix}/auth/logout", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users, OroBiDbContext db, CancellationToken ct) =>
            {
                var user = await users.GetUserAsync(principal);
                if (user is null) return Results.Unauthorized();
                var result = await users.UpdateSecurityStampAsync(user);
                if (!result.Succeeded) return Errors(result);
                await AuditAsync(db, principal, "Logout", user.Id, ct);
                return Results.NoContent();
            }).RequireAuthorization();
            endpoints.MapPost($"{prefix}/me/change-password", async (ChangePasswordRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users, OroBiDbContext db, CancellationToken ct) =>
            {
                if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword)) return Results.BadRequest(new { error = "Informe a senha atual e a nova senha." });
                var user = await users.GetUserAsync(principal);
                if (user is null) return Results.Unauthorized();
                var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
                if (!result.Succeeded) return Errors(result);
                await AuditAsync(db, principal, "PasswordChanged", user.Id, ct);
                return Results.NoContent();
            }).RequireAuthorization();
        }

        var admin = endpoints.MapGroup("/api/v1/admin").RequireAuthorization(AuthorizationPolicies.AdministratorOnly);
        admin.MapGet("/sellers", async (OroBiDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Sellers.AsNoTracking().OrderBy(s => s.Name).ToArrayAsync(ct)));
        admin.MapPost("/sellers", async (CreateSellerRequest request, ClaimsPrincipal principal, OroBiDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ImportedName) || request.Name.Length > 120 || request.ImportedName.Length > 120)
                return Results.BadRequest(new { error = "Nome e nome importado são obrigatórios e devem ter até 120 caracteres." });
            var canonical = SellerAliasCatalog.ResolveImportedName(request.ImportedName);
            var canonicalName = AliasKey(request.Name);
            var sellers = await db.Sellers.AsNoTracking().ToArrayAsync(ct);
            if (sellers.Any(s => AliasKey(s.ImportedName) == AliasKey(canonical) || AliasKey(s.Name) == canonicalName || AliasKey(s.Name) == AliasKey(canonical) || AliasKey(s.ImportedName) == canonicalName))
                return Results.Conflict(new { error = "Nome ou alias já vinculado a outro vendedor." });
            var seller = new Seller { Name = request.Name.Trim(), ImportedName = canonical };
            db.Sellers.Add(seller);
            AddAudit(db, principal, "SellerCreated", seller.Id.ToString(), new { seller.Name, seller.ImportedName });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return Results.Conflict(new { error = "Vendedor já cadastrado." }); }
            return Results.Created($"/api/v1/admin/sellers/{seller.Id}", seller);
        });
        admin.MapPut("/sellers/{sellerId:guid}/active", async (Guid sellerId, ActiveRequest request, ClaimsPrincipal principal, OroBiDbContext db, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            var seller = await db.Sellers.FindAsync([sellerId], ct);
            if (seller is null) return Results.NotFound();
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            seller.IsActive = request.IsActive;
            var ids = await db.UserSellerAccesses.Where(a => a.SellerId == sellerId).Select(a => a.UserId).ToArrayAsync(ct);
            foreach (var id in ids)
            {
                var user = await users.FindByIdAsync(id);
                if (user is not null)
                {
                    var result = await users.UpdateSecurityStampAsync(user);
                    if (!result.Succeeded) return Errors(result);
                }
            }
            await AuditAsync(db, principal, "SellerActivationChanged", sellerId.ToString(), ct, request);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Results.NoContent();
        });
        admin.MapGet("/users", async (OroBiDbContext db, CancellationToken ct) =>
        {
            var users = await db.Users.AsNoTracking().OrderBy(u => u.Email).ToArrayAsync(ct);
            var roles = await (from link in db.UserRoles join role in db.Roles on link.RoleId equals role.Id select new { link.UserId, role.Name }).ToArrayAsync(ct);
            var links = await db.UserSellerAccesses.AsNoTracking().Include(a => a.Seller).ToArrayAsync(ct);
            return Results.Ok(users.Select(u => new
            {
                u.Id, u.Email, u.IsActive, u.RegistrationName, u.IsRegistrationPending,
                Roles = roles.Where(r => r.UserId == u.Id).Select(r => r.Name).ToArray(),
                SellerAccesses = links.Where(a => a.UserId == u.Id).Select(a => new { a.SellerId, a.Seller.Name, a.IsActive, a.Permissions }).ToArray()
            }));
        });
        admin.MapPost("/users", async (CreateUserRequest request, ClaimsPrincipal principal, OroBiDbContext db, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password)) return Results.BadRequest(new { error = "Email e senha são obrigatórios." });
            var invalid = await ValidateAccessAsync(request.Role, request.SellerAccesses, db, ct);
            if (invalid is not null) return Results.BadRequest(new { error = invalid });
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            var user = new ApplicationUser { Email = request.Email.Trim(), UserName = request.Email.Trim(), LockoutEnabled = true };
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded) return Errors(result);
            result = await EnsureRoleAsync(request.Role, roles);
            if (!result.Succeeded) return Errors(result);
            result = await users.AddToRoleAsync(user, request.Role);
            if (!result.Succeeded) return Errors(result);
            ReplaceLinks(db, user.Id, [], request.SellerAccesses ?? []);
            await AuditAsync(db, principal, "UserCreated", user.Id, ct, new { request.Role, request.SellerAccesses });
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Results.Created($"/api/v1/admin/users/{user.Id}", new { user.Id, user.Email, user.IsActive });
        });
        admin.MapPut("/users/{userId}/access", async (string userId, UpdateAccessRequest request, ClaimsPrincipal principal, OroBiDbContext db, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, CancellationToken ct) =>
        {
            var invalid = await ValidateAccessAsync(request.Role, request.SellerAccesses, db, ct);
            if (invalid is not null) return Results.BadRequest(new { error = invalid });
            var user = await users.FindByIdAsync(userId);
            if (user is null) return Results.NotFound();
            if (user.IsRegistrationPending) return PendingApprovalRequired();
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            var result = await EnsureRoleAsync(request.Role, roles);
            if (!result.Succeeded) return Errors(result);
            var existingRoles = await users.GetRolesAsync(user);
            result = await users.RemoveFromRolesAsync(user, existingRoles.Where(r => r != request.Role));
            if (!result.Succeeded) return Errors(result);
            if (!existingRoles.Contains(request.Role))
            {
                result = await users.AddToRoleAsync(user, request.Role);
                if (!result.Succeeded) return Errors(result);
            }
            var links = await db.UserSellerAccesses.Where(a => a.UserId == userId).ToArrayAsync(ct);
            ReplaceLinks(db, userId, links, request.SellerAccesses ?? []);
            result = await users.UpdateSecurityStampAsync(user);
            if (!result.Succeeded) return Errors(result);
            await AuditAsync(db, principal, "UserAccessChanged", userId, ct, request);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Results.NoContent();
        });
        admin.MapPut("/users/{userId}/active", async (string userId, ActiveRequest request, ClaimsPrincipal principal, OroBiDbContext db, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            var user = await users.FindByIdAsync(userId);
            if (user is null) return Results.NotFound();
            if (user.IsRegistrationPending) return PendingApprovalRequired();
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            user.IsActive = request.IsActive;
            var result = await users.UpdateSecurityStampAsync(user);
            if (!result.Succeeded) return Errors(result);
            await AuditAsync(db, principal, "UserActivationChanged", userId, ct, request);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Results.NoContent();
        });
        admin.MapPost("/users/{userId}/reset-password", async (string userId, ResetPasswordRequest request, ClaimsPrincipal principal, OroBiDbContext db, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(request.NewPassword)) return Results.BadRequest(new { error = "Informe a nova senha." });
            var user = await users.FindByIdAsync(userId);
            if (user is null) return Results.NotFound();
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
            if (!result.Succeeded) return Errors(result);
            await AuditAsync(db, principal, "PasswordReset", userId, ct);
            return Results.NoContent();
        });
        return endpoints;
    }

    private static async Task<string?> ValidateAccessAsync(string role, SellerAccessRequest[]? accesses, OroBiDbContext db, CancellationToken ct)
    {
        if (!Roles.Contains(role)) return "Perfil inválido.";
        accesses ??= [];
        if (accesses.Length > 500 || accesses.Select(a => a.SellerId).Distinct().Count() != accesses.Length) return "Vínculos duplicados ou limite excedido.";
        var ids = accesses.Select(a => a.SellerId).ToArray();
        var sellers = await db.Sellers.Where(s => ids.Contains(s.Id)).ToArrayAsync(ct);
        if (sellers.Length != accesses.Length) return "Vendedor inexistente.";
        if (accesses.Any(a => a.IsActive && !sellers.Single(s => s.Id == a.SellerId).IsActive)) return "Vendedor desativado não pode receber vínculo ativo.";
        if (role == "Vendedor" && accesses.Count(a => a.IsActive) != 1) return "Vendedor deve ter exatamente um vínculo ativo.";
        return null;
    }

    private static void ReplaceLinks(OroBiDbContext db, string userId, UserSellerAccess[] existing, SellerAccessRequest[] requested)
    {
        foreach (var omitted in existing.Where(a => !requested.Any(r => r.SellerId == a.SellerId)))
            omitted.IsActive = false;
        foreach (var request in requested)
        {
            var link = existing.SingleOrDefault(a => a.SellerId == request.SellerId);
            if (link is null)
            {
                link = new UserSellerAccess { UserId = userId, SellerId = request.SellerId };
                db.UserSellerAccesses.Add(link);
            }
            link.IsActive = request.IsActive;
            link.Permissions = request.Permissions ?? new();
        }
    }

    private static async Task<IdentityResult> EnsureRoleAsync(string role, RoleManager<IdentityRole> roles) =>
        await roles.RoleExistsAsync(role) ? IdentityResult.Success : await roles.CreateAsync(new IdentityRole(role));
    private static string AliasKey(string name) => SellerAliasCatalog.ResolveImportedName(name);
    private static IResult Errors(IdentityResult result) => Results.BadRequest(new { errors = result.Errors.Select(e => e.Description).ToArray() });
    private static void AddAudit(OroBiDbContext db, ClaimsPrincipal actor, string action, string target, object? details = null) =>
        db.AccountAuditEvents.Add(new AccountAuditEvent { ActorUserId = actor.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, Action = action, TargetId = target, DetailsJson = details is null ? null : JsonSerializer.Serialize(details) });
    private static async Task AuditAsync(OroBiDbContext db, ClaimsPrincipal actor, string action, string target, CancellationToken ct, object? details = null)
    {
        AddAudit(db, actor, action, target, details);
        await db.SaveChangesAsync(ct);
    }

    public sealed record CreateSellerRequest(string Name, string ImportedName);
    public sealed record SellerAccessRequest(Guid SellerId, bool IsActive = true, SellerPortalPermissions? Permissions = null);
    public sealed record CreateUserRequest(string Email, string Password, string Role, SellerAccessRequest[]? SellerAccesses);
    public sealed record UpdateAccessRequest(string Role, SellerAccessRequest[]? SellerAccesses);
    public sealed record ActiveRequest(bool IsActive);
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    public sealed record ResetPasswordRequest(string NewPassword);
}
