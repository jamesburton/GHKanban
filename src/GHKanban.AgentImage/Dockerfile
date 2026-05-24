# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
# Copy build props from the repo root (build context = repo root for this image build)
COPY Directory.Build.props Directory.Packages.props NuGet.config global.json ./
COPY src/GHKanban.AgentImage/ src/GHKanban.AgentImage/
WORKDIR /src/src/GHKanban.AgentImage
RUN dotnet restore
RUN dotnet publish -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine
WORKDIR /app
COPY --from=build /app .

# The image expects /skill, /event.json, /secrets/* to be mounted at runtime by the control plane.
ENTRYPOINT ["dotnet", "GHKanban.AgentImage.dll"]
