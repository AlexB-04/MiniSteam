FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["MiniSteam/MiniSteam.csproj", "MiniSteam/"]
RUN dotnet restore "MiniSteam/MiniSteam.csproj"

COPY . .
WORKDIR "/src/MiniSteam"
RUN dotnet publish "MiniSteam.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MiniSteam.dll"]
