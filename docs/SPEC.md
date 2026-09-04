# Спецификация Sensitivity Randomizer

## 1. Назначение

Локальная Windows-утилита для variable-practice тренировки моторной адаптации в
любой игре. Программа периодически выбирает новый постоянный multiplier и
применяет его через Raw Accel.

Программа не должна:

- читать или писать память игры;
- внедрять DLL в игровой процесс;
- генерировать движения мыши или нажатия;
- менять настройку чувствительности внутри игры;
- менять физический DPI устройства;
- рассчитывать multiplier из скорости движения мыши.

## 2. Модель чувствительности

Единственная записываемая величина:

```text
Raw Accel profile.outputDPI = multiplier × 1000
```

Пользовательские `Mouse DPI` и `Base sensitivity` нужны только для отображения:

```text
effective_sensitivity = base_sensitivity × multiplier
effective_eDPI = mouse_DPI × base_sensitivity × multiplier
```

Между запланированными сменами gain обязан оставаться постоянным. Любая активная
speed-based acceleration curve блокирует запуск.

## 3. Режимы

### Balanced Coverage

Default-режим. Логарифмические отрезки `[min, 1)` и `(1, max]` делятся на четыре
зоны каждый. Один цикл содержит по одному случайному значению из каждой зоны и
перемешивается Fisher-Yates. Инварианты полного цикла:

- восемь значений;
- четыре ниже и четыре выше `1.000`;
- каждая зона используется один раз;
- при `min × max = 1` геометрическое среднее равно `1.000`.

### Smooth Random Walk

```text
delta = uniform(-maximum_step, maximum_step)
drift = (1 - current) × return_strength
next = clamp(current + delta + drift, min, max)
```

### Centered Gaussian

Две стороны центра выбираются с вероятностью `0.5`. Для асимметричных границ
scale дальней стороны калибруется так, чтобы условные средние расстояния были
равны. Используется rejection sampling без pile-up на границах.

### Linear Uniform

Классический линейный baseline:

```text
next = min + (max - min) × U[0,1)
```

## 4. Диапазоны и интервалы

- multiplier: `0.10000-10.00000`;
- minimum обязан быть меньше maximum;
- Balanced Coverage требует `min < 1 < max`;
- reciprocal link поддерживает `min × max = 1` с точностью пяти знаков;
- интервал может быть fixed или uniform random;
- технический минимум интервала: `1.25 s`;
- максимум: `3600 s`.

## 5. Сессия

- Start выполняет preflight readback и проверку constant gain;
- Pause замораживает текущее значение без reset;
- Resume продолжает последовательность;
- Stop, Reset, таймер и штатное закрытие возвращают `1.000`;
- optional recalibration удерживает `1.000` после randomized phase;
- PCG32 и seed обеспечивают воспроизводимость multiplier и интервалов;
- CSV и JSON логи сохраняются только локально.

## 6. Безопасность записи

Для каждой смены:

```text
GetActive
  -> проверить noaccel во всех профилях
  -> изменить только outputDPI
  -> Activate
  -> GetActive
  -> проверить noaccel и requested multiplier во всех профилях
```

Все операции с драйвером сериализуются одним `SemaphoreSlim`. Reset использует
отдельный safety path и не отменяет уже выполняющуюся запись. ResetGuard
вооружается только перед первой разрешённой записью и повторяет reset после
аварийного завершения основного процесса.

## 7. Интерфейс

Активный GUI реализован на WPF без WinForms-компонентов. Требования:

- компактный dashboard с live multiplier и effective calculations;
- Essential, Optional и Guide;
- скрытие нерелевантных mode-specific полей;
- самостоятельный вертикальный scroll внутри страницы;
- рабочий layout от `900 × 640`;
- отсутствие resize callbacks и ручного per-pixel layout;
- live chart не перерисовывается в idle;
- короткие opacity/transform-анимации только при появлении и hover;
- никакой layout-анимации во время resize.

## 8. Темы и локализация

Темы:

- Dark;
- Light;
- Pastel.

Языки:

- Русский;
- English;
- Deutsch;
- Español;
- Polski;
- Українська.

Выбор языка и темы сохраняется. Системный язык используется при первом запуске,
если он поддерживается, иначе выбирается английский.

## 9. Конфигурации

Локальная схема настроек имеет версию 7. Portable envelope:

```text
Format = SensitivityRandomizerConfig
FormatVersion = 1
ExportedUtc
Settings
```

Импорт ограничен 1 MiB, требует корректный JSON, поддерживаемую версию и полную
валидацию. Импорт не пишет в драйвер до Start. При обновлении читаются прежний
локальный config, старое имя base-sensitivity property и старый portable marker.

## 10. Платформа и релиз

- Windows 10/11 x64;
- .NET Framework 4.7.2+;
- Raw Accel v1.7.x ABI;
- Microsoft Visual C++ Redistributable 2015-2022 x64;
- основной EXE и ResetGuard собираются строго x64;
- оба EXE получают один многоразмерный ICO;
- ZIP содержит бинарники, зависимости, SHA-256, документацию и исходники.

## 11. Проверка

Автотесты покрывают алгоритмы, миграции, границы, статистику, отсутствие
mouse-speed input channel, reset paths, языки и темы. Статический UI-check
запрещает WinForms, resize handlers, recursive layout и reentrant Close.

Финальный hardware smoke test выполняется на Windows с реально установленным
Raw Accel: Start, две смены, Pause, Reset/readback, затем аварийное завершение
основного EXE с проверкой ResetGuard.
