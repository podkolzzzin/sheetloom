# Sheetloom

*Excel files from templates and data.*

**JSON + Vue-шаблон → готовий `.xlsx`.** Описуй аркуші, типи клітинок, формули та друк звичайними Vue-компонентами. Один контейнер містить Node.js і .NET-рендерер; Excel на сервері не потрібен.

## Спробувати за хвилину

Потрібні Docker, Bash і curl. Виконай із кореня репозиторію:

```bash
docker build -t sheetloom:local .
docker run --rm -p 127.0.0.1:8080:8080 \
  -v "$PWD/examples/basic:/templates:ro" sheetloom:local
```

Після повідомлення `ready` виконай в іншому терміналі:

```bash
curl --fail-with-body http://localhost:8080/v1/render/invoice \
  -H 'Content-Type: application/json' \
  --data-binary @examples/basic/request.json -o invoice.xlsx
```

Відкрий `invoice.xlsx`: аркуш **Invoice** містить замовника, дві позиції та формулу підсумку на **50**. Формули обчислює Excel або сумісна програма після відкриття. Для автоматичного запуску й зупинки демо є `bash scripts/demo.sh`; файл залишиться в `.artifacts/demo/`.

## Як це влаштовано

У [manifest.json](examples/basic/manifest.json) ID `invoice` вказує на [Invoice.vue](examples/basic/Invoice.vue). HTTP-тіло має форму `{"data": {...}}`; шаблон отримує саме `data` через `inject('ssrRequest').body`. Vue створює розмітку `Workbook → Worksheet → Row → Cell`, а ClosedXML перетворює її на Excel.

Шаблони монтуються окремо від рушія. Після зміни пакета перезапусти контейнер — перебудовувати образ не потрібно. Дані кожного запиту обробляються в окремому Vue worker.

| Хочу… | Короткий how-to |
| --- | --- |
| Створити власний експорт | [Пакет і шаблон](docs/how-to/01-template-pack.md) |
| Зберегти числа, дати й початкові нулі | [Клітинки та стилі](docs/how-to/02-cell-types.md) |
| Додати підсумки | [Формули й порожні списки](docs/how-to/03-formulas.md) |
| Налаштувати папір і заголовки | [Друк і закріплення](docs/how-to/04-printing.md) |
| Викликати сервіс зі свого backend | [HTTP-клієнт і помилки](docs/how-to/05-http-client.md) |

Пакети — **довірений виконуваний код**. Сервіс не має автентифікації: використовуй приватну мережу або свій захищений gateway.

[Усі XML-теги](docs/workbook-format.md) · [API та налаштування](docs/operations.md) · [Розробка](CONTRIBUTING.md) · [Безпека](SECURITY.md)

Ліцензія [MIT](LICENSE). Залежності мають [власні ліцензії](THIRD_PARTY_NOTICES.md).
