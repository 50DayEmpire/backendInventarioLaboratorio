# ETAPA 1: Compilación (SDK)
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Usamos caché para las capas de restauración
COPY ["BackendInventario.csproj", "./"]
RUN dotnet restore "BackendInventario.csproj"

COPY . .
# Build directo a la carpeta de salida
RUN dotnet build "BackendInventario.csproj" -c Release -o /app/build

# ETAPA 2: Publicación
FROM build AS publish
RUN dotnet publish "BackendInventario.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ETAPA 3: Imagen Final
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

RUN apk add --no-cache icu-libs

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=publish /app/publish .

RUN mkdir -p /app/wwwroot/imagenes

#Exponer el puerto que usará la aplicación
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080


ENTRYPOINT ["dotnet", "BackendInventario.dll"]