# BlackBoxIdentificationStarter

Каркас дипломной программы на C# WinForms.

## Что уже есть

- главное окно WinForms;
- загрузка Excel `.xlsx`;
- демо-данные без Excel;
- выбор метода: Collocation / LeastSquares;
- таблица входных данных;
- графики `x(t)`, `y(t)`, восстановленного `ŷ(t)` и невязки;
- таблицы коэффициентов `A[i]`, `C[i,j]`;
- экспорт результатов в CSV.

## Важно

Математическое ядро сейчас является стартовым приближением. Оно сделано так, чтобы проект запускался и чтобы можно было постепенно заменить внутренние формулы на точную схему из Maple.

Основные места для доработки:

- `MathModel/CollocationIdentifier.cs`
- `MathModel/LeastSquaresIdentifier.cs`
- `MathModel/DesignMatrixBuilder.cs`
- `MathModel/VolterraModelEvaluator.cs`

## Как открыть

1. Установить Visual Studio 2022.
2. В Visual Studio Installer выбрать workload: `.NET desktop development`.
3. Открыть `src/BlackBoxIdentification/BlackBoxIdentification.csproj`.
4. Нажать Restore NuGet packages, если Visual Studio не сделает это сама.
5. Запустить проект.

## Формат Excel

Поддерживаются два простых формата:

```text
x(t) | y(t)
```

или

```text
t | x(t) | y(t)
```

Если времени нет, программа создаёт его как `0, 1, 2, ...`.
