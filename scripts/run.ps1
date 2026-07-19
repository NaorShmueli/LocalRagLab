$ErrorActionPreference = "Stop"

Push-Location "$PSScriptRoot\..\src\LocalRagLab.Api"
try {
    dotnet restore
    dotnet run --launch-profile https
}
finally {
    Pop-Location
}
