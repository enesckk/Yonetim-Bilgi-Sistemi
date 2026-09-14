FROM node:22-bookworm-slim AS frontend-build
WORKDIR /frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY backend/ backend/
COPY frontend/public/geo/ frontend/public/geo/
RUN dotnet publish backend/src/PersonelYonetim.Api/PersonelYonetim.Api.csproj \
    -c Release -o /publish --nologo

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /publish/ ./
COPY --from=frontend-build /frontend/dist/ ./wwwroot/
COPY frontend/public/geo/ ./wwwroot/geo/
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "PersonelYonetim.Api.dll"]
