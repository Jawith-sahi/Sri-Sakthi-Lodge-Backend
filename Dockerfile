# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY ["SriSakthiLodge_API.csproj", "./"]
RUN dotnet restore "SriSakthiLodge_API.csproj"

COPY . .
RUN dotnet publish "SriSakthiLodge_API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/Uploads /app/Uploads

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "SriSakthiLodge_API.dll"]
