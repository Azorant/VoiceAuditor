FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS base
WORKDIR /app
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["VoiceAuditor.sln", "."]
COPY ["VoiceAuditor.Bot/VoiceAuditor.Bot.csproj", "VoiceAuditor.Bot/"]
COPY ["VoiceAuditor.Database/VoiceAuditor.Database.csproj", "VoiceAuditor.Database/"]
RUN dotnet restore
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
