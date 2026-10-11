// ============================================================================
// Скрипт: init.js (ActivitiesDb)
// Призначення: Ініціалізація колекцій, суворої $jsonSchema валідації та індексів
// Контекст: Activities Bounded Context (Комунікації та активності CRM)
// СУБД: MongoDB 8.0 (mongosh)
// ============================================================================

const dbName = 'ActivitiesDb';
const targetDb = db.getSiblingDB(dbName);

print(`[INIT] Налаштування бази даних: ${dbName}`);

// ============================================================================
// 1. КОЛЕКЦІЯ activities (ПОЛІМОРФНА СХЕМА АКТИВНОСТЕЙ)
// ============================================================================
const activitiesValidator = {
  $jsonSchema: {
    bsonType: "object",
    required: ["type", "clientId", "clientName", "author", "createdAt"],
    properties: {
      _id: { bsonType: "objectId" },
      type: {
        enum: ["call", "email", "meeting", "note", "task"],
        description: "Тип активності: call, email, meeting, note або task (тип 'fax' заборонено!)"
      },
      clientId: {
        // без FK, ідентифікатор з SalesDb (Clients.Id)
        bsonType: ["long", "int", "double"],
        description: "Ідентифікатор клієнта (Clients.Id у SalesDb)"
      },
      clientName: {
        // копія для читання, оновлюється подіями ClientUpdatedEvent від SalesDb
        bsonType: "string",
        description: "Денормалізована назва компанії клієнта"
      },
      dealId: {
        // без FK, ідентифікатор з SalesDb (Deals.Id)
        bsonType: ["long", "int", "double", "null"],
        description: "Ідентифікатор угоди (опціонально)"
      },
      dealTitle: {
        // знімок (snapshot) на момент прив'язки, не оновлюється, джерело: SalesDb
        bsonType: ["string", "null"],
        description: "Денормалізована назва угоди (опціонально)"
      },
      author: {
        bsonType: "object",
        required: ["userId", "displayName"],
        properties: {
          userId: { 
            // без FK, ідентифікатор з SalesDb (TeamMembers.Id)
            bsonType: ["long", "int", "double"] 
          },
          displayName: { 
            // знімок на момент створення, не оновлюється, джерело: SalesDb
            bsonType: "string" 
          }
        }
      },
      text: {
        bsonType: "string",
        description: "Текст нотатки, підсумок розмови або деталі завдання"
      },
      createdAt: { bsonType: "date" },

      // Поля для type = "call"
      direction: {
        enum: ["inbound", "outbound"]
      },
      durationSec: {
        bsonType: ["int", "long", "double"]
      },
      outcome: {
        bsonType: "string"
      },

      // Поля для type = "email"
      subject: {
        bsonType: "string"
      },
      attachments: {
        bsonType: "array",
        items: { bsonType: "string" }
      },

      // Поля для type = "meeting"
      location: {
        bsonType: "string"
      },
      participants: {
        bsonType: "array",
        items: { bsonType: "string" }
      },
      agenda: {
        bsonType: "string"
      },

      // Поля для type = "task"
      dueDate: {
        bsonType: "date"
      },
      done: {
        bsonType: "bool"
      },

      // Вбудовані коментарі команди (Embedding)
      comments: {
        bsonType: "array",
        items: {
          bsonType: "object",
          required: ["author", "text", "createdAt"],
          properties: {
            author: { bsonType: "string" },
            text: { bsonType: "string" },
            createdAt: { bsonType: "date" }
          }
        }
      },

      // Пов'язані послуги з прайс-листа (Денормалізація)
      relatedProducts: {
        bsonType: "array",
        items: {
          bsonType: "object",
          required: ["productId", "name"],
          properties: {
            productId: { 
              // без FK, ідентифікатор з CatalogDb (Products.Id)
              bsonType: ["long", "int", "double"] 
            },
            name: { 
              // копія для читання, оновлюється подіями з CatalogDb
              bsonType: "string" 
            }
          }
        }
      },

      // Посилання на зовнішнє обговорення (Referencing)
      discussionId: {
        bsonType: ["objectId", "null"]
      }
    }
  }
};

const existingCollections = targetDb.getCollectionNames();
if (!existingCollections.includes('activities')) {
  targetDb.createCollection('activities', {
    validator: activitiesValidator,
    validationLevel: "strict",
    validationAction: "error"
  });
  print('  -> Створено колекцію activities з валідацією $jsonSchema');
} else {
  targetDb.runCommand({
    collMod: 'activities',
    validator: activitiesValidator,
    validationLevel: "strict",
    validationAction: "error"
  });
  print('  -> Оновлено валідацію $jsonSchema для activities');
}

// Індекси колекції activities:
// 1. запит: вибірка повної стрічки комунікацій конкретного клієнта з сортуванням від найновіших
targetDb.activities.createIndex({ clientId: 1, createdAt: -1 }, { name: "idx_activities_client_date" });

// 2. запит: вибірка всіх активностей, пов'язаних з конкретною угодою воронки продажів
targetDb.activities.createIndex({ dealId: 1 }, { name: "idx_activities_dealId" });

// 3. запит: KPI та активність конкретного менеджера (дзвінки, зустрічі, завдання за період)
targetDb.activities.createIndex({ "author.userId": 1 }, { name: "idx_activities_author_userId" });

// 4. запит: фільтрація стрічки за типом комунікації (лише дзвінки, лише зустрічі тощо)
targetDb.activities.createIndex({ type: 1 }, { name: "idx_activities_type" });

// 5. запит: повнотекстовий пошук по нотатках, темах листів та назвах клієнтів
targetDb.activities.createIndex({ text: "text", clientName: "text" }, { name: "idx_activities_text_search" });
print('  -> Індекси для activities налаштовано');

// ============================================================================
// 2. КОЛЕКЦІЯ discussions (REFERENCING ДЛЯ ТЕМАТИЧНИХ ОБГОВОРЕНЬ)
// ============================================================================
if (!existingCollections.includes('discussions')) {
  targetDb.createCollection('discussions', {
    validator: {
      $jsonSchema: {
        bsonType: "object",
        required: ["title", "clientId", "status", "createdAt"],
        properties: {
          title: { bsonType: "string" },
          clientId: { 
            // без FK, ідентифікатор з SalesDb (Clients.Id)
            bsonType: ["long", "int", "double"] 
          },
          dealId: { 
            // без FK, ідентифікатор з SalesDb (Deals.Id)
            bsonType: ["long", "int", "double", "null"] 
          },
          status: { enum: ["open", "resolved", "archived"] },
          messageCount: { bsonType: ["int", "long", "double"] },
          lastMessageAt: { bsonType: "date" },
          createdAt: { bsonType: "date" }
        }
      }
    }
  });
}
// Індекси discussions:
// запит: список обговорень клієнта від найновіших
targetDb.discussions.createIndex({ clientId: 1, createdAt: -1 }, { name: "idx_discussions_clientId" });
// запит: список обговорень конкретної угоди
targetDb.discussions.createIndex({ dealId: 1 }, { name: "idx_discussions_dealId" });
print('  -> Колекція discussions налаштована');

// ============================================================================
// 3. КОЛЕКЦІЯ discussion_messages (НЕОБМЕЖЕНА ПІДКОЛЕКЦІЯ ПОВІДОМЛЕНЬ)
// ============================================================================
if (!existingCollections.includes('discussion_messages')) {
  targetDb.createCollection('discussion_messages', {
    validator: {
      $jsonSchema: {
        bsonType: "object",
        required: ["discussionId", "author", "text", "createdAt"],
        properties: {
          discussionId: { bsonType: "objectId" },
          author: {
            bsonType: "object",
            required: ["userId", "displayName"],
            properties: {
              userId: { 
                // без FK, ідентифікатор з SalesDb (TeamMembers.Id)
                bsonType: ["long", "int", "double"] 
              },
              displayName: { 
                // знімок на момент написання, не оновлюється, джерело: SalesDb
                bsonType: "string" 
              }
            }
          },
          text: { bsonType: "string" },
          attachments: {
            bsonType: "array",
            items: { bsonType: "string" }
          },
          createdAt: { bsonType: "date" }
        }
      }
    }
  });
}
// запит: хронологічна вибірка повідомлень конкретної гілки з підтримкою пагінації
targetDb.discussion_messages.createIndex({ discussionId: 1, createdAt: 1 }, { name: "idx_messages_discussion_date" });
print('  -> Колекція discussion_messages налаштована');

// ============================================================================
// 4. КОЛЕКЦІЯ client_timelines (ГІБРИДНИЙ ПІДХІД ДЛЯ ШВИДКОГО ТАЙМЛАЙНУ)
// ============================================================================
if (!existingCollections.includes('client_timelines')) {
  targetDb.createCollection('client_timelines', {
    validator: {
      $jsonSchema: {
        bsonType: "object",
        required: ["clientId", "recentActivities", "activityCount"],
        properties: {
          clientId: { 
            // без FK, ідентифікатор з SalesDb (Clients.Id)
            bsonType: ["long", "int", "double"] 
          },
          recentActivities: {
            // знімок (snapshot) останніх 5-10 активностей клієнта для швидкого читання картки без міжсервісних запитів
            bsonType: "array",
            items: {
              bsonType: "object",
              required: ["activityId", "type", "createdAt", "summary"],
              properties: {
                activityId: { 
                  // посилання на первинний документ в колекції activities
                  bsonType: "objectId" 
                },
                type: { bsonType: "string" },
                createdAt: { bsonType: "date" },
                summary: { bsonType: "string" }
              }
            }
          },
          activityCount: { bsonType: ["int", "long", "double"] },
          updatedAt: { bsonType: "date" }
        }
      }
    }
  });
}
// запит: O(1) доступ до таймлайну клієнта за clientId та запобігання дублюванню агрегату
targetDb.client_timelines.createIndex({ clientId: 1 }, { unique: true, name: "idx_client_timelines_clientId_unique" });
print('  -> Колекція client_timelines налаштована');

print('[INIT] Ініціалізацію ActivitiesDb завершено успішно!');
