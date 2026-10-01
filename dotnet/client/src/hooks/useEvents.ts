import { useQuery } from '@tanstack/react-query';
import { fetchEvents } from '@/lib/api';

export function useEvents(lang = 'en') {
  return useQuery({
    queryKey: ['events', lang],
    queryFn: () => fetchEvents(lang),
    staleTime: Infinity,
  });
}
