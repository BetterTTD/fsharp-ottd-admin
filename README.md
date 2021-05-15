<p align="center" style="width: 50%; margin:0 left;text-align: left;">
    <img width="100px" src="https://github.com/TG-OpenTTD/fsharp-ottd-admin/blob/main/media/icon_400x400.png" align="center" />
    <h1 align="center">FSharp OpenTTD Admin</h1>
    <p align="center">NuGet package of OpenTTD admin implementation via FSharp</p>
</p>

</br>
</br>
</br>

## Installation
First of all u need connect to NuGet feed. Easies way to do this is to add next configuration into your ```nuget.config``` file:
```
<configuration>
    <packageSources>
        <add key="github" value="https://nuget.pkg.github.com/TG-OpenTTD/index.json" />
    </packageSources>
    <packageSourceCredentials>
        <github>
            <add key="Username" value="PublicToken" />
            <add key="ClearTextPassword" value="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" />
        </github>
    </packageSourceCredentials>
</configuration>
```
Then you will be able to install package with next command:
```
dotnet add [<PROJECT>] package FSharp.OpenTTD.Admin
```
