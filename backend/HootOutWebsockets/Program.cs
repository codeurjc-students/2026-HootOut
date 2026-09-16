var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

var app = builder.Build();

app.UseWebSockets();

var websocketsOptions = new WebSocketOptions
{
    KeepAliveTimeout = TimeSpan.FromMinutes(2)
};

//websocketsOptions.AllowedOrigins.Add("https://client.com");
//websocketsOptions.AllowedOrigins.Add("https://www.client.com");

app.Run(async (context) =>
{
    //using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    //var socketFinishedTcs = new TaskCompletionSource<object>();

    //BackgroundSocketProcessor.AddSocket(webSocket, socketFinishedTcs);

    //await socketFinishedTcs.Task;
});

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
