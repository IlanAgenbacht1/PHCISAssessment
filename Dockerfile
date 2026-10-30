FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, on its own layer, so code changes don't re-download packages.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Phcis.Booking.Api/Phcis.Booking.Api.csproj src/Phcis.Booking.Api/
COPY src/Phcis.Booking.Client/Phcis.Booking.Client.csproj src/Phcis.Booking.Client/
COPY src/Phcis.Booking.Domain/Phcis.Booking.Domain.csproj src/Phcis.Booking.Domain/
COPY src/Phcis.Booking.Shared/Phcis.Booking.Shared.csproj src/Phcis.Booking.Shared/
RUN dotnet restore src/Phcis.Booking.Api/Phcis.Booking.Api.csproj

COPY src/ src/
RUN dotnet publish src/Phcis.Booking.Api/Phcis.Booking.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# Clinic time zones are looked up by IANA id, which needs the tz database in the image.
RUN apt-get update && apt-get install -y --no-install-recommends tzdata && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV TZ=Africa/Johannesburg \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Phcis.Booking.Api.dll"]
