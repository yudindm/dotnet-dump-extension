# Локальная сборка пакетов diagnostics

Пакеты diagnostics пока не публикуются в nuget, так что для разработки расширений
к утилите dotnet-dump нужно собрать эти пакеты локльно.

1. Склонировать репозиторий diagnostics из GitHub

   ```
   git clone https://github.com/dotnet/diagnostics.git
   ```

2. Добавить идентификатор официальной сборки в файл _...\diagnostics\eng\Versions.props_  

   рассчитывается от номера патча из версии сборки утилиты dotnet-dump  
   например, если версия утилиты `6.0.351802` (номер патча `351802`), то
   OfficialBuildId должент быть равен `20221018.2`  

   Описание расчета взятое из файла _microsoft.dotnet.arcade.sdk\8.0.0-beta.22480.2\tools\Version.BeforeCommonTargets.targets_
   - OfficialBuildId is assumed to have format "20yymmdd.r" (the assumption is checked later in a target).  
   - SHORT_DATE := yy * 1000 + mm * 50 + dd  
   - REVISION := r  
   - PATCH_NUMBER := (SHORT_DATE - VersionBaseShortDate) * 100 + r  
     по умолчанию VersionBaseShortDate=19000

   ```
     <PropertyGroup>
       <OfficialBuildId>20221018.2</OfficialBuildId>
     </PropertyGroup>
   ```

3. Собрать пакеты для проектов

   - dotnet pack Microsoft.Diagnostics.DebugServices
   - dotnet pack Microsoft.Diagnostics.ExtensionCommands

   Пакеты сформируются в каталоге _diagnostics\artifacts\packages\Debug\NonShipping_

4. Настроить локальный репозиторий nuget на каталог с собранными в п.3 пакетами

   ```
   <configuration>
     <packageSources>
       <add key="local_diag" value="...diagnostics\artifacts\packages\Debug\NonShipping" />
     <packageSources>
   </configuration>
   ```

# Разработка расширений

1. Создать проект Class Library
2. Подключить пакет Microsoft.Diagnostics.ExtensionCommands
3. Разработать расширение ))

# Подключение расширений

Перед запуском утилиты _dotnet-dump_ определить переменную окружения, с путем к dll-файлу
с командами расширенй

```
export DOTNET_DIAGNOSTIC_EXTENSIONS=/full/path/to/extension/Yudindm.Diagnostics.DumpExt.dll
dotnet-dump analyze some.dump
```
