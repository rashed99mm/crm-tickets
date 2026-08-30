@echo off
setlocal

rem Start both APIs and both Angular apps with the local proxy topology.
set "ASPNETCORE_ENVIRONMENT=Development"
set "SeedData=true"
set "Messaging__Required=false"
set "Jwt__Issuer=CustomerSupport"
set "Jwt__Audience=CustomerSupport"
set "Jwt__Key=ThisIsAVeryLongSecretKeyForJwtTokenGeneration2024!"

if "%ConnectionStrings__DefaultConnection%"=="" set "ConnectionStrings__DefaultConnection=Server=(localdb)\MSSQLLocalDB;Database=CustomerSupportCrmTest;Trusted_Connection=True;TrustServerCertificate=True"

set "ROOT=%~dp0"
start "CustomerSupport Internal API :5074" /D "%ROOT%" cmd /k "dotnet run --project backend\src\CustomerSupport.InternalApi --no-launch-profile --urls http://localhost:5074"
start "CustomerSupport External API :5095" /D "%ROOT%" cmd /k "dotnet run --project backend\src\CustomerSupport.ExternalApi --no-launch-profile --urls http://localhost:5095"
start "CustomerSupport Admin App :4200" /D "%ROOT%frontend" cmd /k "npm start -- --project admin-app --port 4200 --proxy-config proxy.conf.json"
start "CustomerSupport Portal App :4201" /D "%ROOT%frontend" cmd /k "npm start -- --project portal-app --port 4201 --proxy-config proxy.portal.conf.json"

echo Started Internal API  http://localhost:5074
echo Started External API http://localhost:5095
echo Started Admin App     http://localhost:4200
echo Started Portal App    http://localhost:4201
echo Close the four opened command windows to stop development services.
endlocal
