FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY RentalAI.sln ./
COPY src/RentalAI.Api/RentalAI.Api.csproj src/RentalAI.Api/
COPY src/shared/RentalAI.Common/RentalAI.Common.csproj src/shared/RentalAI.Common/
COPY src/shared/RentalAI.Contracts/RentalAI.Contracts.csproj src/shared/RentalAI.Contracts/
RUN dotnet restore src/RentalAI.Api/RentalAI.Api.csproj

COPY . .
RUN dotnet publish src/RentalAI.Api/RentalAI.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "RentalAI.Api.dll"]
