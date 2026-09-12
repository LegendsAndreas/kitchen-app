# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY WebKitchen.csproj ./
RUN dotnet restore

COPY . ./

RUN dotnet publish -c Release -o /app/publish --os linux

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8081

CMD ["./WebKitchen"]