// ============================================================================
// Скрипт: queries.js (ActivitiesDb)
// Призначення: Приклади запитів find, оновлення вкладеного масиву ($push),
//              3 агрегаційні пайплайни та перевірка валідатора ($jsonSchema)
// Контекст: Activities Bounded Context
// СУБД: MongoDB 8.0 (mongosh)
// ============================================================================

const dbName = 'ActivitiesDb';
const targetDb = db.getSiblingDB(dbName);

print(`============================================================================`);
print(`           ВИКОНАННЯ ЗАПИТІВ ACTIVITIES-DB (MONGODB)                        `);
print(`============================================================================`);

// ============================================================================
// 1. FIND: Отримання історії активностей клієнта №1 (АгроТрейд)
// ============================================================================
print('\n[1. FIND] Хронологія активностей для клієнта №1:');
const clientActs = targetDb.activities.find(
  { clientId: NumberLong(1) },
  { type: 1, text: 1, "author.displayName": 1, durationSec: 1, createdAt: 1 }
).sort({ createdAt: -1 }).toArray();

printjson(clientActs);

// ============================================================================
// 2. UPDATE ($push): Додавання коментаря менеджера до існуючої активності
// ============================================================================
print('\n[2. UPDATE $push] Додавання вкладеного коментаря команди до дзвінка:');
const updateResult = targetDb.activities.updateOne(
  { _id: new ObjectId("653000000000000000000001") },
  {
    $push: {
      comments: {
        author: "Вікторія Коваль",
        text: "Клієнт надіслав реквізити компанії у Telegram.",
        createdAt: new Date()
      }
    }
  }
);
print(`  -> Оновлено документів ($push): ${updateResult.modifiedCount}`);

// ============================================================================
// 3. АГРЕГАЦІЯ 1: Кількість активностей за типами (call, email, meeting, note, task)
// ============================================================================
print('\n[3. AGGREGATION 1] Розподіл комунікацій за типами активностей:');
const actsByType = targetDb.activities.aggregate([
  {
    $group: {
      _id: "$type",
      totalCount: { $sum: 1 }
    }
  },
  { $sort: { totalCount: -1 } }
]).toArray();

printjson(actsByType);

// ============================================================================
// 4. АГРЕГАЦІЯ 2: Найактивніші клієнти за останні 30 днів
// ============================================================================
print('\n[4. AGGREGATION 2] Топ активних клієнтів за кількістю взаємодій:');
const thirtyDaysAgo = new Date();
thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);

const activeClients = targetDb.activities.aggregate([
  {
    $match: {
      createdAt: { $gte: thirtyDaysAgo }
    }
  },
  {
    $group: {
      _id: "$clientId",
      clientName: { $first: "$clientName" },
      activitiesCount: { $sum: 1 },
      lastInteraction: { $max: "$createdAt" }
    }
  },
  { $sort: { activitiesCount: -1 } }
]).toArray();

printjson(activeClients);

// ============================================================================
// 5. АГРЕГАЦІЯ 3: Середня тривалість дзвінків по співробітниках відділу продажів
// ============================================================================
print('\n[5. AGGREGATION 3] Середня тривалість телефонних розмов (секунди) за менеджерами:');
const callStats = targetDb.activities.aggregate([
  {
    $match: {
      type: "call",
      durationSec: { $exists: true, $ne: null }
    }
  },
  {
    $group: {
      _id: "$author.userId",
      managerName: { $first: "$author.displayName" },
      totalCalls: { $sum: 1 },
      avgDurationSec: { $avg: "$durationSec" },
      totalDurationSec: { $sum: "$durationSec" }
    }
  },
  {
    $project: {
      managerId: "$_id",
      managerName: 1,
      totalCalls: 1,
      avgDurationSec: { $round: ["$avgDurationSec", 0] },
      totalDurationSec: 1,
      _id: 0
    }
  },
  { $sort: { totalCalls: -1 } }
]).toArray();

printjson(callStats);

// ============================================================================
// 6. ТЕСТ ВАЛІДАЦІЇ ($jsonSchema Self-Check #5)
// Спроба вставити активність із забороненим типом "fax" ПОВИННА БУТИ ВІДХИЛЕНА!
// ============================================================================
print('\n[6. VALIDATION TEST] Спроба вставки активності з неприпустимим type: "fax"...');
try {
  targetDb.activities.insertOne({
    type: "fax", // Недопустимий тип, enum дозволяє лише call, email, meeting, note, task
    clientId: NumberLong(1),
    clientName: "АгроТрейд",
    author: { userId: NumberLong(1), displayName: "Тестер" },
    text: "Тестовий факс",
    createdAt: new Date()
  });
  print('  -> [ПОМИЛКА] База даних пропустила заборонений тип факсу!');
} catch (err) {
  print('  -> [ОЧІКУВАНА ВАЛІДАЦІЙНА ПОМИЛКА] Запит успішно відхилено MongoDB:');
  print(`     Повідомлення: ${err.message}`);
}

print('\n============================================================================');
print('           ЗАПИТИ ACTIVITIES-DB УСПІШНО ВИКОНАНО                            ');
print('============================================================================');
