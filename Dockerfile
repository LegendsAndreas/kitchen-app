# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY WebKitchen.csproj ./
RUN dotnet restore

COPY . ./

RUN dotnet publish -c Release -o /app/publish --os linux --self-contained

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 5001

CMD ["./WebKitchen"]