using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<LinklyDb>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<LinklyDb>().Database.EnsureCreated();

// API
app.MapPost("/api/links", async (LinklyDb db, CreateLink req) =>
{
    if (string.IsNullOrWhiteSpace(req.Url)) return Results.BadRequest(new { error = "url is required" });
    if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var uri) ||
        (uri.Scheme != "http" && uri.Scheme != "https"))
        return Results.BadRequest(new { error = "url must be a valid http(s) URL" });
    string code;
    do { code = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant(); }
    while (await db.Links.AnyAsync(l => l.Code == code));
    var link = new Link { Code = code, Url = req.Url, CreatedAt = DateTime.UtcNow };
    db.Links.Add(link);
    await db.SaveChangesAsync();
    return Results.Ok(ToDto(link, 0));
});

app.MapGet("/api/links", async (LinklyDb db) =>
    await db.Links.OrderByDescending(l => l.CreatedAt)
        .Select(l => new { l.Code, l.Url, l.CreatedAt, clicks = l.Clicks.Count }).ToListAsync());

app.MapGet("/api/links/{code}/stats", async (LinklyDb db, string code) =>
{
    var link = await db.Links.Include(l => l.Clicks).FirstOrDefaultAsync(l => l.Code == code);
    if (link is null) return Results.NotFound();
    var byDay = link.Clicks.GroupBy(c => c.ClickedAt.Date)
        .Select(g => new { date = g.Key.ToString("yyyy-MM-dd"), count = g.Count() })
        .OrderBy(x => x.date).ToList();
    var recent = link.Clicks.OrderByDescending(c => c.ClickedAt).Take(20)
        .Select(c => new { at = c.ClickedAt, ua = c.UserAgent, referer = c.Referer }).ToList();
    return Results.Ok(new { link = ToDto(link, link.Clicks.Count), byDay, recent });
});

// Redirect
app.MapGet("/r/{code}", async (LinklyDb db, string code, HttpContext ctx) =>
{
    var link = await db.Links.FirstOrDefaultAsync(l => l.Code == code);
    if (link is null) return Results.NotFound(new { error = "unknown short code" });
    db.Clicks.Add(new Click
    {
        LinkId = link.Id,
        ClickedAt = DateTime.UtcNow,
        UserAgent = ctx.Request.Headers.UserAgent.ToString(),
        Referer = ctx.Request.Headers.Referer.ToString()
    });
    await db.SaveChangesAsync();
    return Results.Redirect(link.Url);
});

app.Run();

static object ToDto(Link l, int clicks) => new { l.Code, l.Url, l.CreatedAt, clicks };

public record CreateLink(string Url);

public class Link
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<Click> Clicks { get; set; } = new();
}

public class Click
{
    public long Id { get; set; }
    public int LinkId { get; set; }
    public DateTime ClickedAt { get; set; }
    public string UserAgent { get; set; } = "";
    public string Referer { get; set; } = "";
}

public class LinklyDb : DbContext
{
    public LinklyDb(DbContextOptions<LinklyDb> o) : base(o) { }
    public DbSet<Link> Links => Set<Link>();
    public DbSet<Click> Clicks => Set<Click>();
    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("linkly");
        mb.Entity<Link>().HasIndex(l => l.Code).IsUnique();
        mb.Entity<Link>().Property(l => l.Url).HasMaxLength(2048);
        mb.Entity<Click>().Property(c => c.UserAgent).HasMaxLength(512);
        mb.Entity<Click>().Property(c => c.Referer).HasMaxLength(1024);
    }
}

