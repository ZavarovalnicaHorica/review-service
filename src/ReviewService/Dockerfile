# Build context: this folder (src/ReviewService)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached.
COPY ReviewService.csproj ./
RUN dotnet restore ReviewService.csproj

COPY . .
RUN dotnet publish ReviewService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER app
ENTRYPOINT ["dotnet", "Horica.ReviewService.dll"]
