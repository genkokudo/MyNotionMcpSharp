using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace MyNotionMcpSharp;

public class Function1(ILogger<Function1> logger)
{
    [Function(nameof(GetWeatherForecast))]
    public WeatherForecastResult GetWeatherForecast(
    [McpToolTrigger(nameof(GetWeatherForecast), "指定された場所の天気予報を取得します。")]
        ToolInvocationContext context,
    [McpToolProperty(nameof(location), "天気を知りたい場所の名前")]
        string location)
    {
        logger.LogInformation("Location: {location}", location);
        return location switch
        {
            "東京" => new WeatherForecastResult("東京", "晴れ"),
            "大阪" => new WeatherForecastResult("大阪", "曇り"),
            "福岡" => new WeatherForecastResult("福岡", "雨"),
            _ => new WeatherForecastResult(location, "不明"),
        };
    }

}
public record WeatherForecastResult(
    string Location,
    string Weather);