FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/OidcStarter.AspNetCore.Bff/OidcStarter.AspNetCore.Bff.csproj src/OidcStarter.AspNetCore.Bff/
COPY src/backend/Backend.csproj src/backend/
RUN dotnet restore src/backend/Backend.csproj

COPY src/OidcStarter.AspNetCore.Bff/ src/OidcStarter.AspNetCore.Bff/
COPY src/backend/ src/backend/
RUN dotnet publish src/backend/Backend.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish/ .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Backend.dll"]
