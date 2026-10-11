// ============================================================================
// Скрипт: seed.js (ActivitiesDb)
// Призначення: Ідемпотентне наповнення активностей (12+ документів, 2 обговорення,
//              15+ повідомлень, таймлайни) для CRM студії веб-розробки
// Контекст: Activities Bounded Context
// СУБД: MongoDB 8.0 (mongosh)
// ============================================================================

const dbName = 'ActivitiesDb';
const targetDb = db.getSiblingDB(dbName);

print(`[SEED] Початок ідемпотентного заповнення ActivitiesDb...`);

// 1. Ідентифікатори обговорень
const disc1Id = new ObjectId("651000000000000000000001");
const disc2Id = new ObjectId("651000000000000000000002");

// 1.1. Наповнення discussions
targetDb.discussions.updateOne(
  { _id: disc1Id },
  {
    $set: {
      title: "Узгодження архітектури та хостингу для порталу АгроТрейд",
      clientId: NumberLong(1),
      dealId: NumberLong(1),
      status: "open",
      messageCount: NumberInt(8),
      lastMessageAt: new Date("2026-10-04T16:00:00Z"),
      createdAt: new Date("2026-10-01T10:00:00Z")
    }
  },
  { upsert: true }
);

targetDb.discussions.updateOne(
  { _id: disc2Id },
  {
    $set: {
      title: "Коригування фірмового стилю для Сидоренко та партнери",
      clientId: NumberLong(2),
      dealId: NumberLong(2),
      status: "open",
      messageCount: NumberInt(7),
      lastMessageAt: new Date("2026-10-07T14:30:00Z"),
      createdAt: new Date("2026-10-05T11:00:00Z")
    }
  },
  { upsert: true }
);

// 1.2. Наповнення discussion_messages (15+ повідомлень)
const messagesData = [
  // Повідомлення для обговорення 1 (АгроТрейд)
  { _id: new ObjectId("652000000000000000000001"), discussionId: disc1Id, author: { userId: NumberLong(2), displayName: "Вікторія Коваль" }, text: "Колеги, клієнт просить розгорнути сайт у європейському дата-центрі через вимоги безпеки.", attachments: ["hosting_specs.pdf"], createdAt: new Date("2026-10-01T10:15:00Z") },
  { _id: new ObjectId("652000000000000000000002"), discussionId: disc1Id, author: { userId: NumberLong(1), displayName: "Андрій Мельник" }, text: "Так, включимо в комерційну пропозицію наш пакет хостингу (позиція 9 у каталозі).", attachments: [], createdAt: new Date("2026-10-01T10:30:00Z") },
  { _id: new ObjectId("652000000000000000000003"), discussionId: disc1Id, author: { userId: NumberLong(2), displayName: "Вікторія Коваль" }, text: "Додала до КП хостинг на 1 рік за 4500 грн.", attachments: [], createdAt: new Date("2026-10-01T11:00:00Z") },
  { _id: new ObjectId("652000000000000000000004"), discussionId: disc1Id, author: { userId: NumberLong(4), displayName: "Марина Савченко" }, text: "Чи потрібна синхронізація з 1С для експорту прайсу зерна?", attachments: [], createdAt: new Date("2026-10-02T09:00:00Z") },
  { _id: new ObjectId("652000000000000000000005"), discussionId: disc1Id, author: { userId: NumberLong(2), displayName: "Вікторія Коваль" }, text: "На першому етапі ні, буде лише ручний імпорт Excel файлу.", attachments: [], createdAt: new Date("2026-10-02T10:20:00Z") },
  { _id: new ObjectId("652000000000000000000006"), discussionId: disc1Id, author: { userId: NumberLong(1), displayName: "Андрій Мельник" }, text: "Погоджено. Розробку починаємо з понеділка.", attachments: [], createdAt: new Date("2026-10-03T14:00:00Z") },
  { _id: new ObjectId("652000000000000000000007"), discussionId: disc1Id, author: { userId: NumberLong(2), displayName: "Вікторія Коваль" }, text: "Відправила фінальний договір Петру Іванчуку.", attachments: ["contract_signed.pdf"], createdAt: new Date("2026-10-04T12:00:00Z") },
  { _id: new ObjectId("652000000000000000000008"), discussionId: disc1Id, author: { userId: NumberLong(2), displayName: "Вікторія Коваль" }, text: "Отримано підписаний договір та передоплату 50%!", attachments: [], createdAt: new Date("2026-10-04T16:00:00Z") },

  // Повідомлення для обговорення 2 (Сидоренко та партнери)
  { _id: new ObjectId("652000000000000000000009"), discussionId: disc2Id, author: { userId: NumberLong(2), displayName: "Вікторія Коваль" }, text: "Клієнтка хоче строгий класичний стиль для логотипу: темно-синій та золотий.", attachments: ["palette_ideas.png"], createdAt: new Date("2026-10-05T11:15:00Z") },
  { _id: new ObjectId("652000000000000000000010"), discussionId: disc2Id, author: { userId: NumberLong(1), displayName: "Андрій Мельник" }, text: "Шрифти мають бути із засічками, щоб підкреслити надійність юридичної практики.", attachments: [], createdAt: new Date("2026-10-05T12:30:00Z") },
  { _id: new ObjectId("652000000000000000000011"), discussionId: disc2Id, author: { userId: NumberLong(4), displayName: "Марина Савченко" }, text: "Підготувала 3 перші концепції логотипу для презентації.", attachments: ["concepts_v1.pdf"], createdAt: new Date("2026-10-06T15:00:00Z") },
  { _id: new ObjectId("652000000000000000000012"), discussionId: disc2Id, author: { userId: NumberLong(2), displayName: "Вікторія Коваль" }, text: "Показала клієнтці, вона обрала концепт №2 з невеликими правками товщини ліній.", attachments: [], createdAt: new Date("2026-10-06T17:45:00Z") },
  { _id: new ObjectId("652000000000000000000013"), discussionId: disc2Id, author: { userId: NumberLong(4), displayName: "Марина Савченко" }, text: "Внесла правки. Готую фінальний гайдлайн та експорт у векторі SVG/EPS.", attachments: [], createdAt: new Date("2026-10-07T11:00:00Z") },
  { _id: new ObjectId("652000000000000000000014"), discussionId: disc2Id, author: { userId: NumberLong(2), displayName: "Вікторія Коваль" }, text: "Узгоджено знижку 5% на лендінг при замовленні разом з логотипом.", attachments: [], createdAt: new Date("2026-10-07T13:00:00Z") },
  { _id: new ObjectId("652000000000000000000015"), discussionId: disc2Id, author: { userId: NumberLong(2), displayName: "Вікторія Коваль" }, text: "Очікуємо підтвердження оплати кошторису до кінця тижня.", attachments: [], createdAt: new Date("2026-10-07T14:30:00Z") }
];

messagesData.forEach(msg => {
  targetDb.discussion_messages.updateOne(
    { _id: msg._id },
    { $set: msg },
    { upsert: true }
  );
});

// 2. Наповнення activities (13 різноманітних документів різних типів)
const activitiesData = [
  // 1. Дзвінок вхідний (Call - inbound)
  {
    _id: new ObjectId("653000000000000000000001"),
    type: "call",
    clientId: NumberLong(1),
    clientName: "ТОВ «АгроТрейд Захід»",
    dealId: NumberLong(1),
    dealTitle: "Корпоративний портал для АгроТрейд Захід",
    author: { userId: NumberLong(2), displayName: "Вікторія Коваль" },
    text: "Первинний дзвінок: обговорили редизайн старого сайту, інтеграцію з каталогом зернових культур.",
    direction: "inbound",
    durationSec: NumberInt(420),
    outcome: "Клієнт зацікавлений у розробці сайту на Vue.js та надійному хостингу",
    relatedProducts: [{ productId: NumberLong(2), name: "Корпоративний сайт" }],
    discussionId: disc1Id,
    comments: [
      { author: "Андрій Мельник", text: "Чудовий контакт, підготуй кошторис на понеділок.", createdAt: new Date("2026-10-01T10:00:00Z") }
    ],
    createdAt: new Date("2026-10-01T09:30:00Z")
  },
  // 2. Лист (Email)
  {
    _id: new ObjectId("653000000000000000000002"),
    type: "email",
    clientId: NumberLong(1),
    clientName: "ТОВ «АгроТрейд Захід»",
    dealId: NumberLong(1),
    dealTitle: "Корпоративний портал для АгроТрейд Захід",
    author: { userId: NumberLong(2), displayName: "Вікторія Коваль" },
    text: "Відправлено комерційну пропозицію з детальним таймлайном розробки на 6 тижнів.",
    subject: "Комерційна пропозиція: Корпоративний сайт АгроТрейд Захід",
    attachments: ["КП_АгроТрейд_2026.pdf"],
    relatedProducts: [
      { productId: NumberLong(2), name: "Корпоративний сайт" },
      { productId: NumberLong(9), name: "Пакет хостингу та SSL-сертифікат на 1 рік" }
    ],
    discussionId: disc1Id,
    comments: [],
    createdAt: new Date("2026-10-02T14:00:00Z")
  },
  // 3. Зустріч (Meeting)
  {
    _id: new ObjectId("653000000000000000000003"),
    type: "meeting",
    clientId: NumberLong(1),
    clientName: "ТОВ «АгроТрейд Захід»",
    dealId: NumberLong(1),
    dealTitle: "Корпоративний портал для АгроТрейд Захід",
    author: { userId: NumberLong(1), displayName: "Андрій Мельник" },
    text: "Онлайн-зустріч у Google Meet з директором АгроТрейд: погоджено дизайн-концепт та етапи оплати.",
    location: "Google Meet",
    participants: ["Петро Іванчук", "Андрій Мельник", "Вікторія Коваль"],
    agenda: "Презентація прототипу сайту, узгодження структури сторінок та договору",
    relatedProducts: [{ productId: NumberLong(2), name: "Корпоративний сайт" }],
    discussionId: disc1Id,
    comments: [],
    createdAt: new Date("2026-10-04T11:00:00Z")
  },
  // 4. Нотатка (Note)
  {
    _id: new ObjectId("653000000000000000000004"),
    type: "note",
    clientId: NumberLong(1),
    clientName: "ТОВ «АгроТрейд Захід»",
    dealId: NumberLong(1),
    dealTitle: "Корпоративний портал для АгроТрейд Захід",
    author: { userId: NumberLong(2), displayName: "Вікторія Коваль" },
    text: "Клієнт надав фірмові шрифти та фотографії елеваторів у високій якості на Google Drive.",
    relatedProducts: [],
    comments: [],
    createdAt: new Date("2026-10-05T09:15:00Z")
  },
  // 5. Завдання (Task)
  {
    _id: new ObjectId("653000000000000000000005"),
    type: "task",
    clientId: NumberLong(1),
    clientName: "ТОВ «АгроТрейд Захід»",
    dealId: NumberLong(1),
    dealTitle: "Корпоративний портал для АгроТрейд Захід",
    author: { userId: NumberLong(4), displayName: "Марина Савченко" },
    text: "Підготувати серверну конфігурацію Nginx та встановити SSL сертифікат на тестовий домен.",
    dueDate: new Date("2026-10-15T18:00:00Z"),
    done: false,
    relatedProducts: [{ productId: NumberLong(9), name: "Пакет хостингу та SSL-сертифікат на 1 рік" }],
    comments: [],
    createdAt: new Date("2026-10-05T17:00:00Z")
  },
  // 6. Дзвінок вихідний (Call - outbound)
  {
    _id: new ObjectId("653000000000000000000006"),
    type: "call",
    clientId: NumberLong(2),
    clientName: "Юридичне бюро «Сидоренко та партнери»",
    dealId: NumberLong(2),
    dealTitle: "Створення лендінгу та айдентики для адвокатів",
    author: { userId: NumberLong(2), displayName: "Вікторія Коваль" },
    text: "Консультація щодо структури лендінгу та позиціонування адвокатських послуг.",
    direction: "outbound",
    durationSec: NumberInt(680),
    outcome: "Клієнтка погодилася на пакетне замовлення: лендінг + розробка фірмового стилю",
    relatedProducts: [
      { productId: NumberLong(1), name: "Лендінг під ключ" },
      { productId: NumberLong(7), name: "Розробка логотипу та фірмового стилю" }
    ],
    discussionId: disc2Id,
    comments: [],
    createdAt: new Date("2026-10-05T12:00:00Z")
  },
  // 7. Зустріч в офісі (Meeting)
  {
    _id: new ObjectId("653000000000000000000007"),
    type: "meeting",
    clientId: NumberLong(2),
    clientName: "Юридичне бюро «Сидоренко та партнери»",
    dealId: NumberLong(2),
    dealTitle: "Створення лендінгу та айдентики для адвокатів",
    author: { userId: NumberLong(1), displayName: "Андрій Мельник" },
    text: "Зустріч в офісі клієнта у Києві: перегляд референсів та затвердження палітри кольорів.",
    location: "Офіс клієнта, вул. Володимирська 42",
    participants: ["Марія Сидоренко", "Андрій Мельник"],
    agenda: "Обговорення логотипу та тексту посадкової сторінки",
    relatedProducts: [{ productId: NumberLong(7), name: "Розробка логотипу та фірмового стилю" }],
    discussionId: disc2Id,
    comments: [
      { author: "Вікторія Коваль", text: "Протокол зустрічі зафіксовано в CRM.", createdAt: new Date("2026-10-06T18:00:00Z") }
    ],
    createdAt: new Date("2026-10-06T16:30:00Z")
  },
  // 8. Лист з макетами (Email)
  {
    _id: new ObjectId("653000000000000000000008"),
    type: "email",
    clientId: NumberLong(2),
    clientName: "Юридичне бюро «Сидоренко та партнери»",
    dealId: NumberLong(2),
    dealTitle: "Створення лендінгу та айдентики для адвокатів",
    author: { userId: NumberLong(2), displayName: "Вікторія Коваль" },
    text: "Надіслано 3 варіанти логотипу та презентацію шрифтів.",
    subject: "Варіанти логотипу: Юридичне бюро Сидоренко",
    attachments: ["logo_options_v1.pdf"],
    relatedProducts: [{ productId: NumberLong(7), name: "Розробка логотипу та фірмового стилю" }],
    discussionId: disc2Id,
    comments: [],
    createdAt: new Date("2026-10-07T11:00:00Z")
  },
  // 9. Завдання виконане (Task - done)
  {
    _id: new ObjectId("653000000000000000000009"),
    type: "task",
    clientId: NumberLong(2),
    clientName: "Юридичне бюро «Сидоренко та партнери»",
    dealId: NumberLong(2),
    dealTitle: "Створення лендінгу та айдентики для адвокатів",
    author: { userId: NumberLong(2), displayName: "Вікторія Коваль" },
    text: "Передзвонити клієнтці та отримати фідбек щодо обраного варіанту логотипу.",
    dueDate: new Date("2026-10-08T12:00:00Z"),
    done: true,
    relatedProducts: [],
    comments: [],
    createdAt: new Date("2026-10-07T15:00:00Z")
  },
  // 10. Дзвінок холодний (Call - outreach)
  {
    _id: new ObjectId("653000000000000000000010"),
    type: "call",
    clientId: NumberLong(3),
    clientName: "ТОВ «Смарт Логістика»",
    dealId: NumberLong(3),
    dealTitle: "SEO аудит логістичного сайту Smart Logistics",
    author: { userId: NumberLong(3), displayName: "Денис Бондаренко" },
    text: "Холодний дзвінок директору з логістики: запропонував безкоштовний експрес-аудит сайту.",
    direction: "outbound",
    durationSec: NumberInt(310),
    outcome: "Клієнт зацікавився, попросив надіслати приклади покращення позицій конкурентів",
    relatedProducts: [{ productId: NumberLong(4), name: "Комплексний SEO-аудит сайту" }],
    comments: [],
    createdAt: new Date("2026-10-10T11:20:00Z")
  },
  // 11. Лист з експрес-аудитом (Email)
  {
    _id: new ObjectId("653000000000000000000011"),
    type: "email",
    clientId: NumberLong(3),
    clientName: "ТОВ «Смарт Логістика»",
    dealId: NumberLong(3),
    dealTitle: "SEO аудит логістичного сайту Smart Logistics",
    author: { userId: NumberLong(3), displayName: "Денис Бондаренко" },
    text: "Відправлено 5 ключових помилок індексації сайту SmartLogistics та опис послуги SEO аудиту.",
    subject: "Експрес-аналіз швидкості та SEO для Smart Logistics",
    attachments: ["express_audit_report.pdf"],
    relatedProducts: [
      { productId: NumberLong(4), name: "Комплексний SEO-аудит сайту" },
      { productId: NumberLong(5), name: "Щомісячне SEO-просування" }
    ],
    comments: [],
    createdAt: new Date("2026-10-10T14:40:00Z")
  },
  // 12. Нотатка про відмову (Note - Lost deal)
  {
    _id: new ObjectId("653000000000000000000012"),
    type: "note",
    clientId: NumberLong(4),
    clientName: "ПП «Еко Продукти»",
    dealId: NumberLong(4),
    dealTitle: "Редизайн інтернет-магазину Еко Продукти",
    author: { userId: NumberLong(3), displayName: "Денис Бондаренко" },
    text: "Клієнт повідомив, що компанія заморожує бюджет до весни через переїзд складу. Нагадати про себе у лютому.",
    relatedProducts: [{ productId: NumberLong(2), name: "Корпоративний сайт" }],
    comments: [
      { author: "Андрій Мельник", text: "Зрозумів, створи завдання на повторний контакт у лютому 2027.", createdAt: new Date("2026-10-08T17:00:00Z") }
    ],
    createdAt: new Date("2026-10-08T16:30:00Z")
  },
  // 13. Завдання нагадування (Task)
  {
    _id: new ObjectId("653000000000000000000013"),
    type: "task",
    clientId: NumberLong(4),
    clientName: "ПП «Еко Продукти»",
    dealId: null,
    dealTitle: null,
    author: { userId: NumberLong(3), displayName: "Денис Бондаренко" },
    text: "Повторний контакт з Еко Продукти щодо редизайну магазину на весняний сезон.",
    dueDate: new Date("2027-02-15T09:00:00Z"),
    done: false,
    relatedProducts: [],
    comments: [],
    createdAt: new Date("2026-10-09T10:00:00Z")
  }
];

activitiesData.forEach(act => {
  targetDb.activities.updateOne(
    { _id: act._id },
    { $set: act },
    { upsert: true }
  );
});

// 3. Наповнення client_timelines (Гібридний підхід: останні 5 знімків активностей клієнта)
const clientsList = [NumberLong(1), NumberLong(2), NumberLong(3), NumberLong(4)];

clientsList.forEach(cId => {
  const clientActs = targetDb.activities
    .find({ clientId: cId })
    .sort({ createdAt: -1 })
    .limit(5)
    .toArray();

  const recentSnapshots = clientActs.map(a => ({
    activityId: a._id,
    type: a.type,
    createdAt: a.createdAt,
    summary: a.text ? (a.text.length > 80 ? a.text.substring(0, 77) + "..." : a.text) : a.type
  }));

  const totalCount = targetDb.activities.countDocuments({ clientId: cId });

  targetDb.client_timelines.updateOne(
    { clientId: cId },
    {
      $set: {
        recentActivities: recentSnapshots,
        activityCount: NumberInt(totalCount),
        updatedAt: new Date()
      }
    },
    { upsert: true }
  );
});

print(`[SEED] Успішно наповнено ActivitiesDb:`);
print(`  -> Активностей: ${targetDb.activities.countDocuments()}`);
print(`  -> Обговорень: ${targetDb.discussions.countDocuments()}`);
print(`  -> Повідомлень в обговореннях: ${targetDb.discussion_messages.countDocuments()}`);
print(`  -> Таймлайнів клієнтів: ${targetDb.client_timelines.countDocuments()}`);
