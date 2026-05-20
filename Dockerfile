FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/BolaoCopa.Domain/BolaoCopa.Domain.csproj             src/BolaoCopa.Domain/
COPY src/BolaoCopa.Application/BolaoCopa.Application.csproj   src/BolaoCopa.Application/
COPY src/BolaoCopa.Infrastructure/BolaoCopa.Infrastructure.csproj src/BolaoCopa.Infrastructure/
COPY src/BolaoCopa.Api/BolaoCopa.Api.csproj                   src/BolaoCopa.Api/
RUN dotnet restore src/BolaoCopa.Api/BolaoCopa.Api.csproj

COPY . .
RUN dotnet publish src/BolaoCopa.Api/BolaoCopa.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "BolaoCopa.Api.dll"]
