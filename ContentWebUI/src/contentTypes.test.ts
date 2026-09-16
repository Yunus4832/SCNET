import { describe, expect, it } from 'vitest';
import { contentTypeLabel } from './contentTypes';

describe('contentTypeLabel', () => {
  it('uses Chinese labels for known content types', () => {
    expect(contentTypeLabel('Mod')).toBe('模组');
    expect(contentTypeLabel('World')).toBe('世界');
    expect(contentTypeLabel('BlocksTexture')).toBe('材质');
    expect(contentTypeLabel('CharacterSkin')).toBe('皮肤');
    expect(contentTypeLabel('FurniturePack')).toBe('家具包');
  });

  it('keeps unknown types readable', () => {
    expect(contentTypeLabel('FutureType')).toBe('FutureType');
  });
});
