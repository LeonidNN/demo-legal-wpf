# demo-legal-wpf

## Сборка и запуск
1. Откройте `demo-legal-wpf.sln` в Visual Studio 2022 (или используйте `dotnet build`).
2. Проект старта: **DemoLegal.Wpf**.
3. Приложение создаст БД SQLite в `%APPDATA%\DemoLegal\data.sqlite`.
4. В главном окне: выберите **CSV (.csv)** или **Excel (.xlsx)**  **Импорт**  затем **Дела**  **Обновить**  выберите дело  **Собрать досудебный пакет**.
5. Сгенерированные файлы лежат в `Мои документы\DemoLegal\Cases\<CaseId>\`.

## Формат входных данных
- Поддерживаются: **CSV (UTF-8; `;` разделитель; `,`  десятичная)** и **XLSX**.
- Шапка колонок  как в `ExpectedColumns` (см. `src/DemoLegal.Infrastructure/Import/ExpectedColumns.cs`).
