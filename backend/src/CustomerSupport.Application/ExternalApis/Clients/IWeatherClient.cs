using Refit;

namespace CustomerSupport.Application.ExternalApis.Clients;

[ExternalApiClient("WeatherApi")]
public interface IWeatherClient
{
    [Get("/weather")]
    Task<WeatherApiResponse> GetCurrentWeatherAsync(
        [Query] string city,
        [Query] string units = "metric",
        CancellationToken cancellationToken = default);

    [Get("/forecast")]
    Task<WeatherApiForecastResponse> GetForecastAsync(
        [Query] string city,
        [Query] int cnt = 5,
        [Query] string units = "metric",
        CancellationToken cancellationToken = default);
}

public class WeatherApiResponse
{
    public string Name { get; set; } = string.Empty;
    public WeatherApiMain Main { get; set; } = new();
    public WeatherApiWind Wind { get; set; } = new();
    public List<WeatherApiDescription> Weather { get; set; } = new();
}

public class WeatherApiMain
{
    public double Temp { get; set; }
    public double FeelsLike { get; set; }
    public int Humidity { get; set; }
    public double TempMin { get; set; }
    public double TempMax { get; set; }
}

public class WeatherApiWind
{
    public double Speed { get; set; }
}

public class WeatherApiDescription
{
    public string Main { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public class WeatherApiForecastResponse
{
    public List<WeatherApiForecastItem> List { get; set; } = new();
}

public class WeatherApiForecastItem
{
    public DateTime Dt { get; set; }
    public WeatherApiForecastMain Main { get; set; } = new();
    public List<WeatherApiDescription> Weather { get; set; } = new();
}

public class WeatherApiForecastMain
{
    public double Temp { get; set; }
    public double TempMin { get; set; }
    public double TempMax { get; set; }
    public int Humidity { get; set; }
}
