# build with the SDK image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
COPY . .
WORKDIR /
RUN dotnet restore -a $TARGETARCH
RUN dotnet publish -a $TARGETARCH --no-restore -o /app/publish

# runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 
WORKDIR /app 
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["./DockerRadar"]