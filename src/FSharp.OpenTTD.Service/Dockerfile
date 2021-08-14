ARG DotnetVersion=5.0
FROM mcr.microsoft.com/dotnet/sdk:$DotnetVersion-focal AS build
WORKDIR /app

COPY src/*/*.fsproj ./
RUN for file in $(ls *.fsproj); do \
        echo $file \
        && mkdir -p src/${file%.*}/ \
        && mv $file src/${file%.*}/; \
    done

RUN dotnet restore ./src/FSharp.OpenTTD.Service/FSharp.OpenTTD.Service.fsproj
RUN dotnet restore ./src/FSharp.OpenTTD.Admin/FSharp.OpenTTD.Admin.fsproj

COPY . ./

RUN dotnet publish ./src/FSharp.OpenTTD.Service/FSharp.OpenTTD.Service.fsproj -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/sdk:$DotnetVersion-focal AS base

WORKDIR /app
COPY --from=build /app/out .

HEALTHCHECK --interval=30s --timeout=30s --retries=3 \
   CMD curl --silent --fail http://localhost:80/_health || exit 1
   
EXPOSE 80
EXPOSE 443
ENTRYPOINT ["dotnet", "FSharp.OpenTTD.Service.dll"]
