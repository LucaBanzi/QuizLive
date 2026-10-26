FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/QuizLive.Api/QuizLive.Api.csproj src/QuizLive.Api/
RUN dotnet restore src/QuizLive.Api/QuizLive.Api.csproj
COPY src/QuizLive.Api/ src/QuizLive.Api/
RUN dotnet publish src/QuizLive.Api/QuizLive.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
RUN mkdir -p /app/media
ENTRYPOINT ["dotnet", "QuizLive.Api.dll"]
