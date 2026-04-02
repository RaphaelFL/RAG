FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Api/Chatbot.Api.csproj", "src/Api/"]
COPY ["src/Application/Chatbot.Application.csproj", "src/Application/"]
COPY ["src/Domain/Chatbot.Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/Chatbot.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Retrieval/Chatbot.Retrieval.csproj", "src/Retrieval/"]
COPY ["src/Ingestion/Chatbot.Ingestion.csproj", "src/Ingestion/"]
COPY ["src/Mcp/Chatbot.Mcp.csproj", "src/Mcp/"]

RUN dotnet restore "src/Api/Chatbot.Api.csproj"

COPY . .
WORKDIR "/src/src/Api"
RUN dotnet build "Chatbot.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Chatbot.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=publish /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Chatbot.Api.dll"]
