using RuoliAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using RuoliAPI.Models;
using RuoliAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<CalligraphyContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("CalligraphyDB"));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://45.130.167.27:8080", "https://ruoliwrite.com", "https://www.ruoliwrite.com").AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

builder.Services.AddControllers().AddNewtonsoftJson();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//IHttpFactory 註入
builder.Services.AddHttpClient();
//GetClientIPService 註入
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IClientIpService, GetClientIPService>();
//LogService 註入
builder.Services.AddScoped<ILogService, LogService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();
