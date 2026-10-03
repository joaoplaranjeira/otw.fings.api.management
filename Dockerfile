FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["otw.fings.api.management.csproj", "."]
RUN dotnet restore "otw.fings.api.management.csproj"
COPY . .
RUN dotnet publish "otw.fings.api.management.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
CMD ["sh", "-c", "dotnet otw.fings.api.management.dll --urls http://0.0.0.0:${PORT:-8080}"]
