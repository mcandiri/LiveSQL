using LiveSQL.Core.Extensions;
using LiveSQL.Web.Components;
using LiveSQL.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register LiveSQL Core services (parsers, analyzers, visualization, demo)
builder.Services.AddLiveSqlCore();

// Register LiveSQL Web services
builder.Services.AddSingleton<DemoDataService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
