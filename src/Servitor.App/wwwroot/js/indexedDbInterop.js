const DB_NAME = 'servitor-rules-data';
const STORE_NAME = 'documents';
const LEGACY_STORE_NAME = 'factions';
const DB_VERSION = 2;

let databasePromise = null;

// One shared connection instead of one per call; it is released when another tab upgrades or deletes the database.
function openDatabase() {
    databasePromise ??= new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);
        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(STORE_NAME)) {
                db.createObjectStore(STORE_NAME);
            }
            if (db.objectStoreNames.contains(LEGACY_STORE_NAME)) {
                db.deleteObjectStore(LEGACY_STORE_NAME);
            }
        };
        request.onsuccess = () => {
            const db = request.result;
            db.onversionchange = () => {
                db.close();
                databasePromise = null;
            };
            resolve(db);
        };
        request.onerror = () => {
            databasePromise = null;
            reject(request.error);
        };
    });
    return databasePromise;
}

export async function putDocument(key, json) {
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(STORE_NAME, 'readwrite');
        transaction.objectStore(STORE_NAME).put(json, key);
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
        // A commit-time failure such as QuotaExceededError only raises 'abort', never a request error.
        transaction.onabort = () => reject(transaction.error);
    });
}

export async function getDocument(key) {
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(STORE_NAME, 'readonly');
        const request = transaction.objectStore(STORE_NAME).get(key);
        request.onsuccess = () => resolve(request.result ?? null);
        request.onerror = () => reject(request.error);
    });
}
