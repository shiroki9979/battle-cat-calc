import type {
  CatSearchParams,
  CatSearchResult,
  EventDto,
  ProblemDetails,
  SimulateRequest,
  SimulateResponse,
  TrackParams,
  TrackResponse,
} from '@/types/api';

const API_BASE = '/api/v1';

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
  });

  if (!response.ok) {
    let detail = response.statusText || 'Request failed';
    try {
      const problem = (await response.json()) as Partial<ProblemDetails>;
      if (problem.detail) detail = problem.detail;
    } catch {
      // 非 JSON 錯誤本體時沿用 HTTP status text
    }
    throw new Error(detail);
  }

  return (await response.json()) as T;
}

function toQuery(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') {
      search.set(key, String(value));
    }
  }
  const query = search.toString();
  return query ? `?${query}` : '';
}

export function fetchEvents(lang = 'en'): Promise<EventDto[]> {
  return request(`/events${toQuery({ lang })}`);
}

export function fetchTracks(params: TrackParams): Promise<TrackResponse> {
  return request(
    `/tracks${toQuery({
      seed: params.seed,
      event: params.event,
      lang: params.lang,
      count: params.count,
    })}`,
  );
}

export function simulate(body: SimulateRequest): Promise<SimulateResponse> {
  return request('/simulate', {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

export function searchCats(params: CatSearchParams): Promise<CatSearchResult[]> {
  return request(
    `/cats/search${toQuery({
      seed: params.seed,
      event: params.event,
      name: params.name,
      lang: params.lang,
      count: params.count,
    })}`,
  );
}
