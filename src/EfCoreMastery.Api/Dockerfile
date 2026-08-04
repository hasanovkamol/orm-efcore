# Build stage (.NET 10 Preview / SDK)
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files
COPY ["src/EfCoreMastery.Domain/EfCoreMastery.Domain.csproj", "EfCoreMastery.Domain/"]
COPY ["src/EfCoreMastery.Application/EfCoreMastery.Application.csproj", "EfCoreMastery.Application/"]
COPY ["src/EfCoreMastery.Infrastructure/EfCoreMastery.Infrastructure.csproj", "EfCoreMastery.Infrastructure/"]
COPY ["src/EfCoreMastery.Api/EfCoreMastery.Api.csproj", "EfCoreMastery.Api/"]

# Restore dependencies
RUN dotnet restore "EfCoreMastery.Api/EfCoreMastery.Api.csproj"

# Copy remaining source code
COPY src/ .

# Publish app
WORKDIR "/src/EfCoreMastery.Api"
RUN dotnet publish "EfCoreMastery.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage (.NET 10 ASP.NET)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "EfCoreMastery.Api.dll"]
