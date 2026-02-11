using SimpleBPM.Blazor.Components;
using SimpleBPM.Localisation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Monitoring-only registration: no execution engine or ICommandExecutor needed.
// Register process definitions before this call if you want them visible in the dashboard.
builder.Services.AddProcessMonitoring();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
