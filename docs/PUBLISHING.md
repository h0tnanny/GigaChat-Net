# Публикация SDK и сопровождение GitHub

Этот документ описывает, как вести `GigaChat.Net` как публичный SDK: где хранить код, как выпускать preview и release NuGet-пакеты, как работать с GitHub Issues/Projects и что нужно настроить перед первой публикацией.

## Один репозиторий или два

Для текущего состояния правильнее оставить один репозиторий `h0tnanny/GigaChat-Net` и выпускать из него два NuGet-пакета:

| Пакет | Проект | Назначение |
| --- | --- | --- |
| `GigaChat.Net` | `src/GigaChat.Net` | Базовый SDK: клиент, модели, auth, streaming, embeddings, files, retry. |
| `GigaChat.Net.AspNetCore` | `src/GigaChat.Net.AspNetCore` | DI, middleware, request context и ASP.NET Core интеграция поверх базового SDK. |

Причины оставить монорепозиторий:

- ASP.NET Core пакет зависит от базового SDK и должен выпускаться совместимой версией.
- Один CI прогон проверяет оба проекта и интеграционные сценарии.
- Issues, bug reports и roadmap проще вести в одном Project.
- Общая документация не расходится между пакетами.

Разделять на два репозитория имеет смысл только если ASP.NET Core пакет начнет жить с независимым релизным циклом, отдельной командой поддержки или существенно другим набором зависимостей. Сейчас это расширение к SDK, поэтому монорепозиторий чище.

README нужны разные по назначению:

- `README.md` в корне - главная документация репозитория и точка входа для GitHub.
- `docs/nuget/GigaChat.Net.md` - короткая страница NuGet для базового пакета.
- `docs/nuget/GigaChat.Net.AspNetCore.md` - короткая страница NuGet для ASP.NET Core пакета.

NuGet README должен быть короче root README: пользователь на NuGet странице должен быстро понять, какой пакет поставить и где лежит полная документация.

## GitHub структура

В репозитории настроены:

- `.github/workflows/ci.yml` - сборка, тесты и упаковка на pull request, push в `master` и ручной запуск.
- `.github/workflows/publish-preview.yml` - публикация preview пакетов на каждый push в `master`.
- `.github/workflows/publish-release.yml` - публикация release пакетов при публикации GitHub Release или ручном запуске.
- `.github/ISSUE_TEMPLATE/bug_report.yml` - шаблон bug report.
- `.github/ISSUE_TEMPLATE/feature_request.yml` - шаблон feature request.
- `.github/ISSUE_TEMPLATE/task.yml` - шаблон task.
- `.github/pull_request_template.md` - checklist для PR.

GitHub Project создается отдельно через GitHub UI или GitHub CLI. В Project удобно держать поля:

| Поле | Тип | Значения |
| --- | --- | --- |
| Status | Single select | `Backlog`, `Ready`, `In Progress`, `Review`, `Done` |
| Type | Single select | `Bug`, `Task`, `Feature`, `Docs`, `CI` |
| Area | Single select | `SDK`, `ASP.NET Core`, `CI/CD`, `Docs`, `Examples` |
| Priority | Single select | `P0`, `P1`, `P2`, `P3` |
| Version | Text | Например `0.1.0-preview`, `0.1.0`, `1.0.0` |

## Secrets

Для публикации в NuGet нужен repository secret:

```text
NUGET_API_KEY
```

Как создать:

1. Откройте nuget.org.
2. Создайте API key со scope `Push`.
3. Ограничьте ключ пакетами `GigaChat.Net*`, если пакеты уже зарезервированы.
4. В GitHub откройте `Settings -> Secrets and variables -> Actions -> New repository secret`.
5. Назовите secret `NUGET_API_KEY`.
6. Вставьте значение API key.

Секрет нельзя хранить в репозитории, README, issue или workflow logs.

## CI

Workflow `CI` запускается на:

- pull request;
- push в `master`;
- ручной `workflow_dispatch`.

Он делает:

```bash
dotnet restore GigaChat.Net.slnx
dotnet build GigaChat.Net.slnx --configuration Release --no-restore
dotnet test GigaChat.Net.slnx --configuration Release --no-build
dotnet pack src/GigaChat.Net/GigaChat.Net.csproj --configuration Release --no-build
dotnet pack src/GigaChat.Net.AspNetCore/GigaChat.Net.AspNetCore.csproj --configuration Release --no-build
```

В CI используется временная версия пакета:

```text
0.0.0-ci.<github.run_number>.<github.run_attempt>
```

Такие пакеты не публикуются в NuGet. Они прикладываются к workflow run как artifact, чтобы можно было скачать и проверить локально.

## Preview публикация

Workflow `Publish Preview NuGet Packages` запускается на каждый push в `master` и вручную.

Версия preview формируется автоматически:

```text
0.1.0-preview.<github.run_number>.<github.run_attempt>
```

Preview workflow:

1. Проверяет наличие `NUGET_API_KEY`.
2. Восстанавливает зависимости.
3. Собирает решение в `Release`.
4. Запускает тесты.
5. Упаковывает оба проекта.
6. Публикует `.nupkg` и `.snupkg` в nuget.org.

Установка preview версии:

```bash
dotnet add package GigaChat.Net --version 0.1.0-preview.<run>.<attempt>
dotnet add package GigaChat.Net.AspNetCore --version 0.1.0-preview.<run>.<attempt>
```

Preview пакеты подходят для проверки интеграции до стабильного релиза. Их не стоит считать контрактом совместимости.

## Release публикация

Workflow `Publish Release NuGet Packages` запускается двумя способами.

Первый способ - GitHub Release:

```bash
git tag v0.1.0
git push origin v0.1.0
```

Затем на GitHub создайте Release из tag `v0.1.0` и нажмите `Publish release`. Workflow возьмет tag, удалит начальную `v` и опубликует NuGet версию `0.1.0`.

Второй способ - ручной запуск workflow:

1. Откройте `Actions`.
2. Выберите `Publish Release NuGet Packages`.
3. Нажмите `Run workflow`.
4. Укажите `version`, например `0.1.0`.

Release workflow принимает SemVer:

```text
1.0.0
1.0.0-rc.1
1.2.3-preview.4
```

Если версия уже опубликована в NuGet, перезаписать ее нельзя. Нужно выпустить новую версию.

## Рекомендуемый релизный процесс

1. Вести разработку в feature branch.
2. Открывать PR в `master`.
3. Дождаться зеленого `CI`.
4. Слить PR в `master`.
5. Дождаться публикации preview пакетов.
6. Проверить preview в реальном приложении или example.
7. Создать release tag `vX.Y.Z`.
8. Опубликовать GitHub Release.
9. Дождаться `Publish Release NuGet Packages`.
10. Проверить страницы пакетов на nuget.org.

## Локальная проверка перед PR

```bash
dotnet restore GigaChat.Net.slnx
dotnet build GigaChat.Net.slnx --configuration Release --no-restore
dotnet test GigaChat.Net.slnx --configuration Release --no-build
dotnet pack src/GigaChat.Net/GigaChat.Net.csproj --configuration Release --no-build --output artifacts/packages /p:PackageVersion=0.1.0-local
dotnet pack src/GigaChat.Net.AspNetCore/GigaChat.Net.AspNetCore.csproj --configuration Release --no-build --output artifacts/packages /p:PackageVersion=0.1.0-local
```

Локальная установка из папки:

```bash
dotnet nuget add source ./artifacts/packages --name GigaChatLocal
dotnet add package GigaChat.Net --version 0.1.0-local --source ./artifacts/packages
dotnet add package GigaChat.Net.AspNetCore --version 0.1.0-local --source ./artifacts/packages
```

## Версионирование

До первого стабильного релиза используйте ветку `0.x`:

- `0.1.0-preview.N` - автоматические preview сборки из `master`.
- `0.1.0` - первый стабильный публичный release.
- `0.2.0` - новые совместимые возможности.
- `0.1.1` - исправления без изменения публичного API.

После `1.0.0` желательно следовать SemVer:

- `MAJOR` - breaking changes.
- `MINOR` - новые возможности без breaking changes.
- `PATCH` - bug fixes.

Если `GigaChat.Net.AspNetCore` зависит от `GigaChat.Net`, выпускайте оба пакета одной версией. Это снижает риск, что пользователь установит несовместимые версии.

## Labels

Рекомендуемые labels:

| Label | Назначение |
| --- | --- |
| `type:bug` | Ошибка или регрессия. |
| `type:task` | Техническая задача без нового пользовательского поведения. |
| `type:feature` | Новая возможность. |
| `type:docs` | Документация. |
| `area:sdk` | Базовый SDK. |
| `area:aspnetcore` | ASP.NET Core интеграция. |
| `area:ci` | GitHub Actions, NuGet, packaging. |
| `area:examples` | Example проекты. |
| `priority:p0` | Критично. |
| `priority:p1` | Высокий приоритет. |
| `priority:p2` | Обычный приоритет. |
| `priority:p3` | Низкий приоритет. |

## Troubleshooting

`NUGET_API_KEY repository secret is required`

Secret не создан или недоступен workflow. Создайте repository secret `NUGET_API_KEY`.

`Package already exists`

NuGet не позволяет перезаписывать опубликованную версию. Увеличьте version/tag.

`No .nupkg files were produced`

Проверьте шаг `dotnet pack`. Обычно причина в ошибке сборки, неверном target framework или отсутствующем README файле, указанном в `PackageReadmeFile`.

`The package does not contain a readme`

Проверьте, что `PackageReadmeFile` указывает на `README.md`, а соответствующий `None Include=... Pack="true" PackagePath="README.md"` действительно попадает в пакет.

`401 Unauthorized` при публикации

Проверьте, что NuGet API key имеет scope `Push`, не истек и разрешает публикацию нужных package IDs.

## Полезные официальные ссылки

- NuGet publish: https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package
- `dotnet pack`: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-pack
- `dotnet nuget push`: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-push
- NuGet package README: https://learn.microsoft.com/en-us/nuget/nuget-org/package-readme-on-nuget-org
- GitHub Actions for .NET: https://docs.github.com/actions/automating-builds-and-tests/building-and-testing-net
- GitHub Actions secrets: https://docs.github.com/en/actions/concepts/security/secrets
- GitHub Projects: https://docs.github.com/issues/planning-and-tracking-with-projects/creating-projects/creating-a-project
