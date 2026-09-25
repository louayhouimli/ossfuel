FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["OSSFuel.Api.csproj", "."]
RUN dotnet restore "OSSFuel.Api.csproj"

COPY . .

RUN dotnet publish "OSSFuel.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "OSSFuel.Api.dll"]