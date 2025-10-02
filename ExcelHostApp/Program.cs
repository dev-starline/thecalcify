using CommonDatabase;
using CommonDatabase.Interfaces;
using CommonDatabase.Services;
using DashboardExcelApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Reuters.Repositories;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var allowedOrigins = builder.Configuration.GetSection("allowedOrigins").Get<string[]>();
builder.Services.AddSingleton(allowedOrigins);

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "AllowOrigin",
        policy =>
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        });
});
builder.Services.AddHttpClient();
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(365); 
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.MigrationsAssembly("CommonDatabase") 
    )
);


var firebaseServerKey = builder.Configuration["Firebase:ServerKey"];

builder.Services.AddControllers()
    .AddApplicationPart(typeof(ClientExcelApi.Controllers.ClientAuthController).Assembly)
    .AddApplicationPart(typeof(DashboardExcelApi.Controllers.AuthController).Assembly)
    .AddJsonOptions(x =>
    {
        x.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });


builder.Services.AddSingleton<IUserIdProvider, QueryStringUserIdProvider>();
// Register a singleton Redis connection
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("127.0.0.1:6379"));

// Use that connection for SignalR Redis backplane
builder.Services.AddSignalR().AddStackExchangeRedis(options =>
{
    var multiplexer = builder.Services.BuildServiceProvider().GetRequiredService<IConnectionMultiplexer>();
    options.ConnectionFactory = async writer =>
    {
        writer.WriteLine("Connecting to Redis...");
        return multiplexer;
    };
});

//builder.Services.AddSignalR().AddStackExchangeRedis("127.0.0.1:6379");
//builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect("127.0.0.1:6379"));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<ReutersService>();
builder.Services.AddScoped<IInstrumentsService, InstrumentsService>();
builder.Services.AddScoped<ISubscribeService, SubscribeService>();
builder.Services.AddScoped<ISelfSubscribeService, SelfSubscribeService>();
builder.Services.AddScoped<ApplicationConstant>();
builder.Services.AddHostedService<SubscribeRate>();
builder.Services.AddSingleton<IJwtBlacklistService, JwtBlacklistService>();
builder.Services.AddScoped<ICommonService, CommonService>();
builder.Services.AddSingleton<ConnectionStore>();

builder.Services.AddOpenApi();
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]);
//builder.Services.AddHttpClient<CommonService>(client =>
//{
//    client.BaseAddress = new Uri(builder.Configuration.GetSection("publishUrl").Value); // or your deployed domain
//});
builder.Services.AddHttpClient("MyApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetSection("publishUrl").Value);
});


builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero, // ⛔ disables grace 
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Excel API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ClientOnly", policy => policy.RequireRole("Client"));
});
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Excel API V1");
    c.RoutePrefix = "swagger"; 
});

app.UseCors("AllowOrigin");
app.UseHttpsRedirection();

app.UseSession();
app.UseDefaultFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ExcelHub>("/excel");
app.Run();