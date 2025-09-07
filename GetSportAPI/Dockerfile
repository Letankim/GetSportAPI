# =======================
# Stage 1: Build
# =======================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution và project file trước (để tận dụng cache restore)
COPY *.sln .
COPY GetSportAPI/*.csproj GetSportAPI/

RUN dotnet restore

# Copy toàn bộ code vào container
COPY . .

# Build và publish ra thư mục /app
WORKDIR /src/GetSportAPI
RUN dotnet publish -c Release -o /app

# =======================
# Stage 2: Runtime
# =======================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Expose port (Render mặc định sẽ map PORT env)
EXPOSE 8080

# Gán ASP.NET lắng nghe đúng PORT
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "GetSportAPI.dll"]
