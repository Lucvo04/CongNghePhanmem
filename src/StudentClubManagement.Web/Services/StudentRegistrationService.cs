using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.ViewModels.Account;

namespace StudentClubManagement.Web.Services;

public class StudentRegistrationService(ApplicationDbContext db, UserManager<ApplicationUser> users,
    ClubAccessService access, ClubOperationLocks locks)
{
    public Task<WorkflowResult> RegisterAsync(StudentRegistrationForm form) => WorkflowTransaction.RunAsync(db, async () =>
    {
        form.Normalize();
        if (!Validator.TryValidateObject(form, new ValidationContext(form), new List<ValidationResult>(), true))
            return new("Validation", "Vui lòng kiểm tra đầy đủ thông tin và mật khẩu xác nhận.");
        if (await db.Users.AnyAsync(u => u.StudentCode == form.StudentCode)) return new("Validation", "Email hoặc mã sinh viên đã được sử dụng.");
        var user = new ApplicationUser
        {
            Email = form.Email, FullName = form.FullName, StudentCode = form.StudentCode,
            ClassName = form.ClassName?.Trim(), Faculty = form.Faculty?.Trim(),
            Status = UserStatus.Pending, RoleId = "system-student"
        };
        var result = await users.CreateAsync(user, form.Password);
        if (!result.Succeeded) return new("Validation", string.Join(" ", result.Errors.Select(e => e.Description)));
        db.AuditLogs.Add(new AuditLog { ActorId = user.Id, Action = "StudentRegistered", EntityType = nameof(ApplicationUser),
            EntityId = user.Id, AfterData = "{\"Status\":\"Pending\"}" });
        await db.SaveChangesAsync(); return new("Success");
    });

    public Task<WorkflowResult> ReviewAsync(string id, string actor, string stamp, bool approve, string? reason) =>
        WorkflowTransaction.RunAsync(db, async () =>
        {
            if (!await access.IsAdminAsync(actor)) return new("NotFound");
            if ((!approve && string.IsNullOrWhiteSpace(reason)) || reason?.Length > 500)
                return new("Validation", "Từ chối cần lý do; lý do tối đa 500 ký tự.");
            foreach (var account in new[] { actor, id }.Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                if (!await locks.AccountAsync(account)) return WorkflowTransaction.Conflict();
            if (!await access.IsAdminAsync(actor)) return new("NotFound");
            var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.RoleId == "system-student");
            if (user is null) return new("NotFound");
            await db.Entry(user).ReloadAsync();
            if (user.Status != UserStatus.Pending || user.ConcurrencyStamp != stamp) return WorkflowTransaction.Conflict();
            user.Status = approve ? UserStatus.Active : UserStatus.Rejected;
            var result = await users.UpdateSecurityStampAsync(user);
            if (!result.Succeeded) return WorkflowTransaction.Conflict();
            db.AuditLogs.Add(new AuditLog { ActorId = actor, Action = approve ? "StudentRegistrationApproved" : "StudentRegistrationRejected",
                EntityType = nameof(ApplicationUser), EntityId = id, BeforeData = "{\"Status\":\"Pending\"}",
                AfterData = JsonSerializer.Serialize(new { Status = user.Status.ToString() }), Reason = reason?.Trim() });
            await db.SaveChangesAsync(); return new("Success");
        });
}

