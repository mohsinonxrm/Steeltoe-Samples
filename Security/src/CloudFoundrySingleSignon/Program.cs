using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Steeltoe.Common.Hosting;
using Steeltoe.Common.Options;
using Steeltoe.Connector.Redis;
using Steeltoe.Extensions.Configuration.CloudFoundry;
using Steeltoe.Management.Endpoint;
using Steeltoe.Security.Authentication.CloudFoundry;
using Steeltoe.Security.DataProtection;
using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);
builder.UseCloudHosting(null, 8081);
builder.Configuration
    .AddCloudFoundry()
    .AddCloudFoundryContainerIdentity("a8fef16f-94c0-49e3-aa0b-ced7c3da6229", "122b942a-d7b9-4839-b26e-836654b9785f");

builder.AddAllActuators();

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddCloudFoundryContainerIdentity();
builder.Services.AddHttpClient("default", (serviceProvider, client) =>
{
    // retrieve a Cloud Foundry style client certificate from the environment, pass it base64-encoded in a request header
    var options = serviceProvider.GetService<IOptions<CertificateOptions>>();
    if (options?.Value.Certificate == null) return;
    var b64 = Convert.ToBase64String(options.Value.Certificate.Export(X509ContentType.Cert));
    client.DefaultRequestHeaders.Add("X-Forwarded-Client-Cert", b64);
}).ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true });


builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CloudFoundryDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.AccessDeniedPath = new PathString("/Home/AccessDenied");
    })
        //    .AddCloudFoundryOAuth(builder.Configuration)
        .AddCloudFoundryOpenIdConnect(builder.Configuration)
    ;

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("testgroup", policy => policy.RequireClaim("scope", "testgroup"));
    options.AddPolicy("testgroup1", policy => policy.RequireClaim("scope", "testgroup1"));
});

// Add Redis to allow scaling beyond a single instance
//builder.Services.AddRedisConnectionMultiplexer(builder.Configuration);
//builder.Services.AddDataProtection()
//    .PersistKeysToRedis()
//    .SetApplicationName("cfSSO");

//builder.Services.AddDistributedRedisCache(builder.Configuration);
//builder.Services.AddSession();

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();