# Se construye desde la raíz del repositorio: docker compose up --build
# Compilación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/src/Mentorias.Api/Mentorias.Api.csproj backend/src/Mentorias.Api/
RUN dotnet restore backend/src/Mentorias.Api/Mentorias.Api.csproj
COPY backend/src/Mentorias.Api/ backend/src/Mentorias.Api/
RUN dotnet publish backend/src/Mentorias.Api/Mentorias.Api.csproj -c Release -o /app --no-restore

# Ejecución: imagen ligera, usuario sin privilegios y el sitio de frontend/ como archivos estáticos
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
COPY frontend/index.html frontend/panel.html /app/wwwroot/
COPY frontend/css /app/wwwroot/css
COPY frontend/js /app/wwwroot/js
COPY frontend/img /app/wwwroot/img
RUN mkdir -p /app/App_Data/adjuntos /app/App_Data/claves && chown -R $APP_UID /app/App_Data
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Mentorias.Api.dll"]
