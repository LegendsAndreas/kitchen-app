FROM mcr.microsoft.com/dotnet/sdk:9.0 AS builder

WORKDIR /app

COPY WebKitchen.csproj ./
RUN dotnet restore

COPY . ./

RUN dotnet publish -c Release -o /app/publish
RUN cd /app/publish && chmod +x WebKitchen

CMD ["./publish/WebKitchen"]