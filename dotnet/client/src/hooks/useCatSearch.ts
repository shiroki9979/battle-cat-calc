import { useQuery } from '@tanstack/react-query';
import { searchCats } from '@/lib/api';
import type { CatSearchParams } from '@/types/api';

export function useCatSearch(params: CatSearchParams | null) {
  return useQuery({
    queryKey: ['cats', 'search', params],
    queryFn: () => searchCats(params!),
    enabled: params !== null && !!params.seed && !!params.event && !!params.name,
  });
}
