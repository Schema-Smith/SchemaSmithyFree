#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["SchemaQuench/SchemaQuench.csproj", "SchemaQuench/"]
RUN dotnet restore "SchemaQuench/SchemaQuench.csproj"
COPY . .
WORKDIR "/src/SchemaQuench"
RUN dotnet build "SchemaQuench.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SchemaQuench.csproj"  --framework net9.0 -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SchemaQuench.dll"]