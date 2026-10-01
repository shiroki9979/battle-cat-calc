import type { CatRarity } from '@/types/api';

export const RARITY_ORDER: CatRarity[] = [
  'Rare',
  'SuperRare',
  'SuperRareFest',
  'UberRare',
  'UberRareFest',
  'Exclusive',
  'Legend',
];

export const RARITY_LABEL: Record<CatRarity, string> = {
  Rare: 'Rare',
  SuperRare: 'Super Rare',
  SuperRareFest: 'Super Rare Fest',
  UberRare: 'Uber Rare',
  UberRareFest: 'Uber Fest',
  Exclusive: 'Exclusive',
  Legend: 'Legend',
};
