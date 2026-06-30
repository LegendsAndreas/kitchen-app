FROM mcr.microsoft.com/dotnet/runtime-deps:10.0

WORKDIR /app

COPY bin/Debug/net10.0/linux-x64/publish ./

EXPOSE 5001

CMD ["./WebKitchen"]

#FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

#WORKDIR /app

#COPY WebKitchen.csproj ./
#RUN dotnet restore

#COPY . ./

#RUN dotnet publish -c Release -o /app/publish
#RUN cd /app/publish && chmod +x WebKitchen

#CMD ["./publish/WebKitchen"]