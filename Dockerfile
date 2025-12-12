FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/TransformationEngine.Service/TransformationEngine.Service.csproj", "TransformationEngine.Service/"]
COPY ["src/TransformationEngine.Core/TransformationEngine.Core.csproj", "TransformationEngine.Core/"]
COPY ["src/TransformationEngine.Interfaces/TransformationEngine.Interfaces.csproj", "TransformationEngine.Interfaces/"]
COPY ["src/TransformationEngine.Integration/TransformationEngine.Integration.csproj", "TransformationEngine.Integration/"]
COPY ["src/EntityContracts/EntityContracts.csproj", "EntityContracts/"]
RUN dotnet restore "TransformationEngine.Service/TransformationEngine.Service.csproj"
COPY src/ .
WORKDIR "/src/TransformationEngine.Service"
RUN dotnet build "TransformationEngine.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TransformationEngine.Service.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TransformationEngine.Service.dll"]

