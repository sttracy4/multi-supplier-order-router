FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/OrderRouter.Api/OrderRouter.Api.csproj OrderRouter.Api/
RUN dotnet restore OrderRouter.Api/OrderRouter.Api.csproj
COPY src/OrderRouter.Api/ OrderRouter.Api/
RUN dotnet publish OrderRouter.Api/OrderRouter.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderRouter.Api.dll"]
