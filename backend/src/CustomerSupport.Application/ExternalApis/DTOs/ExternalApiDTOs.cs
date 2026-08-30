namespace CustomerSupport.Application.ExternalApis.DTOs;

public class PostDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class CommentDto
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class WeatherDto
{
    public string Name { get; set; } = string.Empty;
    public WeatherMainDto Main { get; set; } = new();
    public WeatherWindDto Wind { get; set; } = new();
    public List<WeatherDescriptionDto> Weather { get; set; } = new();
}

public class WeatherMainDto
{
    public double Temp { get; set; }
    public double FeelsLike { get; set; }
    public int Humidity { get; set; }
    public double TempMin { get; set; }
    public double TempMax { get; set; }
}

public class WeatherWindDto
{
    public double Speed { get; set; }
}

public class WeatherDescriptionDto
{
    public string Main { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}
