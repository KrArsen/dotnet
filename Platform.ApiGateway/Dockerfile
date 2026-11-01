FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Platform.ApiGateway/Platform.ApiGateway.csproj", "Platform.ApiGateway/"]
COPY ["Platform.ServiceDefaults/Platform.ServiceDefaults.csproj", "Platform.ServiceDefaults/"]
RUN dotnet restore "Platform.ApiGateway/Platform.ApiGateway.csproj"
COPY . .
WORKDIR "/src/Platform.ApiGateway"
RUN dotnet build "Platform.ApiGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Platform.ApiGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Platform.ApiGateway.dll"]
