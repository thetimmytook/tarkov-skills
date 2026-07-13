# TOTEST — проверка на Windows

## 1. Ретро-анонимизация benchmark.json

- [ ] Взять существующий `benchmark.json`, у старых runs вручную добавить в `settings` поле `settings_dir` и в `fps` поле `path`.
- [ ] Добавить новый прогон через приложение (или `scripts/add-benchmark-run.ps1`).
- [ ] Проверить, что у СТАРЫХ записей эти поля удалены (фикс порядка загрузки в `add-benchmark-run.ps1`: очистка старых runs теперь выполняется после загрузки `$benchmark`, а не до неё).

## 2. Обычный прогон приложения end-to-end

- [ ] Пройти полный цикл сбора в `TarkovBenchmarkWizard.ps1` (capture → context → save).
- [ ] После сохранения строка результата (`$lblResult`) показывает актуальные Avg FPS / 1% low.
- [ ] Кнопки Open folder и Upload становятся активны сразу после сохранения (регрессия после правки `Complete-CaptureCollection`, которая теперь делегирует обновление UI в `Update-BenchmarkDataAvailability`).

## 3. UAC-ретрай захвата (фикс ExitCode = $null)

- [ ] На машине, где непривилегированный запуск PresentMon падает, запустить `scripts/capture-presentmon.ps1 -DurationSec 120 -RequestElevation` и принять UAC-запрос.
- [ ] Захват завершается успешно, БЕЗ ложной ошибки вида «PresentMon exited with code .» (после элевированного ретрая код выхода может быть нечитаем — теперь гарантия успеха это наличие CSV).

## 4. Пустой контекст логов → `unknown` (фикс нормализации)

- [ ] Вызвать `scripts/add-benchmark-run.ps1` напрямую с `-Map ""` (остальные параметры валидные).
- [ ] В сохранённом прогоне `map` = `unknown` (не пустая строка), `confidence` НЕ `high`.

## 5. Дедуп загрузки (Upload)

- [ ] После первого сохранённого прогона нажать Upload: в буфере payload со всеми прогонами, открылась форма, в `benchmark.json` появились `uploaded_run_count` и `last_uploaded_at`.
- [ ] Нажать Upload повторно без новых прогонов: приложение сообщает «уже отправлено» и предлагает скопировать всё заново (Yes/No).
- [ ] Сделать ещё один прогон и нажать Upload: в буфере payload только с новым прогоном.

## 6. Кодекс-архив без scripts/references/app

- [ ] Запустить релиз (GitHub Action **Release**) и распаковать `tarkov-skills-codex.zip`.
- [ ] Внутри архива только `skills/` + `AGENTS.md`, `CLAUDE.md`, `README.md`, `LICENSE`.
- [ ] Папок `scripts/`, `references/`, `app/` в архиве нет.
