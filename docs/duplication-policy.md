# Політика дублювання даних та узгодженості (CRM для малого бізнесу)

У мікросервісній архітектурі з патерном **Database per Service** прямі SQL JOIN або зовнішні ключі (FOREIGN KEY) між базами даних суворо заборонені. Для забезпечення максимальної автономності та швидкодії сервісів застосовується контрольована денормалізація та дублювання даних за трьома чіткими стратегіями:
1. **Незмінні фінансові та історичні знімки (Snapshots, не оновлюються)**;
2. **Локальні копії / проекції для читання (Read Copies, оновлюються асинхронними подіями)**;
3. **Ідентифікатори без міжсервісних зв'язків (No-FK Identifiers)**.

---

## 1. Матриця політики дублювання даних (6 стовпців)

Таблиця покриває всі три пари взаємодіючих контекстів:
1. **SalesDb ↔ CatalogDb**
2. **SalesDb ↔ ActivitiesDb**
3. **CatalogDb ↔ ActivitiesDb**

| Сутність / поле | Власник (Bounded Context) | Споживач (Bounded Context) | Тип дублювання | Механізм оновлення / синхронізації | Обґрунтування |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`DealItems.ProductName`, `DealItems.UnitPrice`** | CatalogDb (`Product.Name`, `Product.Price`) | SalesDb (`DealItems`) | **Знімок (snapshot, не оновлюється)** | Не оновлюється (фіксується в момент виклику процедури `usp_CreateDeal` або `usp_AddDealItem`) | **Фінансовий інваріант:** зафіксована в договорі / КП вартість робіт повинна залишатися незмінною, навіть якщо студія згодом змінить тарифи в прайс-листі. |
| **`Products.Name`, `Products.Price`, `Products.IsActive`** | CatalogDb (`Product`) | SalesDb (`Products` — локальна копія) | **Копія для читання (read copy, оновлюється подіями)** | Асинхронний брокер повідомлень (RabbitMQ / Kafka) через Outbox Pattern: події `ProductCreated`, `ProductPriceChanged`, `ProductDeactivated` | **Автономність продажів:** створення угод та перевірка актуальних цін відбуваються локально без синхронних мережевих викликів до каталогу (High Availability). |
| **`DealItems.ProductId`, `Products.Id`** | CatalogDb (`Product.Id`) | SalesDb (`Products.Id`, `DealItems.ProductId`) | **Ідентифікатор без зв'язку (FK відсутній)** | Не оновлюється (передається у подіях та запитах) | **Ізоляція БД:** поле `BIGINT` без FK унеможливлює міжсервісні блокування транзакцій та каскадні падіння. |
| **`activities.clientName`** | SalesDb (`Clients.CompanyName`) | ActivitiesDb (`activities.clientName`) | **Копія для читання (read copy, оновлюється подіями)** | Асинхронна черга повідомлень: обробник події `ClientUpdatedEvent` від SalesDb | **Швидкодія UI:** миттєвий рендеринг стрічки активностей та повнотекстовий пошук за назвою компанії клієнта без запитів до SalesDb. |
| **`activities.dealTitle`** | SalesDb (`Deals.Title`) | ActivitiesDb (`activities.dealTitle`) | **Знімок (snapshot на момент створення)** | Не оновлюється (або оновлюється подією `DealTitleChangedEvent`) | **Контекстний знімок:** відображає назву угоди, якої стосувався дзвінок чи зустріч на момент їх проведення. |
| **`activities.author.displayName`, `discussion_messages.author.displayName`** | SalesDb (`TeamMembers.FullName`) | ActivitiesDb (`activities.author`, `discussion_messages.author`) | **Знімок (snapshot, не оновлюється)** | Не оновлюється (фіксується на момент створення активності / повідомлення) | **Історична достовірність:** фіксує ім'я автора замітки чи дзвінка без потреби масового оновлення документів у разі зміни прізвища чи ролі співробітника. |
| **`client_timelines.recentActivities[]`** | ActivitiesDb (`activities`) | ActivitiesDb (`client_timelines.recentActivities`) | **Знімок (snapshot останніх 5–10 подій)** | Внутрішнє атомарне оновлення при вставці активності: оператор `$push` з модифікатором `$slice: -5` | **O(1) доступ:** миттєвий вивід віджета останніх взаємодій у картці клієнта без агрегації та сканування великої колекції `activities`. |
| **`activities.clientId`, `activities.dealId`, `activities.author.userId`, `discussions.clientId`, `discussions.dealId`, `client_timelines.clientId`** | SalesDb (`Clients.Id`, `Deals.Id`, `TeamMembers.Id`) | ActivitiesDb | **Ідентифікатор без зв'язку (FK відсутній)** | Не оновлюється (скалярні числа `NumberLong` / `BIGINT`) | **Поліглотна незалежність:** між реляційною SQL Server та документною MongoDB фізично не існує зовнішніх ключів. Цілісність забезпечується рівнем додатків. |
| **`activities.relatedProducts[].name`** | CatalogDb (`Product.Name`) | ActivitiesDb (`activities.relatedProducts[].name`) | **Копія для читання (read copy, оновлюється подіями)** | Асинхронний брокер повідомлень: споживач події `ProductNameChangedEvent` від CatalogDb | **Зручність перегляду:** менеджер бачить, про які саме послуги йшла мова на зустрічі, не роблячи звернень до мікросервісу каталогу. |
| **`activities.relatedProducts[].productId`** | CatalogDb (`Product.Id`) | ActivitiesDb (`activities.relatedProducts[].productId`) | **Ідентифікатор без зв'язку (FK відсутній)** | Не оновлюється (скалярний `NumberLong`) | **Ізоляція контекстів:** зв'язує активність з послугою без порушення кордонів каталог/комунікації. |

---

## 2. Деталізація реалізації стратегій

### 2.1. Фінансовий знімок (Price Snapshot Pattern)
У T-SQL збережених процедурах `dbo.usp_CreateDeal` та `dbo.usp_AddDealItem` назва та ціна послуги копіюються безпосередньо з локальної таблиці `dbo.Products`:
```sql
INSERT INTO dbo.DealItems (DealId, ProductId, ProductName, UnitPrice, Discount, Quantity)
SELECT @DealId, p.Id, p.Name, p.Price, @Discount, @Quantity
FROM dbo.Products p
WHERE p.Id = @ProductId AND p.IsActive = 1;
```
Завдяки цьому клієнт отримує комерційну пропозицію та договір саме з тими цінами, які діяли на момент укладання, що захищає від фінансових розбіжностей.

### 2.2. Асинхронна синхронізація через події (Eventual Consistency & Outbox Pattern)
1. Коли керівник студії оновлює тарифи в `CatalogDb`, мікросервіс каталогу фіксує зміну у власній БД та зберігає подію в таблиці `Outbox`:
   `ProductPriceChangedEvent(ProductId, NewPrice)`.
2. Фоновий реле (наприклад, Debezium або Outbox Publisher) передає подію до брокера повідомлень (RabbitMQ / Kafka).
3. Споживач контексту «Продажі» читає повідомлення та оновлює локальну репліку:
   ```sql
   UPDATE dbo.Products 
   SET Price = @NewPrice, UpdatedAt = SYSUTCDATETIME() 
   WHERE Id = @ProductId;
   ```
4. Раніше створені угоди (`DealItems`) залишаються зі своїми збереженими знімками цін, а нові створюватимуться вже за актуальними тарифами.

### 2.3. Поліглотно-документна інтеграція (MongoDB Timelines)
Для швидкого рендерингу картки контрагента в ActivitiesDb реалізовано гібридний патерн:
- Основна стрічка активностей зберігається в колекції `activities`.
- Агрегований документ `client_timelines` атомарно кешує знімки останніх 5 дій клієнта за допомогою MongoDB-оператора `$push` з опцією `$slice`:
  ```javascript
  db.client_timelines.updateOne(
    { clientId: newClientId },
    {
      $push: {
        recentActivities: {
          $each: [ newActivitySnapshot ],
          $slice: -5
        }
      },
      $inc: { activityCount: 1 },
      $set: { updatedAt: new Date() }
    },
    { upsert: true }
  );
  ```
- Завдяки цьому фронтенд CRM завантажує картку клієнта за 1 запит без складних агрегацій.
