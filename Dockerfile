FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY SportsCenterAPI.csproj ./
RUN dotnet restore SportsCenterAPI.csproj
COPY . .
RUN dotnet publish SportsCenterAPI.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
CMD ["sh", "-c", "exec dotnet SportsCenterAPI.dll --urls http://0.0.0.0:${PORT:-8080}"]
