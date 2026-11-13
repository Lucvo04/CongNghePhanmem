using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Account;
using StudentClubManagement.Web.ViewModels.Clubs;

namespace StudentClubManagement.IntegrationTests;

public class ApprovalWorkflowTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    private Person admin = null!, student = null!, outsider = null!;
    public async Task InitializeAsync()
    {
        app = new(database.ConnectionString);
        using var scope = app.Services.CreateScope();
        database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        admin = await Seed("system-admin"); student = await Seed(); outsider = await Seed();
    }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RegistrationMustBeReviewedBeforeLoginAndCannotBeApprovedThroughUnlock(bool approve)
    {
        var form = Registration();
        await using (var s = app.Services.CreateAsyncScope())
            Assert.True((await s.ServiceProvider.GetRequiredService<StudentRegistrationService>().RegisterAsync(form)).Succeeded);
        await using var db = database.CreateContext();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == form.Email);
        Assert.Equal(UserStatus.Pending, user.Status); Assert.Equal("system-student", user.RoleId);
        using var client = app.BrowserClient();
        var person = new Person(user.Id, user.Email!, form.Password);
        Assert.Equal(HttpStatusCode.OK, (await LoginResponse(client, person)).StatusCode);
        await using (var s = app.Services.CreateAsyncScope())
        {
            var result = await s.ServiceProvider.GetRequiredService<StudentAccountService>().ChangeStatusAsync(user.Id, user.ConcurrencyStamp!, UserStatus.Active, "Bypass review", admin.Id);
            Assert.False(result.Succeeded);
        }
        using var reviewer = await Login(admin);
        var path = $"/Admin/Students/Review/{user.Id}";
        var html = await reviewer.GetStringAsync(path);
        var fields = new Dictionary<string,string> { ["__RequestVerificationToken"] = Hidden(html,"__RequestVerificationToken"),
            ["ConcurrencyStamp"] = Hidden(html,"ConcurrencyStamp"), ["Approve"] = approve.ToString(), ["Reason"] = "Đã đối chiếu hồ sơ" };
        Assert.Equal(HttpStatusCode.Redirect, (await reviewer.PostAsync(path, new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await reviewer.PostAsync(path, new FormUrlEncodedContent(fields))).StatusCode);
        var after = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(approve ? UserStatus.Active : UserStatus.Rejected, after.Status);
        Assert.Equal(user.PasswordHash, after.PasswordHash); Assert.NotEqual(user.SecurityStamp, after.SecurityStamp);
        Assert.Equal(approve ? HttpStatusCode.Redirect : HttpStatusCode.OK, (await LoginResponse(client, person)).StatusCode);
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.EntityId == user.Id && a.EntityType == nameof(ApplicationUser)));
    }

    [Fact]
    public async Task RegistrationAndReviewRequireAntiforgeryAndAdminCannotCreateAccounts()
    {
        using var anonymous = app.BrowserClient(); using var reviewer = await Login(admin); using var ordinary = await Login(student);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsync("/Account/Register", new FormUrlEncodedContent([]))).StatusCode);
        var form = Registration();
        await using (var s = app.Services.CreateAsyncScope()) await s.ServiceProvider.GetRequiredService<StudentRegistrationService>().RegisterAsync(form);
        await using var db = database.CreateContext(); var id = await db.Users.Where(u => u.Email == form.Email).Select(u => u.Id).SingleAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsync($"/Admin/Students/Review/{id}", new FormUrlEncodedContent([]))).StatusCode);
        Assert.Contains("AccessDenied", (await ordinary.GetAsync($"/Admin/Students/Review/{id}")).Headers.Location!.ToString());
        var token = Hidden(await reviewer.GetStringAsync("/Account/Overview"),"__RequestVerificationToken");
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.PostAsync("/Admin/Students/Create", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"]=token }))).StatusCode);
    }

    [Fact]
    public async Task ReviewRejectsMissingReasonNonAdminAndStaleStampAndSerializesCompetingDecisions()
    {
        var form = Registration();
        await using (var s = app.Services.CreateAsyncScope()) await s.ServiceProvider.GetRequiredService<StudentRegistrationService>().RegisterAsync(form);
        await using var db = database.CreateContext(); var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == form.Email);
        async Task<WorkflowResult> Review(string actor, string stamp, bool approve, string? reason)
        { await using var s = app.Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<StudentRegistrationService>().ReviewAsync(user.Id, actor, stamp, approve, reason); }
        Assert.Equal("NotFound", (await Review(student.Id, user.ConcurrencyStamp!, true, null)).Code);
        Assert.Equal("Validation", (await Review(admin.Id, user.ConcurrencyStamp!, false, " ")).Code);
        Assert.Equal("Conflict", (await Review(admin.Id, "stale", true, null)).Code);
        var results = await Task.WhenAll(Review(admin.Id, user.ConcurrencyStamp!, true, null), Review(admin.Id, user.ConcurrencyStamp!, false, "Không hợp lệ"));
        Assert.Single(results, r => r.Succeeded); Assert.Single(results, r => r.Code == "Conflict");
    }

    [Fact]
    public async Task ProposalApprovalCreatesExactlyOneClubAndManagerAndEnablesManagerMenu()
    {
        using var applicant = await Login(student); using var reviewer = await Login(admin);
        Assert.DoesNotContain("CLB tôi quản lý", WebUtility.HtmlDecode(await applicant.GetStringAsync("/Account/Overview")));
        var page = await applicant.GetStringAsync("/ClubProposals/Create");
        var response = await applicant.PostAsync("/ClubProposals/Create", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["Name"]="CLB <script>mới</script>", ["Purpose"]="Sinh hoạt và chia sẻ kiến thức", ["Category"]="Học thuật",
            ["ApplicantId"]=outsider.Id, ["Status"]="2", ["ClubId"]="1", ["__RequestVerificationToken"]=Hidden(page,"__RequestVerificationToken") }));
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        var path = response.Headers.Location!.ToString();
        await using var db = database.CreateContext(); var proposal = await db.ClubProposals.AsNoTracking().SingleAsync(p=>p.ApplicantId==student.Id);
        Assert.Equal(ProposalStatus.Pending,proposal.Status); Assert.Null(proposal.ClubId);
        var details = await reviewer.GetStringAsync(path); Assert.DoesNotContain("<script>mới</script>",details);
        var form = new Dictionary<string,string> { ["RowVersion"]=Hidden(details,"RowVersion"), ["Operation"]="Approve", ["Code"]="NEW-"+proposal.Id,
            ["__RequestVerificationToken"]=Hidden(details,"__RequestVerificationToken") };
        var results = await Task.WhenAll(reviewer.PostAsync($"/ClubProposals/Decide/{proposal.Id}",new FormUrlEncodedContent(form)),reviewer.PostAsync($"/ClubProposals/Decide/{proposal.Id}",new FormUrlEncodedContent(form)));
        Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Redirect); Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Conflict);
        var approved = await db.ClubProposals.AsNoTracking().SingleAsync(p=>p.Id==proposal.Id);
        Assert.Equal(ProposalStatus.Approved,approved.Status);
        var club = await db.Clubs.FindAsync(approved.ClubId); Assert.NotNull(club); Assert.Equal(ClubStatus.Active,club.Status);
        var manager = await db.Memberships.SingleAsync(m=>m.ClubId==club.Id);
        Assert.Equal(student.Id,manager.UserId); Assert.Equal(ClubRole.Manager,manager.Role); Assert.Equal(MembershipStatus.Active,manager.Status);
        Assert.Contains("CLB tôi quản lý",WebUtility.HtmlDecode(await applicant.GetStringAsync("/Account/Overview")));
        Assert.Equal(HttpStatusCode.OK,(await applicant.GetAsync("/ClubManagement")).StatusCode);
        Assert.Equal(1,await db.AuditLogs.CountAsync(a=>a.EntityType==nameof(ClubProposal)&&a.EntityId==proposal.Id.ToString()&&a.Action=="ClubProposalApproved"));
    }

    [Theory]
    [InlineData("Reject")]
    [InlineData("Withdraw")]
    public async Task ClosingProposalDoesNotCreateClubAndAllowsNewApplication(string operation)
    {
        var id=await Submit(); var before=await Read(id);
        if(operation=="Reject") Assert.Equal("Validation",(await Decide(id,admin.Id,operation,reason:" ")).Code);
        Assert.True((await Decide(id,operation=="Reject"?admin.Id:student.Id,operation,reason:"Cần bổ sung kế hoạch")).Succeeded);
        var after=await Read(id); Assert.Null(after.ClubId); Assert.Equal(operation=="Reject"?ProposalStatus.Rejected:ProposalStatus.Withdrawn,after.Status);
        Assert.Equal("Conflict",(await Decide(id,admin.Id,"Approve",stamp:Convert.ToBase64String(before.RowVersion))).Code);
        Assert.NotEqual(id,await Submit());
        using var client=await Login(student); Assert.Contains("Cần bổ sung kế hoạch",WebUtility.HtmlDecode(await client.GetStringAsync($"/ClubProposals/Details/{id}")));
    }

    [Fact]
    public async Task PrivateProposalsAndForgedDecisionsAreProtected()
    {
        var id=await Submit(); using var other=await Login(outsider); using var owner=await Login(student); using var reviewer=await Login(admin);
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/ClubProposals/Details/{id}")).StatusCode);
        Assert.DoesNotContain($"/ClubProposals/Details/{id}\"",await other.GetStringAsync("/ClubProposals"));
        Assert.Equal("NotFound",(await Decide(id,outsider.Id,"Withdraw")).Code);
        Assert.Equal("NotFound",(await Decide(id,student.Id,"Approve")).Code);
        Assert.Equal("NotFound",(await Decide(id,admin.Id,"Withdraw")).Code);
        Assert.Equal(HttpStatusCode.BadRequest,(await reviewer.PostAsync($"/ClubProposals/Decide/{id}",new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsync("/ClubProposals/Create",new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.GetAsync("/ClubProposals?status=999")).StatusCode);
        var page=await owner.GetAsync($"/ClubProposals/Details/{id}"); Assert.True(page.Headers.CacheControl!.NoStore);
    }

    [Theory]
    [InlineData("Disabled")]
    [InlineData("Admin")]
    [InlineData("NoRole")]
    [InlineData("Stale")]
    [InlineData("CodeCollision")]
    public async Task InvalidApprovalLeavesNoClubOrManager(string condition)
    {
        var id=await Submit(); var old=await Read(id);
        await using(var db=database.CreateContext())
        {
            var user=await db.Users.FindAsync(student.Id);
            if(condition=="Disabled") user!.Status=UserStatus.Disabled;
            if(condition=="Admin") user!.RoleId="system-admin";
            if(condition=="NoRole") user!.RoleId=null;
            if(condition=="Stale") (await db.ClubProposals.FindAsync(id))!.Purpose="Changed";
            if(condition=="CodeCollision") db.Clubs.Add(new Club{Code="PROP-"+id,Name="Existing"});
            await db.SaveChangesAsync();
        }
        Assert.False((await Decide(id,admin.Id,"Approve",stamp:Convert.ToBase64String(old.RowVersion))).Succeeded);
        var after=await Read(id); Assert.Equal(ProposalStatus.Pending,after.Status); Assert.Null(after.ClubId);
        await using var verify=database.CreateContext(); Assert.False(await verify.Memberships.AnyAsync(m=>m.UserId==student.Id));
    }

    [Fact]
    public async Task DuplicatePendingRequestsAndApprovalAuditFailuresAreAtomic()
    {
        async Task<WorkflowResult> Send() { await using var s=app.Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<ClubProposalService>().SubmitAsync(student.Id,new(){Name="New club",Purpose="A valid plan"}); }
        var results=await Task.WhenAll(Send(),Send()); Assert.Single(results,r=>r.Succeeded); Assert.Single(results,r=>r.Code=="Conflict");
        var id=results.Single(r=>r.Succeeded).Id!.Value;
        await using var db=database.CreateContext();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectProposalAudit ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Action='ClubProposalApproved') THROW 51001,'Test audit failure',1; END");
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(()=>Decide(id,admin.Id,"Approve"));
            Assert.Equal(ProposalStatus.Pending,(await Read(id)).Status);
            Assert.False(await db.Clubs.AnyAsync(c=>c.Code=="PROP-"+id));
            Assert.False(await db.Memberships.AnyAsync(m=>m.UserId==student.Id));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectProposalAudit"); }
    }

    [Fact]
    public async Task RegistrationAuditFailureRollsBackAccount()
    {
        var form=Registration(); await using var db=database.CreateContext();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectRegistrationAudit ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Action='StudentRegistered') THROW 51001,'Test audit failure',1; END");
        try
        {
            await using var s=app.Services.CreateAsyncScope();
            await Assert.ThrowsAsync<DbUpdateException>(()=>s.ServiceProvider.GetRequiredService<StudentRegistrationService>().RegisterAsync(form));
            Assert.False(await db.Users.AnyAsync(u=>u.Email==form.Email));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectRegistrationAudit"); }
    }

    private static StudentRegistrationForm Registration() { var id=Guid.NewGuid().ToString("N"); return new(){FullName="Sinh viên mới",Email=$"registration-{id}@example.test",StudentCode=id[..24],Password="Test-Only!9-password",ConfirmPassword="Test-Only!9-password"}; }
    private async Task<Person> Seed(string role="system-student")
    { await using var s=app.Services.CreateAsyncScope(); var users=s.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var f=Registration(); var u=new ApplicationUser{Email=f.Email,FullName=f.FullName,StudentCode=f.StudentCode,RoleId=role}; Assert.True((await users.CreateAsync(u,f.Password)).Succeeded); return new(u.Id,f.Email,f.Password); }
    private async Task<int> Submit() { await using var s=app.Services.CreateAsyncScope(); var r=await s.ServiceProvider.GetRequiredService<ClubProposalService>().SubmitAsync(student.Id,new(){Name="CLB mới",Purpose="Kế hoạch sinh hoạt và phát triển"}); Assert.True(r.Succeeded,r.Message); return r.Id!.Value; }
    private async Task<ClubProposal> Read(int id) { await using var db=database.CreateContext(); return await db.ClubProposals.AsNoTracking().SingleAsync(p=>p.Id==id); }
    private async Task<WorkflowResult> Decide(int id,string actor,string op,string? reason=null,string? stamp=null)
    { stamp??=Convert.ToBase64String((await Read(id)).RowVersion); await using var s=app.Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<ClubProposalService>().DecideAsync(id,actor,new(){Operation=op,Reason=reason,Code="PROP-"+id,RowVersion=stamp}); }
    private async Task<HttpClient> Login(Person p) { var c=app.BrowserClient(); Assert.Equal(HttpStatusCode.Redirect,(await LoginResponse(c,p)).StatusCode); return c; }
    private static async Task<HttpResponseMessage> LoginResponse(HttpClient c,Person p) => await c.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{["Email"]=p.Email,["Password"]=p.Password,["__RequestVerificationToken"]=Hidden(await c.GetStringAsync("/Account/Login"),"__RequestVerificationToken")}));
    private static string Hidden(string html,string name) { var m=Regex.Match(html,$"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\""); Assert.True(m.Success,"Missing "+name); return WebUtility.HtmlDecode(m.Groups[1].Value); }
    private sealed record Person(string Id,string Email,string Password);
}
