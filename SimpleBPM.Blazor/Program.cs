using Autofac;
using Autofac.Extensions.DependencyInjection;
using SimpleBPM.Blazor.Components;
using SimpleBPM.Blazor.Services;
using SimpleBPM.Localisation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Use Autofac as the DI container.
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    // Register Blazor-specific scoped services via Autofac.
    // InstancePerLifetimeScope is equivalent to AddScoped in Microsoft DI.
    containerBuilder.RegisterType<EnvironmentContext>()
        .AsSelf()
        .InstancePerLifetimeScope();

    // Monitoring-only registration: no execution engine or IBpmMediateur needed.
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
