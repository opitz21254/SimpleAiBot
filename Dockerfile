FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /App

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN cd SimpleBlazor
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
EXPOSE 8080 
WORKDIR /App/SimpleBlazor
COPY --from=build-env /App/out .
ENTRYPOINT ["dotnet", "SimpleBlazor.dll"]