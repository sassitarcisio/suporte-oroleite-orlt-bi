FROM mcr.microsoft.com/dotnet/sdk:10.0.201 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json OroBI.slnx ./
COPY src/OroBI.Domain/OroBI.Domain.csproj src/OroBI.Domain/
COPY src/OroBI.Application/OroBI.Application.csproj src/OroBI.Application/
COPY src/OroBI.Infrastructure/OroBI.Infrastructure.csproj src/OroBI.Infrastructure/
COPY src/OroBI.Api/OroBI.Api.csproj src/OroBI.Api/
RUN dotnet restore src/OroBI.Api/OroBI.Api.csproj --disable-build-servers

COPY src/ src/
RUN dotnet publish src/OroBI.Api/OroBI.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "OroBI.Api.dll"]
