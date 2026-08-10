
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src


COPY MedMateAI/MedMateAI.csproj MedMateAI/
COPY MedMateAI.Application/MedMateAI.Application.csproj MedMateAI.Application/
COPY MedMateAI.Domain/MedMateAI.Domain.csproj MedMateAI.Domain/
COPY MedMateAI.Infrastructure/MedMateAI.Infrastructure.csproj MedMateAI.Infrastructure/

RUN dotnet restore MedMateAI/MedMateAI.csproj

COPY MedMateAI/ MedMateAI/
COPY MedMateAI.Application/ MedMateAI.Application/
COPY MedMateAI.Domain/ MedMateAI.Domain/
COPY MedMateAI.Infrastructure/ MedMateAI.Infrastructure/

RUN dotnet publish MedMateAI/MedMateAI.csproj -c Release -o /app/publish --no-restore

# Run
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render (và nhiều PaaS) inject biến PORT; local có thể để trống → 8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["/bin/sh", "-c", "exec dotnet MedMateAI.dll --urls \"http://0.0.0.0:${PORT:-8080}\""]
