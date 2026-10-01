import { useMutation } from '@tanstack/react-query';
import { simulate } from '@/lib/api';

export function useSimulate() {
  return useMutation({
    mutationFn: simulate,
  });
}
