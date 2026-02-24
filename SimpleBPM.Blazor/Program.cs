using Autofac;
using Autofac.Extensions.DependencyInjection;
using SimpleBPM.Blazor.Components;
using SimpleBPM.Localisation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Use Autofac as the DI container.
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    // Monitoring-only registration: no execution engine or IBpmMediator needed.
    // Register process definitions before this call if you want them visible in the dashboard.
    containerBuilder.RegisterModule(new ProcessMonitoringAutofacModule());
});

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
