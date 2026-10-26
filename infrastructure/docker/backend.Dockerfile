FROM mcr.microsoft.com/dotnet/sdk:10.0.203 AS build
WORKDIR /source
COPY global.json ./
COPY apps/backend/ apps/backend/
ARG PROJECT=TicketFlow.Api
RUN dotnet restore apps/backend/src/${PROJECT}/${PROJECT}.csproj --locked-mode
RUN dotnet publish apps/backend/src/${PROJECT}/${PROJECT}.csproj --no-restore -c Release -o /out /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet"]
CMD ["TicketFlow.Api.dll"]
