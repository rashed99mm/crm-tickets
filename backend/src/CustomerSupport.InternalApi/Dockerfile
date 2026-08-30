FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy props files FIRST (before project files) for central package management
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["NuGet.Config", "./"]

# Copy project files
COPY ["CCE.Platform/CCE.Platform.API/CCE.Platform.API.csproj", "CCE.Platform/CCE.Platform.API/"]
COPY ["CCE.Platform/CCE.Platform.Application/CCE.Platform.Application.csproj", "CCE.Platform/CCE.Platform.Application/"]
COPY ["CCE.Platform/CCE.Platform.Domain/CCE.Platform.Domain.csproj", "CCE.Platform/CCE.Platform.Domain/"]
COPY ["CCE.Platform/CCE.Platform.Infrastructure/CCE.Platform.Infrastructure.csproj", "CCE.Platform/CCE.Platform.Infrastructure/"]
COPY ["Shared/CCE.Shared.Constants/CCE.Shared.Constants.csproj", "Shared/CCE.Shared.Constants/"]
COPY ["Shared/CCE.Shared.Contracts/CCE.Shared.Contracts.csproj", "Shared/CCE.Shared.Contracts/"]

# Now restore with props available
RUN dotnet restore "CCE.Platform/CCE.Platform.API/CCE.Platform.API.csproj"

COPY . .
WORKDIR "/src/CCE.Platform/CCE.Platform.API"
RUN dotnet build "CCE.Platform.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CCE.Platform.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CCE.Platform.API.dll"]
