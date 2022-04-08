@echo off
dotnet new bepinex5plugin -n %1 -T net46 -U 2020.3.26
dotnet restore %1