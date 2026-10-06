# 🌊 Ripple

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)
![Whisper](https://img.shields.io/badge/ASR-OpenAI%20Whisper-412991)
![llama.cpp](https://img.shields.io/badge/LLM-llama.cpp%20%2B%20gpt--oss--20b-informational)

**Ripple** - консольное приложение для Windows, которое превращает аудио, видео и записи с экрана компьютера в текстовый конспект лекции. Транскрибация (Whisper) и составление конспекта (gpt-oss-20b через llama.cpp) выполняются локально, без облачных API.

## 🔍 Возможности

- **Импорт** аудио/видеофайла, папки с файлами или ссылки (через yt-dlp) - `ripple import`.
- **Запись системного звука** (WASAPI loopback) с последующей обработкой - `ripple record`.
- **Транскрибация** речи на русском языке с таймкодами (OpenAI Whisper, по умолчанию модель `large-v3`).
- **Автоматический конспект** на русском: краткое содержание, тезисы, ключевые термины, вопросы для повторения. Длинные транскрипты обрабатываются по частям и сводятся в один конспект.
- **Библиотека лекций** (`list`, `show`, `retry`, `delete`) с короткими ID (первые 8 символов) и статусами обработки.
- **Экспорт** в Markdown и DOCX - `ripple export`.
- **Менеджер модулей**: установка и удаление ffmpeg, yt-dlp, openai-whisper, весов Whisper, llama.cpp и GGUF-модели командой `ripple modules`.
- **Диагностика окружения** - `ripple status`.

## 🛠️ Технологии

| Компонент | Назначение |
|---|---|
| .NET 10 (`net10.0-windows10.0.19041.0`), C# | основное приложение (консольное) |
| [NAudio](https://github.com/naudio/NAudio) 2.2.1 | запись системного звука (WASAPI loopback) |
| [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK) 3.3.0 | экспорт в DOCX |
| [openai-whisper](https://github.com/openai/whisper) (Python, pip) | транскрибация |
| [llama.cpp](https://github.com/ggml-org/llama.cpp) (`llama-server`) | локальный сервер LLM |
| gpt-oss-20b, GGUF (MXFP4), [ggml-org/gpt-oss-20b-GGUF](https://huggingface.co/ggml-org/gpt-oss-20b-GGUF) | генерация конспектов |
| FFmpeg | конвертация в WAV (16 кГц, моно) и определение длительности |
| yt-dlp | скачивание аудио по ссылке |

## 🚀 Установка и запуск

### Требования

- Windows 10 (сборка 10.0.17763) или новее.
- [.NET 10 SDK](https://dotnet.microsoft.com/download).
- Python 3 с `pip` в `PATH` (версия: 3.11).
- Свободное место на диске: ~3 ГБ для весов `large-v3`, ~12 ГБ для GGUF-модели gpt-oss-20b, ~130 МБ для FFmpeg (размеры приблизительные, из каталога модулей приложения).

### Шаги

1. Клонируйте репозиторий и перейдите в каталог проекта (где лежит `ripple.csproj`):

```bash
   git clone github.com/cxlijes/Ripple
   cd ripple
```

2. Соберите проект:

```bash
   dotnet build
```

3. Укажите каталог для тяжёлых данных. По умолчанию в конфигурации задан `e:\dev\ripple\storage`, поэтому на другой машине его нужно заменить. Пустое значение означает `%APPDATA%\Ripple`:

```bash
   dotnet run -- config set data-root "D:\ripple-data"
```

4. Установите зависимости через менеджер модулей:

```bash
   dotnet run -- modules install ffmpeg
   dotnet run -- modules install ytdlp
   dotnet run -- modules install whisper
   dotnet run -- modules install whisper-model
   dotnet run -- modules install llamacpp
   dotnet run -- modules install gguf
```

   `whisper` ставится командой `pip install openai-whisper` в Python, указанный в настройке `python`. `ytdlp` нужен только для импорта по ссылке. Если зависимости уже установлены вручную, укажите пути через `config set` (см. ниже) и проверьте результат командой `modules list`.

5. Проверьте окружение:

```bash
   dotnet run -- status
```

   Команда выводит доступность Whisper и `llama-server`, размеры каталогов данных и число лекций в библиотеке. Код возврата `0` означает, что Whisper доступен; `llama-server` при необходимости запускается автоматически при создании конспекта.

6. Обработайте первую лекцию:

```bash
   dotnet run -- import "C:\lectures\lecture01.mp4" --title "Лекция 1"
```

> Для запуска без `dotnet run` используйте собранный `ripple.exe` (каталог `bin\Debug\net10.0-windows10.0.19041.0\`) - тот же набор команд: `ripple <команда> [аргументы]`.

### Конфигурация

Настройки хранятся в `%APPDATA%\Ripple\config.json` и меняются командой `ripple config set <ключ> <значение>`.

| Ключ | Описание | Значение по умолчанию |
|---|---|---|
| `whisper-model` | модель Whisper (`large-v3`, `medium`, `base`, `tiny`) | `large-v3` |
| `python` | путь к `python.exe` | `python` |
| `ffmpeg` | путь к `ffmpeg.exe` | `ffmpeg` |
| `ytdlp` | путь к `yt-dlp.exe` | `yt-dlp` |
| `llama-server` | путь к `llama-server.exe` | `llama-server` |
| `llama-model` | путь к GGUF-модели | пусто |
| `llama-host` | адрес `llama-server` | `127.0.0.1` |
| `llama-port` | порт `llama-server` | `8080` |
| `auto-summarize` | делать конспект после транскрибации (`true`/`false`) | `true` |
| `data-root` | корень тяжёлых данных (аудио, модули, веса Whisper); пусто = `%APPDATA%\Ripple` | `e:\dev\ripple\storage` |

**Переменные окружения:** `RIPPLE_WHISPER_MODEL` - модель Whisper по умолчанию (используется, если она не задана в `config.json`). Других переменных окружения проект не использует.

Для создания конспекта должна быть задана GGUF-модель (`llama-model`); после `modules install gguf` путь подставляется автоматически. Если `llama-server` не запущен, Ripple стартует его сам с параметрами `-c 8192 --jinja -np 1` и ждёт готовности до 2 минут.

## 💡 Примеры использования

Импорт файла и построение конспекта:

```bash
ripple import "C:\lectures\lecture01.mp4" --title "Алгоритмы. Лекция 1"
```

Импорт по ссылке без конспекта (только транскрипт):

```bash
ripple import "https://example.com/video" --no-summary
```

Запись системного звука; `Ctrl+C` останавливает запись и запускает обработку:

```bash
ripple record --output "C:\recordings\lecture.wav"
```

Просмотр библиотеки и конкретной лекции (по первым 8 символам ID):

```bash
ripple list
ripple show a1b2c3d4
ripple show a1b2c3d4 --summary-only
ripple show a1b2c3d4 --transcript-only
```

Повторный запуск лекции после ошибки или отмены:

```bash
ripple retry a1b2c3d4
```

Экспорт:

```bash
ripple export a1b2c3d4 --format md
ripple export a1b2c3d4 --format docx --output "C:\notes\lecture01.docx"
```

Если `--output` не указан, файл сохраняется в `exports\<название>-<id>.<md|docx>` относительно текущего каталога. Экспортируемый документ содержит конспект и дословную расшифровку с таймкодами.

Управление модулями и настройками:

```bash
ripple modules list
ripple modules remove ytdlp
ripple config show
ripple config set whisper-model medium
ripple config set auto-summarize false
```

Полный список команд:

```bash
ripple help
```

## 📁 Структура проекта

```text
ripple/
├── Program.cs            # точка входа, маршрутизация команд
├── ripple.csproj         # .NET 10, NAudio, DocumentFormat.OpenXml
├── CLI/                  # команды: import, record, list/show/retry/delete,
│                         #          export, modules, config, status, справка
├── Models/
│   └── Lecture.cs        # Lecture, TranscriptSegment, статусы и типы источников
└── Services/
    ├── LecturePipeline.cs    # загрузка → транскрибация → конспект
    ├── MediaService.cs       # ffmpeg/ffprobe, yt-dlp
    ├── WhisperService.cs     # вызов openai-whisper через Python
    ├── LlmService.cs         # llama-server, генерация конспекта
    ├── RecordingService.cs   # запись системного звука (NAudio)
    ├── ExportService.cs      # экспорт в Markdown и DOCX
    ├── ModuleService.cs      # установка и проверка внешних модулей
    ├── JsonConfig.cs, JsonLibrary.cs, Storage.cs, AppConfig.cs  # конфигурация и хранилище
    └── ProcessRunner.cs, TempDirClean.cs, ...                   # вспомогательные сервисы
```

Конфигурация (`config.json`), библиотека (`library.json`) и журнал (`ripple.log`) хранятся в `%APPDATA%\Ripple`. Аудио, модули и веса Whisper - в каталоге `data-root`.

Пайплайн обработки:

```text
файл / ссылка / запись → ffmpeg (WAV 16 кГц, моно) → Whisper (ru) → сегменты с таймкодами
                                                                  → gpt-oss-20b (llama-server) → конспект
``