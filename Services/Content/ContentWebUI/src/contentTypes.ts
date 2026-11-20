const labels: Record<string, string> = {
  Mod: '模组',
  World: '世界',
  BlocksTexture: '材质',
  CharacterSkin: '皮肤',
  FurniturePack: '家具包',
};

export function contentTypeLabel(type: string) {
  return labels[type] ?? type;
}
