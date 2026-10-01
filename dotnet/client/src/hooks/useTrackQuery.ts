import { useQuery } from '@tanstack/react-query';
import { fetchTracks } from '@/lib/api';
import type { TrackParams } from '@/types/api';

export function useTrackQuery(params: TrackParams | null) {
  return useQuery({
    queryKey: ['tracks', params],
    queryFn: () => fetchTracks(params!),
    enabled: params !== null && !!params.seed && !!params.event,
  });
}
