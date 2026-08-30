using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.ExternalApis.Clients;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Application.ExternalApis.Queries.GetWeather;

public record GetWeatherQuery(string City = "London") : IQuery<Response<WeatherDto>>;

public class GetWeatherQueryHandler(
    IWeatherClient? weatherClient,
    IMessageFactory messages)
    : IQueryHandler<GetWeatherQuery, Response<WeatherDto>>
{
    public async Task<Response<WeatherDto>> Handle(GetWeatherQuery request, CancellationToken ct)
    {
        if (weatherClient is null)
        {
            return messages.Fail<WeatherDto>(ApplicationErrors.ExternalApi.NOT_CONFIGURED, MessageType.Internal);
        }

        try
        {
            var weather = await weatherClient.GetCurrentWeatherAsync(request.City, "metric", ct);
            var mapped = new WeatherDto
            {
                Name = weather.Name,
                Main = new WeatherMainDto
                {
                    Temp = weather.Main.Temp,
                    FeelsLike = weather.Main.FeelsLike,
                    Humidity = weather.Main.Humidity,
                    TempMin = weather.Main.TempMin,
                    TempMax = weather.Main.TempMax
                },
                Wind = new WeatherWindDto { Speed = weather.Wind.Speed },
                Weather = weather.Weather.Select(w => new WeatherDescriptionDto
                {
                    Main = w.Main,
                    Description = w.Description,
                    Icon = w.Icon
                }).ToList()
            };
            return messages.Success(mapped, ApplicationErrors.General.SUCCESS_OPERATION);
        }
        catch
        {
            return messages.Fail<WeatherDto>(ApplicationErrors.General.INTERNAL_ERROR, MessageType.Internal);
        }
    }
}
