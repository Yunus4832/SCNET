import { beforeEach, describe, expect, it } from 'vitest';
import { clearDrafts, deleteDraft, listDrafts, saveDraft, type ContentDraft } from './contentDraftStore';

const draft: ContentDraft = {
  schemaVersion: 1,
  draftId: 'draft-1',
  type: 'CharacterSkin',
  identifier: '0c644f44-b9cf-4099-97ca-99dd7be7108e',
  name: 'Test Skin',
  version: '1.0.0',
  description: 'draft test',
  sourceBlobId: 'blob-1',
  sourceFileName: 'skin.png',
  sourceMediaType: 'image/png',
  createdAt: '2026-01-01T00:00:00.000Z',
  updatedAt: '2026-01-01T00:00:00.000Z',
};

beforeEach(async () => {
  await new Promise<void>((resolve) => {
    const request = indexedDB.deleteDatabase('scnet-content-drafts');
    request.onsuccess = () => resolve();
    request.onerror = () => resolve();
    request.onblocked = () => resolve();
  });
});

describe('content draft store', () => {
  it('persists and deletes draft metadata transactionally', async () => {
    const source = new Blob([new Uint8Array([137, 80, 78, 71])], { type: 'image/png' });
    await saveDraft(draft, source, 'source-hash');
    expect(await listDrafts()).toEqual([draft]);
    await deleteDraft(draft.draftId);
    expect(await listDrafts()).toEqual([]);
  });

  it('clears all drafts and source records', async () => {
    const source = new Blob([new Uint8Array([137, 80, 78, 71])], { type: 'image/png' });
    await saveDraft(draft, source, 'source-hash');
    await saveDraft({ ...draft, draftId: 'draft-2', sourceBlobId: 'blob-2' }, source, 'source-hash');
    await clearDrafts();
    expect(await listDrafts()).toEqual([]);
  });
});
