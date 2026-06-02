# Публикация SDK и сопровождение GitHub

Этот документ описывает, как вести `GigaChat.Net` как публичный SDK: где хранить код, как выпускать preview и release NuGet-пакеты, как работать с GitHub Issues/Projects и что нужно настроить перед первой публикацией.

Общие правила веток, коммитов, PR, code review и Project описаны в `CONTRIBUTING.md`.

NuGet.org остается основным публичным registry для пользователей .NET. GitHub Packages используется как дополнительный registry, чтобы пакеты были видны во вкладке `Packages` репозитория GitHub и могли устанавливаться из GitHub Package Registry.

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

- `.github/workflows/ci.yml` - сборка, тесты и упаковка на pull request, push в `master`/`develop` и ручной запуск.
- `.github/workflows/repository-policy.yml` - проверка PR target branch, branch name, PR title и commit subjects.
- `.github/workflows/publish-preview.yml` - публикация preview пакетов на каждый push в `develop` в NuGet.org и GitHub Packages с созданием preview tag.
- `.github/workflows/publish-release.yml` - публикация release пакетов при push stable tag `vX.Y.Z` или публикации GitHub Release в NuGet.org и GitHub Packages.
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
| Version | Text | Например `1.0.2-preview`, `1.0.2`, `1.1.0` |

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

Для публикации в GitHub Packages отдельный secret не нужен. Workflow использует встроенный `${{ secrets.GITHUB_TOKEN }}` и право:

```yaml
permissions:
  contents: read
  packages: write
```

Адрес GitHub Packages NuGet registry для этого аккаунта:

```text
https://nuget.pkg.github.com/h0tnanny/index.json
```

## CI

Workflow `CI` запускается на:

- pull request;
- push в `master`;
- push в `develop`;
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

Workflow `Publish Preview NuGet Packages` запускается на каждый push в `develop` и вручную только с ref `develop`.

Версия preview формируется автоматически:

```text
1.0.2-preview.<github.run_number>.<github.run_attempt>
```

Preview workflow:

1. Проверяет наличие `NUGET_API_KEY`.
2. Восстанавливает зависимости.
3. Собирает решение в `Release`.
4. Запускает тесты.
5. Упаковывает оба проекта.
6. Публикует `.nupkg` и `.snupkg` в nuget.org.
7. Публикует `.nupkg` в GitHub Packages.
8. Создает annotated tag вида `preview/v1.0.2-preview.<run>.<attempt>`.

GitHub Packages не является зеркалом NuGet.org. Даже если NuGet пакет содержит `RepositoryUrl` и связан с GitHub репозиторием, вкладка GitHub `Packages` покажет пакет только после отдельной публикации в GitHub Packages. Поэтому workflow публикует один и тот же `.nupkg` в оба registry.

Установка preview версии:

```bash
dotnet add package GigaChat.Net --version 1.0.2-preview.<run>.<attempt>
dotnet add package GigaChat.Net.AspNetCore --version 1.0.2-preview.<run>.<attempt>
```

Preview пакеты подходят для проверки интеграции до стабильного релиза. Их не стоит считать контрактом совместимости.

Установка preview из GitHub Packages обычно требует authenticated source:

```bash
dotnet nuget add source "https://nuget.pkg.github.com/h0tnanny/index.json" \
  --name github \
  --username "<github-user>" \
  --password "<github-token>" \
  --store-password-in-clear-text

dotnet add package GigaChat.Net --version 1.0.2-preview.<run>.<attempt> --source github
dotnet add package GigaChat.Net.AspNetCore --version 1.0.2-preview.<run>.<attempt> --source github
```

Для обычных пользователей предпочтительнее установка из NuGet.org без дополнительного source.

## Release публикация

Workflow `Publish Release NuGet Packages` запускается двумя способами.

Первый способ - stable tag из `master`:

```bash
git tag v1.0.2
git push origin v1.0.2
```

Workflow возьмет tag, удалит начальную `v` и опубликует NuGet версию `1.0.2`.
Release tag должен указывать на commit, который содержится в `master`.

Второй способ - GitHub Release:

1. Создайте tag `vX.Y.Z` из `master`.
2. Откройте GitHub Releases.
3. Создайте Release из этого tag.
4. Нажмите `Publish release`.

Release workflow принимает только stable SemVer:

```text
1.0.0
1.2.3
```

Если версия уже опубликована в NuGet.org или GitHub Packages, перезаписать ее нельзя. Нужно выпустить новую версию.

## Рекомендуемый релизный процесс

1. Создать GitHub issue и добавить его в Project.
2. Создать ветку `feature/GN-123-short-description` или `bugfix/GN-123-short-description` от `develop`.
3. Вести коммиты в формате `[GN-123] feat: short description`.
4. Открыть PR в `develop`.
5. Перевести Project status в `Review`.
6. Пройти code review, security review и проверку тестового покрытия.
7. Слить PR в `develop`.
8. Дождаться публикации preview пакетов и preview tag.
9. Проверить preview в реальном приложении или example.
10. Открыть PR `develop -> master`.
11. Дождаться зеленого `CI` и code review.
12. Слить PR в `master`.
13. Создать release tag `vX.Y.Z` на commit из `master`.
14. Запушить tag и дождаться `Publish Release NuGet Packages`.
15. Проверить страницы пакетов на nuget.org.
16. Проверить, что пакеты появились во вкладке `Packages` репозитория GitHub.
17. Удалить завершенные feature, bugfix, release и hotfix ветки.

## Локальная проверка перед PR

```bash
dotnet restore GigaChat.Net.slnx
dotnet build GigaChat.Net.slnx --configuration Release --no-restore
dotnet test GigaChat.Net.slnx --configuration Release --no-build
dotnet pack src/GigaChat.Net/GigaChat.Net.csproj --configuration Release --no-build --output artifacts/packages /p:PackageVersion=1.0.2-local
dotnet pack src/GigaChat.Net.AspNetCore/GigaChat.Net.AspNetCore.csproj --configuration Release --no-build --output artifacts/packages /p:PackageVersion=1.0.2-local
```

Локальная установка из папки:

```bash
dotnet nuget add source ./artifacts/packages --name GigaChatLocal
dotnet add package GigaChat.Net --version 1.0.2-local --source ./artifacts/packages
dotnet add package GigaChat.Net.AspNetCore --version 1.0.2-local --source ./artifacts/packages
```

## Версионирование

После `1.0.0` следуйте SemVer:

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

NuGet.org и GitHub Packages не позволяют перезаписывать опубликованную версию. Увеличьте version/tag.

`No .nupkg files were produced`

Проверьте шаг `dotnet pack`. Обычно причина в ошибке сборки, неверном target framework или отсутствующем README файле, указанном в `PackageReadmeFile`.

`The package does not contain a readme`

Проверьте, что `PackageReadmeFile` указывает на `README.md`, а соответствующий `None Include=... Pack="true" PackagePath="README.md"` действительно попадает в пакет.

`401 Unauthorized` при публикации

Для NuGet.org проверьте, что NuGet API key имеет scope `Push`, не истек и разрешает публикацию нужных package IDs.

Для GitHub Packages проверьте, что workflow содержит `permissions: packages: write`, а публикация идет через `${{ secrets.GITHUB_TOKEN }}` или token с `write:packages`.

Пакет появился на NuGet.org, но не появился в GitHub `Packages`

NuGet.org не синхронизирует пакеты в GitHub Packages. Проверьте шаг `Publish packages to GitHub Packages` в workflow run. Если шаг не запускался или упал, версия должна быть опубликована в GitHub Packages отдельным push той же `.nupkg`.

## Полезные официальные ссылки

- NuGet publish: https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package
- `dotnet pack`: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-pack
- `dotnet nuget push`: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-push
- NuGet package README: https://learn.microsoft.com/en-us/nuget/nuget-org/package-readme-on-nuget-org
- GitHub Actions for .NET: https://docs.github.com/actions/automating-builds-and-tests/building-and-testing-net
- GitHub Actions secrets: https://docs.github.com/en/actions/concepts/security/secrets
- GitHub Packages with Actions: https://docs.github.com/packages/managing-github-packages-using-github-actions-workflows/publishing-and-installing-a-package-with-github-actions
- GitHub Packages NuGet registry: https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry
- GitHub Projects: https://docs.github.com/issues/planning-and-tracking-with-projects/creating-projects/creating-a-project
