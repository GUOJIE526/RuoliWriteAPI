using Microsoft.EntityFrameworkCore;
using RuoliAPI.Models;

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
        // 第三個網址是使用 ngrok 生成的外部可訪問網址(前端部分)/ by.shan shan
        policy.WithOrigins("http://localhost:3000").AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//IHttpFactory 註入
builder.Services.AddHttpClient();

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
