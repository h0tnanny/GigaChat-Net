# Правила ведения репозитория

Этот репозиторий ведется по простой модели `develop -> master`.

## Ветки

- `master` - основная стабильная ветка. В нее попадает только код, готовый к release.
- `develop` - интеграционная ветка разработки. Из нее публикуются preview пакеты.
- Все feature, bugfix, docs, ci и refactor ветки создаются от `develop`.
- После merge завершенные ветки удаляются. Исключения: `master`, `develop` и git tags.

Формат веток:

```text
feature/GN-123-short-description
bugfix/GN-123-short-description
hotfix/GN-123-short-description
docs/GN-123-short-description
ci/GN-123-short-description
refactor/GN-123-short-description
test/GN-123-short-description
chore/GN-123-short-description
```

`GN-123` - это GitHub issue number в формате проекта. Например, issue `#123`
ведется в ветках и коммитах как `GN-123`.

В `master` можно открывать PR только из `develop`, `release/GN-123-short-description`
или `hotfix/GN-123-short-description`.

## Коммиты

Subject каждого коммита должен начинаться с номера задачи и типа изменения:

```text
[GN-123] feat: add chat request headers
[GN-123] fix: handle model override fallback
[GN-123] docs: describe release process
[GN-123] test: cover request context headers
[GN-123] ci: publish preview packages from develop
```

Разрешенные типы:

- `feat` - новая возможность.
- `fix` - исправление бага.
- `docs` - документация.
- `test` - тесты.
- `refactor` - рефакторинг без изменения поведения.
- `ci` - GitHub Actions, публикация, сборка.
- `chore` - техническое сопровождение.
- `perf` - оптимизация производительности.
- `build` - сборка или package metadata.
- `revert` - откат изменения.

Если коммит делает архитектурное решение, добавляйте в body короткие trailers
с причиной, ограничением, проверками и рисками. Subject все равно должен начинаться
с `[GN-123] type: ...`.

## GitHub Project

Каждая работа начинается с GitHub issue и добавляется в GitHub Project.

Рекомендуемый поток статусов:

```text
Backlog -> Ready -> In Progress -> Review -> Done
```

Правила:

- Перед началом работы назначьте issue и переведите статус в `In Progress`.
- Перед PR переведите задачу в `Review`.
- После merge и проверки CI переведите задачу в `Done`.
- Для bug report в issue должно быть указано ожидаемое и фактическое поведение.
- Для feature в issue должны быть acceptance criteria.

## Pull Request

Основной поток:

```text
feature/* -> develop -> master
```

Перед PR в `develop` обязательно:

- выполнить self-review;
- проверить, что реализация точно соответствует issue;
- проверить безопасность изменения;
- добавить или обновить тесты;
- запустить `dotnet build`;
- запустить `dotnet test`;
- перевести задачу в Project в `Review`.

Без тестов код не проходит review. Исключение допустимо только для чистой документации
или repository metadata, и причина должна быть явно написана в PR.

Code review должен проверить:

- нет ли утечки секретов, credentials, tokens или персональных данных;
- нет ли небезопасной обработки headers, URLs, файлов, сертификатов или HTTP-клиента;
- не расширены ли права доступа без причины;
- реализация закрывает acceptance criteria задачи;
- баг действительно воспроизведен тестом или покрыт regression test;
- публичный API и package metadata не ломают пользователей без необходимости.

Пока репозиторий ведется одним владельцем и нет второго contributor с правом review,
обязательный GitHub approving review в branch protection отключен. Self-review, PR checklist,
`Build, test, pack` и `Validate PR policy` остаются обязательными. Когда появится второй
maintainer или постоянный contributor, required approving review нужно включить обратно
для `develop` и `master`.

## Preview

Preview пакеты публикуются из ветки `develop`.

Каждый успешный preview publish:

- собирает и тестирует решение;
- публикует `.nupkg` и `.snupkg` в NuGet.org;
- публикует `.nupkg` в GitHub Packages;
- создает annotated tag:

```text
preview/v0.1.0-preview.<run>.<attempt>
```

## Release

Release публикуется только из `master` и только по stable tag:

```text
v1.0.0
v1.2.3
```

Release tag должен указывать на commit, который содержится в `master`.
Preview версии не публикуются из `master`.

Рекомендуемый release flow:

1. Слить feature/bugfix PR в `develop`.
2. Дождаться preview publish из `develop`.
3. Проверить preview пакет в example или реальном приложении.
4. Открыть PR `develop -> master`.
5. Дождаться CI и code review.
6. Слить PR в `master`.
7. Поставить stable tag на commit из `master`.
8. Запушить tag.
9. Дождаться release publish.
10. Удалить завершенные feature/release/hotfix ветки.

## Локальная проверка

```bash
dotnet restore GigaChat.Net.slnx
dotnet build GigaChat.Net.slnx --configuration Release --no-restore
dotnet test GigaChat.Net.slnx --configuration Release --no-build
dotnet pack src/GigaChat.Net/GigaChat.Net.csproj --configuration Release --no-build --output artifacts/packages /p:PackageVersion=0.1.0-local
dotnet pack src/GigaChat.Net.AspNetCore/GigaChat.Net.AspNetCore.csproj --configuration Release --no-build --output artifacts/packages /p:PackageVersion=0.1.0-local
```
