import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  clearAccess,
  getAccess,
  getSavedAccesses,
  getSavedRoles,
  markAccessInvalid,
  setAccess,
  setActiveRole,
  updateAccessLabel,
} from './api';

describe('saved access credentials', () => {
  beforeEach(() => {
    const values = new Map<string, string>();
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => values.get(key) ?? null,
      setItem: (key: string, value: string) => values.set(key, value),
      removeItem: (key: string) => values.delete(key),
    });
  });
  afterEach(() => vi.unstubAllGlobals());

  it('persists multiple keys and selects the requested identity', () => {
    setAccess({ role: 'publisher', apiKey: 'publisher-one', label: 'First' });
    setAccess({ role: 'publisher', apiKey: 'publisher-two' });
    setAccess({ role: 'administrator', apiKey: 'admin-one' });

    expect(getSavedRoles()).toEqual(['publisher', 'administrator']);
    expect(getAccess()?.apiKey).toBe('admin-one');
    setActiveRole('publisher', 'publisher-one');
    expect(getAccess()).toMatchObject({
      role: 'publisher',
      apiKey: 'publisher-one',
      label: 'First',
    });
    expect(getSavedAccesses('publisher')).toHaveLength(2);
  });

  it('updates an existing identity without creating duplicate credentials', () => {
    setAccess({ role: 'publisher', apiKey: 'publisher-one' });
    setAccess({ role: 'publisher', apiKey: 'publisher-one', label: 'Updated' });
    updateAccessLabel('publisher', 'publisher-one', 'Renamed');

    expect(getSavedAccesses('publisher')).toHaveLength(1);
    expect(getAccess()?.label).toBe('Renamed');
  });

  it('deletes one key while retaining the other role and remaining keys', () => {
    setAccess({ role: 'administrator', apiKey: 'admin-one' });
    setAccess({ role: 'publisher', apiKey: 'publisher-one' });
    setAccess({ role: 'publisher', apiKey: 'publisher-two' });
    clearAccess('publisher', 'publisher-two');

    expect(getAccess()).toBeNull();
    expect(getAccess('publisher')?.apiKey).toBe('publisher-one');
    expect(getAccess('administrator')?.apiKey).toBe('admin-one');
    clearAccess('publisher');
    expect(getSavedRoles()).toEqual(['administrator']);
    clearAccess();
    expect(getSavedRoles()).toEqual([]);
  });

  it('marks an invalid key and prevents it from being selected again', () => {
    setAccess({ role: 'publisher', apiKey: 'publisher-one' });
    setAccess({ role: 'publisher', apiKey: 'publisher-two' });

    expect(markAccessInvalid('publisher', 'publisher-two')).toBe(true);
    expect(getAccess()).toBeNull();
    expect(getAccess('publisher')?.apiKey).toBe('publisher-one');
    setActiveRole('publisher', 'publisher-two');
    expect(getAccess()).toBeNull();
    setAccess({ role: 'publisher', apiKey: 'publisher-two' });
    expect(getAccess()?.invalid).toBe(false);
  });

  it('allows signing in again after corrupted local storage', () => {
    localStorage.setItem('scnet.content.credentials', '{invalid');
    expect(getSavedRoles()).toEqual([]);
    expect(getAccess()).toBeNull();
    setAccess({ role: 'publisher', apiKey: 'publisher-one' });
    expect(getAccess()?.apiKey).toBe('publisher-one');
  });

  it('ignores data that does not match the credential array structure', () => {
    localStorage.setItem('scnet.content.credentials', JSON.stringify({ publisher: 'invalid' }));
    expect(getSavedAccesses('publisher')).toEqual([]);
    expect(getAccess('publisher')).toBeNull();
  });
});
