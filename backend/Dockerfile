# syntax=docker/dockerfile:1

# ---- build: restore, compile, publish -------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SchoolAdmission.slnx Directory.Build.props ./
COPY src/SchoolAdmission.Api/SchoolAdmission.Api.csproj src/SchoolAdmission.Api/
COPY tests/SchoolAdmission.Api.Tests/SchoolAdmission.Api.Tests.csproj tests/SchoolAdmission.Api.Tests/
RUN dotnet restore SchoolAdmission.slnx
COPY . .
RUN dotnet build SchoolAdmission.slnx -c Release --no-restore \
 && dotnet publish src/SchoolAdmission.Api -c Release -o /app/publish --no-build

# ---- test: `docker compose -f docker-compose.test.yml run backend-tests` ----------------
FROM build AS test
ENTRYPOINT ["dotnet", "test", "SchoolAdmission.slnx", "-c", "Release", "--no-build", "--logger", "console;verbosity=normal"]

# ---- runtime (default, final stage) ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    HOLD_DURATION_SECONDS=600
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "SchoolAdmission.Api.dll"]
