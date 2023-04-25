#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["RemindMeBot/RemindMeBot.csproj", "RemindMeBot/"]
COPY ["RemindMeBot.Tests.Unit/RemindMeBot.Tests.Unit.csproj", "RemindMeBot.Tests.Unit/"]

# Restore projects
RUN dotnet restore "RemindMeBot/RemindMeBot.csproj"
RUN dotnet restore "RemindMeBot.Tests.Unit/RemindMeBot.Tests.Unit.csproj"
COPY . .

# Build
WORKDIR "/src/RemindMeBot"
RUN dotnet build "RemindMeBot.csproj" -c Release -o /app/build

# Run tests
WORKDIR "/src/RemindMeBot.Tests.Unit"
RUN dotnet test "RemindMeBot.Tests.Unit.csproj"

FROM build AS publish
WORKDIR "/src/RemindMeBot"
RUN dotnet publish "RemindMeBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RemindMeBot.dll"]
