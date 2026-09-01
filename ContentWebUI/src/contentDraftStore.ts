export type ImageContentType = 'BlocksTexture' | 'CharacterSkin';

export interface ContentDraft {
  schemaVersion: 1;
  draftId: string;
  type: ImageContentType;
  identifier: string;
  name: string;
  version: string;
  description: string;
  baselineHash?: string;
  sourceBlobId: string;
  sourceFileName: string;
  sourceMediaType: 'image/png';
  createdAt: string;
  updatedAt: string;
}

interface SourceRecord {
  blobId: string;
  blob: Blob;
  byteLength: number;
  sha256: string;
}

const databaseName = 'scnet-content-drafts';
const schemaVersion = 1;

function openDatabase(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(databaseName, schemaVersion);
    request.onupgradeneeded = () => {
      const database = request.result;
      const drafts = database.createObjectStore('drafts', { keyPath: 'draftId' });
      drafts.createIndex('updatedAt', 'updatedAt');
      drafts.createIndex('type', 'type');
      drafts.createIndex('identifier', 'identifier');
      database.createObjectStore('sources', { keyPath: 'blobId' });
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

function complete(transaction: IDBTransaction): Promise<void> {
  return new Promise((resolve, reject) => {
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
    transaction.onabort = () => reject(transaction.error ?? new Error('草稿事务已中止'));
  });
}

function result<T>(request: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

export async function listDrafts(): Promise<ContentDraft[]> {
  const database = await openDatabase();
  try {
    const transaction = database.transaction('drafts', 'readonly');
    const drafts = await result(transaction.objectStore('drafts').getAll()) as ContentDraft[];
    return drafts.sort((left, right) => right.updatedAt.localeCompare(left.updatedAt));
  } finally {
    database.close();
  }
}

export async function loadDraft(draftId: string): Promise<{ draft: ContentDraft; source: Blob }> {
  const database = await openDatabase();
  try {
    const transaction = database.transaction(['drafts', 'sources'], 'readonly');
    const draft = await result(transaction.objectStore('drafts').get(draftId)) as ContentDraft | undefined;
    if (!draft) throw new Error('草稿不存在');
    const source = await result(transaction.objectStore('sources').get(draft.sourceBlobId)) as SourceRecord | undefined;
    if (!source) throw new Error('草稿源文件不存在');
    return { draft, source: source.blob };
  } finally {
    database.close();
  }
}

export async function saveDraft(draft: ContentDraft, source: Blob, sha256: string): Promise<void> {
  const database = await openDatabase();
  try {
    const transaction = database.transaction(['drafts', 'sources'], 'readwrite');
    transaction.objectStore('sources').put({
      blobId: draft.sourceBlobId,
      blob: source,
      byteLength: source.size,
      sha256,
    } satisfies SourceRecord);
    transaction.objectStore('drafts').put(draft);
    await complete(transaction);
  } finally {
    database.close();
  }
}

export async function deleteDraft(draftId: string): Promise<void> {
  const loaded = await loadDraft(draftId);
  const database = await openDatabase();
  try {
    const transaction = database.transaction(['drafts', 'sources'], 'readwrite');
    transaction.objectStore('drafts').delete(draftId);
    transaction.objectStore('sources').delete(loaded.draft.sourceBlobId);
    await complete(transaction);
  } finally {
    database.close();
  }
}

export async function clearDrafts(): Promise<void> {
  const database = await openDatabase();
  try {
    const transaction = database.transaction(['drafts', 'sources'], 'readwrite');
    transaction.objectStore('drafts').clear();
    transaction.objectStore('sources').clear();
    await complete(transaction);
  } finally {
    database.close();
  }
}
