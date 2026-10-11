# Як отримати Excel зі свого backend

[← README](../../README.md) · Приклад у C# для асинхронного методу; `http` — наявний `HttpClient` із `BaseAddress` сервісу:

```csharp
using System.Net.Http.Json;

var body = new
{
    data = new
    {
        customer = "Example",
        items = new[] { new { name = "Paper", amount = 12.5 } }
    }
};
using var response = await http.PostAsJsonAsync("/v1/render/invoice", body);
response.EnsureSuccessStatusCode();
await using var output = File.Create("invoice.xlsx");
await response.Content.CopyToAsync(output);
```

**Результат:** `invoice.xlsx` містить готову книгу. Перевірка статусу виконується до створення файлу, тому JSON із помилкою не буде збережений як Excel.

| Статус | Що робити |
| --- | --- |
| `400` | Перевір обгортку `data` та типи `options`. |
| `404` | Звір ID із `GET /v1/templates`. |
| `413` | Зменш запит або розмір розмітки; перевір ліміти. |
| `422` | Перевір дані шаблону й XML, який він генерує. |
| `502` / `503` / `504` | Перевір доступність, навантаження та час рендерингу; повторюй із затримкою лише коли це доречно. |

Помилки мають форму `{"error":{"code":"...","message":"..."}}`. За потреби прочитай її до `EnsureSuccessStatusCode()` і перетвори на помилку свого застосунку. `GET /health` перевіряє доступність процесів, але не наявність вільного слота. У Docker звертайся за ім'ям контейнера в спільній мережі; `localhost` усередині іншого контейнера не веде до рушія.
