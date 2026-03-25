FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/AgenticRAG.Api/AgenticRAG.Api.csproj", "src/AgenticRAG.Api/"]
COPY ["src/AgenticRAG.Core/AgenticRAG.Core.csproj", "src/AgenticRAG.Core/"]
RUN dotnet restore "src/AgenticRAG.Api/AgenticRAG.Api.csproj"
COPY . .
WORKDIR "/src/src/AgenticRAG.Api"
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AgenticRAG.Api.dll"]
