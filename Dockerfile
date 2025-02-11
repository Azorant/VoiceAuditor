FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["VoiceAuditor.Bot/VoiceAuditor.Bot.csproj", "VoiceAuditor.Bot/"]
RUN dotnet restore "VoiceAuditor.Bot/VoiceAuditor.Bot.csproj"
COPY . .
WORKDIR "/src/VoiceAuditor.Bot"
RUN dotnet build "VoiceAuditor.Bot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "VoiceAuditor.Bot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VoiceAuditor.Bot.dll"]
