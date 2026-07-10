# TOTEST — проверка на Windows

Изменения написаны на macOS без PowerShell: прогнаны только эвристические проверки синтаксиса и грепы. Перед релизом пройти этот список на игровой машине. Все команды выполняются из корня репозитория.

## 1. PresentMon: флаги и самозавершение

```powershell
PresentMon.exe -process_name EscapeFromTarkov.exe -timed 10 -terminate_after_timed -output_file test.csv
```

- [ ] Процесс PresentMon сам завершается после 10 секунд (не висит).
- [ ] Твоя версия PresentMon принимает эти флаги без ошибки.
- [ ] Из **не**-админской консоли выдаётся понятная ошибка про elevation (скрипт `capture-presentmon.ps1` должен её подсказать).

## 2. Flat map настроек (фикс 2.1)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File skills\tarkov-config\scripts\analyze-tarkov-fps-config.ps1
```

- [ ] Секция **Important Settings** показывает реальные значения из Graphics.ini/PostFx.ini, а не сплошные `unknown`.
- [ ] Секция **Tarkov Readiness**: CPU/GPU/Storage больше не всегда `Unknown` (тиры по названию железа и типу диска).
- [ ] Цель сохраняется и читается из `%LOCALAPPDATA%\TarkovSkills\memory\current-goal.json` (запусти с `-Goal better-graphics -TargetFpsMin 45 -SaveGoal`, потом без параметров).

## 3. VRAM и тип диска (фикс 2.3)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\collect-system-info.ps1 -IncludePagefile
```

- [ ] `vram_gb` больше 4 на современной карте, `vram_source` = `registry`.
- [ ] `drive_media_type` у pagefile = `SSD` или `HDD`, а не `unknown`.

## 4. Версия игры из логов (5.1)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\read-tarkov-raid-context.ps1
```

- [ ] Поле `game_version` заполнено (формат вида `0.16.x.x.xxxxx`).
- [ ] Если версия `unknown` — прислать пример имени лог-папки и первых строк `application.log`, поправим regex.

## 5. Визард end-to-end

```text
app\Start-TarkovBenchmark.cmd
```

- [ ] Кнопка Collect собирает settings.json + system.json.
- [ ] «Read latest logs» подставляет карту/server model/версию, но НЕ выбирает режим PvP/PvE автоматически.
- [ ] CSV парсится (включая CSV с `;`-разделителем, если есть под рукой).
- [ ] `run.json` сохраняется в `%LOCALAPPDATA%\TarkovSkills\runs\<timestamp>\`.
- [ ] В `run.json` нигде нет имени пользователя Windows и имени компьютера (поиск по файлу).
- [ ] `schema` = `tarkov-performance-run/v2`, поле `game_version` присутствует.

## 6. Парсер CSV (3.6)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\parse-fps-csv.ps1 -Path <путь к капче>
```

- [ ] Метрики совпадают по порядку величины с тем, что показывает сам PresentMon/CapFrameX.
- [ ] Парс 2-3-минутной капчи занимает секунды, а не десятки секунд.

## 7. Плагин и релиз

- [ ] `/plugin marketplace add thetimmytook/tarkov-skills` + `/plugin install tarkov-performance@tarkov-skills` — скилы видны в Claude Code.
- [ ] GitHub Action **Release** (ручной запуск) собирает оба архива, бампает версию и публикует Release.
- [ ] Скачанный `TarkovBenchmarkApp.zip` работает на чистой папке без репозитория.
