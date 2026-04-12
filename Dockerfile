FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SunroomCrm.sln ./
COPY SunroomCrm.Core/*.csproj SunroomCrm.Core/
COPY SunroomCrm.Infrastructure/*.csproj SunroomCrm.Infrastructure/
COPY SunroomCrm.Api/*.csproj SunroomCrm.Api/
COPY SunroomCrm.Tests/*.csproj SunroomCrm.Tests/
RUN dotnet restore SunroomCrm.sln

COPY . .
RUN dotnet publish SunroomCrm.Api/SunroomCrm.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5236
EXPOSE 5236

ENTRYPOINT ["dotnet", "SunroomCrm.Api.dll"]
