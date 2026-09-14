FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY backend/ backend/
COPY frontend/public/geo/ frontend/public/geo/
RUN dotnet publish backend/src/PersonelYonetim.Api/PersonelYonetim.Api.csproj \
    -c Release -o /publish --nologo

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /publish/ ./
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "PersonelYonetim.Api.dll"]
